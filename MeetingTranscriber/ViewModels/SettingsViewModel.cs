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
        private string _apiKey = string.Empty;
        private string? _statusMessage;
        private Brush _statusColor = Brushes.Green;

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
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
            _apiKey = settings.AssemblyAIApiKey ?? string.Empty;

            // Initialize commands
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    StatusMessage = "Please enter an API key";
                    StatusColor = Brushes.Red;
                    return;
                }

                var settings = new AppSettings
                {
                    AssemblyAIApiKey = ApiKey
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
