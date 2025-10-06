using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MeetingTranscriber.Models;
using MeetingTranscriber.Services;

namespace MeetingTranscriber.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly AudioRecordingService _audioService;
        private readonly TranscriptionService _transcriptionService;
        private readonly AISummaryService _summaryService;
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;
        private readonly FileStorageService _fileStorageService;
        private TranscriptHistoryEntry? _currentHistoryEntry;
        private string _status = "Ready";
        private bool _isRecording;
        private string _recordingButtonText = "Start Recording";
        private float _audioLevel;
        private double _audioLevelPercent;
        private RecordingSource _selectedRecordingSource = RecordingSource.Both;
        private string? _currentRecordingPath;
        private string? _recordingPath;
        private string? _transcriptText;
        private string? _summaryText;
        private bool _isTranscribing;
        private bool _isGeneratingSummary;
        private bool _canTranscribe;
        private bool _canGenerateSummary;
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
            set
            {
                if (SetProperty(ref _audioLevel, value))
                {
                    AudioLevelPercent = value * 100;
                }
            }
        }

        public double AudioLevelPercent
        {
            get => _audioLevelPercent;
            set => SetProperty(ref _audioLevelPercent, value);
        }

        public string? RecordingPath
        {
            get => _recordingPath;
            set => SetProperty(ref _recordingPath, value);
        }

        public string? TranscriptText
        {
            get => _transcriptText;
            set => SetProperty(ref _transcriptText, value);
        }

        public string? SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public bool IsTranscribing
        {
            get => _isTranscribing;
            set => SetProperty(ref _isTranscribing, value);
        }

        public bool IsGeneratingSummary
        {
            get => _isGeneratingSummary;
            set => SetProperty(ref _isGeneratingSummary, value);
        }

        public bool CanTranscribe
        {
            get => _canTranscribe;
            set => SetProperty(ref _canTranscribe, value);
        }

        public bool CanGenerateSummary
        {
            get => _canGenerateSummary;
            set => SetProperty(ref _canGenerateSummary, value);
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
        public ICommand OpenRecordingsFolderCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand TranscribeCommand { get; }
        public ICommand GenerateSummaryCommand { get; }

        public MainViewModel()
        {
            _audioService = new AudioRecordingService();
            _transcriptionService = new TranscriptionService();
            _summaryService = new AISummaryService();
            _settingsService = new SettingsService();
            _historyService = new HistoryService();
            _fileStorageService = new FileStorageService();

            // Subscribe to events
            _audioService.StatusChanged += OnRecordingStatusChanged;
            _audioService.AudioLevelUpdated += OnAudioLevelUpdated;
            _audioService.ErrorOccurred += OnErrorOccurred;

            _transcriptionService.StatusChanged += OnTranscriptionStatusChanged;
            _transcriptionService.TranscriptionCompleted += OnTranscriptionCompleted;
            _transcriptionService.ErrorOccurred += OnTranscriptionError;

            _summaryService.StatusChanged += OnSummaryStatusChanged;
            _summaryService.SummaryCompleted += OnSummaryCompleted;
            _summaryService.ErrorOccurred += OnSummaryError;

            // Initialize commands
            ToggleRecordingCommand = new RelayCommand(ToggleRecording);
            SelectMicrophoneCommand = new RelayCommand(() => SelectedRecordingSource = RecordingSource.Microphone);
            SelectSystemAudioCommand = new RelayCommand(() => SelectedRecordingSource = RecordingSource.SystemAudio);
            SelectBothCommand = new RelayCommand(() => SelectedRecordingSource = RecordingSource.Both);
            OpenRecordingsFolderCommand = new RelayCommand(OpenRecordingsFolder);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            TranscribeCommand = new RelayCommand(async () => await TranscribeRecordingAsync(), () => CanTranscribe);
            GenerateSummaryCommand = new RelayCommand(async () => await GenerateSummaryAsync(), () => CanGenerateSummary);

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

        private async void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case RecordingStatus.Recording:
                    _currentRecordingPath = e.FileName;
                    var fileName = !string.IsNullOrEmpty(e.FileName) ? Path.GetFileName(e.FileName) : "unknown";
                    Status = $"Recording to {fileName}...";
                    RecordingPath = e.FileName;
                    CanTranscribe = false;

                    // Create new history entry
                    _currentHistoryEntry = new TranscriptHistoryEntry
                    {
                        RecordingDate = DateTime.Now,
                        RecordingLocation = e.FileName,
                        Stage = ProcessingStage.Recording
                    };
                    _historyService.AddEntry(_currentHistoryEntry);
                    break;

                case RecordingStatus.Idle:
                    // Update to the final file path (mixed file in "Both" mode)
                    if (!string.IsNullOrEmpty(e.FileName))
                    {
                        _currentRecordingPath = e.FileName;
                        Status = $"Recording saved: {Path.GetFileName(_currentRecordingPath)}";
                        RecordingPath = _currentRecordingPath;
                        CanTranscribe = true;

                        // Update history entry to Processing stage and update location
                        if (_currentHistoryEntry != null)
                        {
                            _currentHistoryEntry.RecordingLocation = _currentRecordingPath;
                            _currentHistoryEntry.Stage = ProcessingStage.Processing;
                            _historyService.UpdateEntry(_currentHistoryEntry);
                        }

                        // Auto-trigger transcription
                        await TranscribeRecordingAsync();
                    }
                    else
                    {
                        Status = "Recording stopped";
                        RecordingPath = null;
                        CanTranscribe = false;
                    }
                    AudioLevel = 0; // Reset audio level
                    break;

                case RecordingStatus.Stopping:
                    Status = "Stopping recording...";
                    break;

                case RecordingStatus.Error:
                    Status = $"Recording error: {e.Message}";
                    IsRecording = false;
                    RecordingPath = null;
                    CanTranscribe = false;

                    // Update history entry to Error stage
                    if (_currentHistoryEntry != null)
                    {
                        _currentHistoryEntry.Stage = ProcessingStage.Error;
                        _historyService.UpdateEntry(_currentHistoryEntry);
                    }
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

        private void OpenRecordingsFolder()
        {
            try
            {
                var recordingsPath = _audioService.GetRecordingsDirectory();
                if (Directory.Exists(recordingsPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", recordingsPath);
                }
                else
                {
                    Status = "Recordings folder not found";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error opening folder: {ex.Message}";
            }
        }

        private void OpenSettings()
        {
            var settingsWindow = new Views.SettingsWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
        }

        private async Task TranscribeRecordingAsync()
        {
            if (string.IsNullOrEmpty(_currentRecordingPath))
            {
                Status = "No recording to transcribe";
                return;
            }

            var settings = _settingsService.LoadSettings();
            if (string.IsNullOrEmpty(settings.AssemblyAIApiKey))
            {
                Status = "Please set your AssemblyAI API key in Settings";
                return;
            }

            try
            {
                IsTranscribing = true;
                CanTranscribe = false;
                TranscriptText = "Transcribing...";

                // Update history entry to Transcribing stage
                if (_currentHistoryEntry != null)
                {
                    _currentHistoryEntry.Stage = ProcessingStage.Transcribing;
                    _historyService.UpdateEntry(_currentHistoryEntry);
                }

                var transcript = await _transcriptionService.TranscribeAudioAsync(_currentRecordingPath, settings.AssemblyAIApiKey);

                IsTranscribing = false;
            }
            catch (Exception ex)
            {
                IsTranscribing = false;
                CanTranscribe = true;
                Status = $"Transcription failed: {ex.Message}";

                // Update history entry to Error stage
                if (_currentHistoryEntry != null)
                {
                    _currentHistoryEntry.Stage = ProcessingStage.Error;
                    _historyService.UpdateEntry(_currentHistoryEntry);
                }
            }
        }

        private async Task GenerateSummaryAsync()
        {
            if (string.IsNullOrEmpty(TranscriptText))
            {
                Status = "No transcript to summarize";
                return;
            }

            var settings = _settingsService.LoadSettings();
            if (string.IsNullOrEmpty(settings.OpenAIApiKey))
            {
                Status = "Please set your OpenAI API key in Settings";
                return;
            }

            try
            {
                IsGeneratingSummary = true;
                CanGenerateSummary = false;
                SummaryText = "Generating summary...";

                // Update history entry to AISummarizing stage
                if (_currentHistoryEntry != null)
                {
                    _currentHistoryEntry.Stage = ProcessingStage.AISummarizing;
                    _historyService.UpdateEntry(_currentHistoryEntry);
                }

                var summary = await _summaryService.GenerateSummaryAsync(TranscriptText, settings.OpenAIApiKey);

                IsGeneratingSummary = false;
            }
            catch (Exception ex)
            {
                IsGeneratingSummary = false;
                CanGenerateSummary = true;
                Status = $"Summary generation failed: {ex.Message}";

                // Update history entry to Error stage
                if (_currentHistoryEntry != null)
                {
                    _currentHistoryEntry.Stage = ProcessingStage.Error;
                    _historyService.UpdateEntry(_currentHistoryEntry);
                }
            }
        }

        private void OnTranscriptionStatusChanged(object? sender, TranscriptionStatusChangedEventArgs e)
        {
            Status = e.Message ?? e.Status.ToString();
        }

        private async void OnTranscriptionCompleted(object? sender, TranscriptionCompletedEventArgs e)
        {
            TranscriptText = e.TranscriptText;
            CanGenerateSummary = true;
            Status = "Transcription completed! Generating AI summary...";

            // Update history entry with transcript
            if (_currentHistoryEntry != null)
            {
                _currentHistoryEntry.Transcript = e.TranscriptText;
                _historyService.UpdateEntry(_currentHistoryEntry);
            }

            // Auto-trigger AI summary generation
            await GenerateSummaryAsync();
        }

        private void OnTranscriptionError(object? sender, Exception e)
        {
            Status = $"Transcription error: {e.Message}";
            TranscriptText = null;
            CanTranscribe = true;
        }

        private void OnSummaryStatusChanged(object? sender, SummaryStatusChangedEventArgs e)
        {
            Status = e.Message ?? e.Status.ToString();
        }

        private void OnSummaryCompleted(object? sender, SummaryCompletedEventArgs e)
        {
            SummaryText = e.Summary;
            Status = "AI summary generated successfully!";

            // Update history entry with summary and mark as Complete
            if (_currentHistoryEntry != null)
            {
                _currentHistoryEntry.AISummary = e.Summary;
                _currentHistoryEntry.Stage = ProcessingStage.Complete;

                // Extract meeting name from summary (first line after #)
                var lines = e.Summary.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("#") && !line.Contains("##"))
                    {
                        _currentHistoryEntry.MeetingName = line.Trim().TrimStart('#').Trim();
                        break;
                    }
                }

                // If no meeting name found, use a default
                if (string.IsNullOrEmpty(_currentHistoryEntry.MeetingName))
                {
                    _currentHistoryEntry.MeetingName = $"Meeting {_currentHistoryEntry.RecordingDate:yyyy-MM-dd HH:mm}";
                }

                // Save markdown file to disk
                var settings = _settingsService.LoadSettings();
                if (!string.IsNullOrEmpty(settings.SaveDirectory))
                {
                    try
                    {
                        var savedPath = _fileStorageService.SaveMarkdownFile(
                            e.Summary,
                            _currentHistoryEntry.MeetingName ?? "Untitled",
                            settings.SaveDirectory,
                            _currentHistoryEntry.RecordingDate
                        );
                        _currentHistoryEntry.SavedFilePath = savedPath;
                        Status = $"Summary saved to {Path.GetFileName(savedPath)}";
                    }
                    catch (Exception ex)
                    {
                        Status = $"Failed to save summary: {ex.Message}";
                    }
                }

                _historyService.UpdateEntry(_currentHistoryEntry);
            }
        }

        private void OnSummaryError(object? sender, Exception e)
        {
            Status = $"Summary error: {e.Message}";
            SummaryText = null;
            CanGenerateSummary = true;
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
                    // Unsubscribe from events and dispose services
                    if (_audioService != null)
                    {
                        _audioService.StatusChanged -= OnRecordingStatusChanged;
                        _audioService.AudioLevelUpdated -= OnAudioLevelUpdated;
                        _audioService.ErrorOccurred -= OnErrorOccurred;
                        _audioService.Dispose();
                    }

                    if (_transcriptionService != null)
                    {
                        _transcriptionService.StatusChanged -= OnTranscriptionStatusChanged;
                        _transcriptionService.TranscriptionCompleted -= OnTranscriptionCompleted;
                        _transcriptionService.ErrorOccurred -= OnTranscriptionError;
                        _transcriptionService.Dispose();
                    }

                    if (_summaryService != null)
                    {
                        _summaryService.StatusChanged -= OnSummaryStatusChanged;
                        _summaryService.SummaryCompleted -= OnSummaryCompleted;
                        _summaryService.ErrorOccurred -= OnSummaryError;
                        _summaryService.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }
}