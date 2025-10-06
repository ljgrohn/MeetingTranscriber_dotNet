using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MeetingTranscriber.Services;

namespace MeetingTranscriber.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly Window _window;
        private string _assemblyAIApiKey = string.Empty;
        private string _openAIApiKey = string.Empty;
        private string _saveDirectory = string.Empty;
        private string? _statusMessage;
        private Brush _statusColor = Brushes.Green;

        public string AssemblyAIApiKey
        {
            get => _assemblyAIApiKey;
            set => SetProperty(ref _assemblyAIApiKey, value);
        }

        public string OpenAIApiKey
        {
            get => _openAIApiKey;
            set => SetProperty(ref _openAIApiKey, value);
        }

        public string SaveDirectory
        {
            get => _saveDirectory;
            set => SetProperty(ref _saveDirectory, value);
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public SettingsViewModel(Window window)
        {
            _window = window;
            _settingsService = new SettingsService();

            // Load existing settings
            var settings = _settingsService.LoadSettings();
            _assemblyAIApiKey = settings.AssemblyAIApiKey ?? string.Empty;
            _openAIApiKey = settings.OpenAIApiKey ?? string.Empty;
            _saveDirectory = settings.SaveDirectory ?? string.Empty;

            // Initialize commands
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save()
        {
            try
            {
                // At least one API key should be provided
                if (string.IsNullOrWhiteSpace(AssemblyAIApiKey) && string.IsNullOrWhiteSpace(OpenAIApiKey))
                {
                    StatusMessage = "Please enter at least one API key";
                    StatusColor = Brushes.Red;
                    return;
                }

                var settings = new AppSettings
                {
                    AssemblyAIApiKey = string.IsNullOrWhiteSpace(AssemblyAIApiKey) ? null : AssemblyAIApiKey,
                    OpenAIApiKey = string.IsNullOrWhiteSpace(OpenAIApiKey) ? null : OpenAIApiKey,
                    SaveDirectory = string.IsNullOrWhiteSpace(SaveDirectory) ? null : SaveDirectory
                };

                _settingsService.SaveSettings(settings);

                StatusMessage = "Settings saved successfully!";
                StatusColor = Brushes.Green;

                // Close window after a brief delay
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    _window.DialogResult = true;
                    _window.Close();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
                StatusColor = Brushes.Red;
            }
        }

        private void Cancel()
        {
            _window.DialogResult = false;
            _window.Close();
        }
    }
}
