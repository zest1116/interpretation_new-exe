using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace LGCNS.axink.App.Controls
{
    /// <summary>
    /// ThemedTitleBar.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ThemedTitleBar : UserControl
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public ThemedTitleBar()
        {
            InitializeComponent();
            Loaded += ThemedTitleBar_Loaded;
        }

        private void ThemedTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.StateChanged -= Window_StateChanged;
                window.StateChanged += Window_StateChanged;
            }

            //UpdateVisualState();
        }

        private void Window_StateChanged(object? sender, System.EventArgs e)
        {
            ApplyCornerPreference();
            //UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            var window = Window.GetWindow(this);
            if (window == null)
                return;

            bool maximized = window.WindowState == WindowState.Maximized;

            if (TitleBarBorder != null)
            {
                TitleBarBorder.CornerRadius = maximized
                    ? new CornerRadius(0)
                    : new CornerRadius(10, 10, 0, 0);

                TitleBarBorder.Margin = maximized
                    ? new Thickness(0)
                    : new Thickness(0);
            }
        }

        private void ApplyCornerPreference()
        {
            if (Environment.OSVersion.Version.Build < 22000)
                return;

            IntPtr hwnd = new WindowInteropHelper(Window.GetWindow(this)).Handle;
            int pref = Window.GetWindow(this).WindowState == WindowState.Maximized
                ? DWMWCP_DONOTROUND
                : DWMWCP_ROUND;

            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, Marshal.SizeOf<int>());
        }

        
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ThemedTitleBar),
                new PropertyMetadata("Window"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register(nameof(IconSource), typeof(ImageSource), typeof(ThemedTitleBar),
                new PropertyMetadata(null));

        public ImageSource? IconSource
        {
            get => (ImageSource?)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window == null)
                return;

            if (e.ClickCount == 2 && window.ResizeMode != ResizeMode.NoResize)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            try
            {
                window.DragMove();
            }
            catch
            {
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window?.Close();
        }
    }
}
