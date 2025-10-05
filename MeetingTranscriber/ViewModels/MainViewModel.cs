using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MeetingTranscriber.Services;

namespace MeetingTranscriber.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly AudioRecordingService _audioService;
        private string _status = "Ready";
        private bool _isRecording;
        private string _recordingButtonText = "Start Recording";
        private float _audioLevel;
        private RecordingSource _selectedRecordingSource = RecordingSource.Microphone;
        private string? _currentRecordingPath;
        private bool _disposed;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (SetProperty(ref _isRecording, value))
                {
                    RecordingButtonText = value ? "Stop Recording" : "Start Recording";
                }
            }
        }

        public string RecordingButtonText
        {
            get => _recordingButtonText;
            set => SetProperty(ref _recordingButtonText, value);
        }

        public float AudioLevel
        {
            get => _audioLevel;
            set => SetProperty(ref _audioLevel, value);
        }

        public RecordingSource SelectedRecordingSource
        {
            get => _selectedRecordingSource;
            set => SetProperty(ref _selectedRecordingSource, value);
        }

        public ObservableCollection<string> RecordingSources { get; }

        public ICommand ToggleRecordingCommand { get; }
        public ICommand SelectMicrophoneCommand { get; }
        public ICommand SelectSystemAudioCommand { get; }
        public ICommand SelectBothCommand { get; }

        public MainViewModel()
        {
            _audioService = new AudioRecordingService();

            // Subscribe to events
            _audioService.StatusChanged += OnRecordingStatusChanged;
            _audioService.AudioLevelUpdated += OnAudioLevelUpdated;
            _audioService.ErrorOccurred += OnErrorOccurred;

            // Initialize commands
            ToggleRecordingCommand = new RelayCommand(ToggleRecording);
            SelectMicrophoneCommand = new RelayCommand(() => SelectedRecordingSource = RecordingSource.Microphone);
            SelectSystemAudioCommand = new RelayCommand(() => SelectedRecordingSource = RecordingSource.SystemAudio);
            SelectBothCommand = new RelayCommand(() => SelectedRecordingSource = RecordingSource.Both);

            // Initialize recording sources
            RecordingSources = new ObservableCollection<string>
            {
                "Microphone",
                "System Audio",
                "Both"
            };
        }

        private void ToggleRecording()
        {
            if (!IsRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }

        private void StartRecording()
        {
            try
            {
                _audioService.StartRecording(SelectedRecordingSource);
                IsRecording = true;
            }
            catch (Exception ex)
            {
                Status = $"Error starting recording: {ex.Message}";
                IsRecording = false;
            }
        }

        private void StopRecording()
        {
            try
            {
                _audioService.StopRecording();
                IsRecording = false;
            }
            catch (Exception ex)
            {
                Status = $"Error stopping recording: {ex.Message}";
            }
        }

        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case RecordingStatus.Recording:
                    _currentRecordingPath = e.FileName;
                    var fileName = !string.IsNullOrEmpty(e.FileName) ? Path.GetFileName(e.FileName) : "unknown";
                    Status = $"Recording to {fileName}...";
                    break;

                case RecordingStatus.Idle:
                    if (!string.IsNullOrEmpty(_currentRecordingPath))
                    {
                        Status = $"Recording saved: {Path.GetFileName(_currentRecordingPath)}";
                    }
                    else
                    {
                        Status = "Recording stopped";
                    }
                    AudioLevel = 0; // Reset audio level
                    break;

                case RecordingStatus.Stopping:
                    Status = "Stopping recording...";
                    break;

                case RecordingStatus.Error:
                    Status = $"Recording error: {e.Message}";
                    IsRecording = false;
                    break;
            }
        }

        private void OnAudioLevelUpdated(object? sender, AudioLevelEventArgs e)
        {
            // Update audio level for visualization (0-1 range)
            AudioLevel = Math.Min(1.0f, e.Level);
        }

        private void OnErrorOccurred(object? sender, Exception e)
        {
            Status = $"Error: {e.Message}";
            IsRecording = false;
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
                    // Unsubscribe from events
                    if (_audioService != null)
                    {
                        _audioService.StatusChanged -= OnRecordingStatusChanged;
                        _audioService.AudioLevelUpdated -= OnAudioLevelUpdated;
                        _audioService.ErrorOccurred -= OnErrorOccurred;
                        _audioService.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }
}