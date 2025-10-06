using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;

namespace MeetingTranscriber.Services
{
    public enum RecordingSource
    {
        Microphone,
        SystemAudio,
        Both
    }

    public enum RecordingStatus
    {
        Idle,
        Recording,
        Stopping,
        Error
    }

    public class RecordingStatusChangedEventArgs : EventArgs
    {
        public RecordingStatus Status { get; }
        public string? Message { get; }
        public string? FileName { get; }

        public RecordingStatusChangedEventArgs(RecordingStatus status, string? message = null, string? fileName = null)
        {
            Status = status;
            Message = message;
            FileName = fileName;
        }
    }

    public class AudioLevelEventArgs : EventArgs
    {
        public float Level { get; }
        public AudioLevelEventArgs(float level) => Level = level;
    }

    public class AudioRecordingService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WasapiLoopbackCapture? _loopbackCapture;
        private WaveFileWriter? _microphoneWriter;
        private WaveFileWriter? _systemAudioWriter;
        private string? _currentMicrophoneFile;
        private string? _currentSystemAudioFile;
        private string? _currentMixedFile;
        private RecordingSource _recordingSource;
        private RecordingStatus _status = RecordingStatus.Idle;
        private readonly string _recordingsPath;
        private bool _disposed;

        public event EventHandler<RecordingStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<AudioLevelEventArgs>? AudioLevelUpdated;
        public event EventHandler<Exception>? ErrorOccurred;

