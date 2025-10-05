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

            // Load existing API keys if available
            if (!string.IsNullOrEmpty(_viewModel.AssemblyAIApiKey))
            {
                AssemblyAIApiKeyPasswordBox.Password = _viewModel.AssemblyAIApiKey;
            }

            if (!string.IsNullOrEmpty(_viewModel.OpenAIApiKey))
            {
                OpenAIApiKeyPasswordBox.Password = _viewModel.OpenAIApiKey;
            }
        }

        private void AssemblyAIApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.AssemblyAIApiKey = AssemblyAIApiKeyPasswordBox.Password;
        }

        private void OpenAIApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenAIApiKey = OpenAIApiKeyPasswordBox.Password;
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
