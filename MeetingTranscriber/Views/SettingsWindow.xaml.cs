using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using MeetingTranscriber.ViewModels;

namespace MeetingTranscriber.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel(this);
            DataContext = _viewModel;

            // Load existing API key if available
            if (!string.IsNullOrEmpty(_viewModel.ApiKey))
            {
                ApiKeyPasswordBox.Password = _viewModel.ApiKey;
            }
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.ApiKey = ApiKeyPasswordBox.Password;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
