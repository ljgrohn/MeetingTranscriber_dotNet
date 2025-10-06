using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Navigation;
using MeetingTranscriber.ViewModels;
using Microsoft.Win32;

namespace MeetingTranscriber.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public SettingsWindow()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel(this);
            DataContext = _viewModel;

            // Enable dark title bar and remove border
            Loaded += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                int value = 1;
                DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));

                // Extend frame to remove border
                MARGINS margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 1 };
                DwmExtendFrameIntoClientArea(helper.Handle, ref margins);
            };

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

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Save Directory for Summaries"
            };

            if (!string.IsNullOrEmpty(_viewModel.SaveDirectory))
            {
                dialog.InitialDirectory = _viewModel.SaveDirectory;
            }

            if (dialog.ShowDialog() == true)
            {
                _viewModel.SaveDirectory = dialog.FolderName;
            }
        }
    }
}
