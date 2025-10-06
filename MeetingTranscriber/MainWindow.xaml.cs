using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MeetingTranscriber.Views;
using MeetingTranscriber.ViewModels;

namespace MeetingTranscriber;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly RecordingView _recordingView;
    private readonly HistoryView _historyView;

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

    public MainWindow()
    {
        InitializeComponent();

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

        // Initialize views with shared ViewModel for recording view
        _recordingView = new RecordingView
        {
            DataContext = this.DataContext
        };

        _historyView = new HistoryView();

        // Show recording view by default
        ShowRecordingView();
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        ShowRecordingView();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHistoryView();
    }

    private void ShowRecordingView()
    {
        MainContent.Content = _recordingView;
        RecordButton.Style = (Style)FindResource("AccentButtonStyle");
        HistoryButton.ClearValue(Button.StyleProperty);
    }

    private void ShowHistoryView()
    {
        MainContent.Content = _historyView;

        // Refresh history when showing the view
        if (_historyView.DataContext is HistoryViewModel historyViewModel)
        {
            historyViewModel.LoadHistory();
        }

        HistoryButton.Style = (Style)FindResource("AccentButtonStyle");
        RecordButton.ClearValue(Button.StyleProperty);
    }
}