        public RecordingStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged(value);
                }
            }
        }

        public RecordingSource Source { get; private set; }

        public AudioRecordingService()
        {
            // Create temp directory for recordings
            _recordingsPath = Path.Combine(Path.GetTempPath(), "MeetingTranscriber", "Recordings");
            Directory.CreateDirectory(_recordingsPath);
        }

        public void StartRecording(RecordingSource source = RecordingSource.Microphone)
        {
            if (Status == RecordingStatus.Recording)
            {
                throw new InvalidOperationException("Recording is already in progress");
            }

            try
            {
                _recordingSource = source;
                Source = source;

                switch (source)
                {
                    case RecordingSource.Microphone:
                        StartMicrophoneRecording();
                        break;
                    case RecordingSource.SystemAudio:
                        StartSystemAudioRecording();
                        break;
                    case RecordingSource.Both:
                        StartBothRecording();
                        break;
                }

                Status = RecordingStatus.Recording;
                OnStatusChanged(RecordingStatus.Recording, "Recording started", GetCurrentFileName());
            }
            catch (Exception ex)
            {
                Status = RecordingStatus.Error;
                OnErrorOccurred(ex);
                CleanupRecording();
                throw;
            }
        }

        private void StartMicrophoneRecording()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 16, 2),
                BufferMilliseconds = 50
            };

            _currentMicrophoneFile = GenerateFileName("Microphone");
            _microphoneWriter = new WaveFileWriter(_currentMicrophoneFile, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnMicrophoneDataAvailable;
            _waveIn.RecordingStopped += OnMicrophoneRecordingStopped;

            _waveIn.StartRecording();
        }

        private void StartSystemAudioRecording()
        {
            _loopbackCapture = new WasapiLoopbackCapture();

            _currentSystemAudioFile = GenerateFileName("SystemAudio");
            _systemAudioWriter = new WaveFileWriter(_currentSystemAudioFile, _loopbackCapture.WaveFormat);

            _loopbackCapture.DataAvailable += OnSystemAudioDataAvailable;
            _loopbackCapture.RecordingStopped += OnSystemAudioRecordingStopped;

            _loopbackCapture.StartRecording();
        }

        private void StartBothRecording()
        {
            // Start both microphone and system audio recording
            StartMicrophoneRecording();
            StartSystemAudioRecording();

            // Generate the mixed file path (will be created after recording stops)
            _currentMixedFile = GenerateFileName("Mixed");
        }

        public void StopRecording()
        {
            if (Status != RecordingStatus.Recording)
            {
                return;
            }

            try
            {
                Status = RecordingStatus.Stopping;

                _waveIn?.StopRecording();
                _loopbackCapture?.StopRecording();

                CleanupRecording();

                // Mix audio files if recording both sources
                if (_recordingSource == RecordingSource.Both &&
                    !string.IsNullOrEmpty(_currentMicrophoneFile) &&
                    !string.IsNullOrEmpty(_currentSystemAudioFile) &&
                    !string.IsNullOrEmpty(_currentMixedFile))
                {
                    OnStatusChanged(RecordingStatus.Stopping, "Mixing audio files...", _currentMixedFile);
                    MixAudioFiles(_currentMicrophoneFile, _currentSystemAudioFile, _currentMixedFile);
                }

                Status = RecordingStatus.Idle;
                OnStatusChanged(RecordingStatus.Idle, "Recording stopped", GetCurrentFileName());
            }
            catch (Exception ex)
            {
                Status = RecordingStatus.Error;
                OnErrorOccurred(ex);
                throw;
            }
        }

        private void OnMicrophoneDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                _microphoneWriter?.Write(e.Buffer, 0, e.BytesRecorded);

                // Calculate audio level for visualization
                if (e.BytesRecorded > 0)
                {
                    var level = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
                    OnAudioLevelUpdated(level);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        private void OnSystemAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                _systemAudioWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        private void OnMicrophoneRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                OnErrorOccurred(e.Exception);
            }
        }

        private void OnSystemAudioRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                OnErrorOccurred(e.Exception);
            }
        }

        private float CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            float max = 0;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                float sampleValue = sample / 32768f;
                max = Math.Max(max, Math.Abs(sampleValue));
            }
            return max;
        }

        private string GenerateFileName(string prefix)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{prefix}_{timestamp}.wav";
            return Path.Combine(_recordingsPath, fileName);
        }

        private void MixAudioFiles(string microphoneFile, string systemAudioFile, string outputFile)
        {
            try
            {
                using var micReader = new AudioFileReader(microphoneFile);
                using var systemReader = new AudioFileReader(systemAudioFile);

                // Convert both to same format if needed and mix them
                var mixer = new MixingSampleProvider(new[] { micReader, systemReader });

                // Convert back to wave format for writing
                WaveFileWriter.CreateWaveFile16(outputFile, mixer);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to mix audio files: {ex.Message}", ex);
            }
        }

        private string? GetCurrentFileName()
        {
            return _recordingSource switch
            {
                RecordingSource.Microphone => _currentMicrophoneFile,
                RecordingSource.SystemAudio => _currentSystemAudioFile,
                RecordingSource.Both => _currentMixedFile, // Use mixed file for transcription
                _ => null
            };
        }

        private void CleanupRecording()
        {
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;

            _systemAudioWriter?.Dispose();
            _systemAudioWriter = null;

            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnMicrophoneDataAvailable;
                _waveIn.RecordingStopped -= OnMicrophoneRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_loopbackCapture != null)
            {
                _loopbackCapture.DataAvailable -= OnSystemAudioDataAvailable;
                _loopbackCapture.RecordingStopped -= OnSystemAudioRecordingStopped;
                _loopbackCapture.Dispose();
                _loopbackCapture = null;
            }
        }

        protected virtual void OnStatusChanged(RecordingStatus status, string? message = null, string? fileName = null)
        {
            StatusChanged?.Invoke(this, new RecordingStatusChangedEventArgs(status, message, fileName));
        }

        protected virtual void OnAudioLevelUpdated(float level)
        {
            AudioLevelUpdated?.Invoke(this, new AudioLevelEventArgs(level));
        }

        protected virtual void OnErrorOccurred(Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (Status == RecordingStatus.Recording)
                    {
                        StopRecording();
                    }

                    CleanupRecording();
                }

                _disposed = true;
            }
        }

        public string GetRecordingsDirectory() => _recordingsPath;

        public string[] GetRecordingFiles()
        {
            if (Directory.Exists(_recordingsPath))
            {
                return Directory.GetFiles(_recordingsPath, "*.wav");
            }
            return Array.Empty<string>();
        }
    }
}