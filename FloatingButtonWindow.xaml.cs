using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace Kolsites
{
    public sealed partial class FloatingButtonWindow : Window
    {
        private readonly AppSettings _settings;
        private KioskWindow? _kioskWindow;

        public FloatingButtonWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            Title = "Kolsites";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            // החלת ערכת הנושא הנבחרת
            if (RootGrid is FrameworkElement fe)
                fe.RequestedTheme = ThemeHelper.ToElementTheme(_settings.Theme);

            ConfigureWindow();
            ApplyButtonAppearance();

            Activated += (_, _) => EnsureTopmost();
        }

        private void ConfigureWindow()
        {
            WindowHelper.SetIcon(this);
            WindowHelper.ConfigureBorderlessTopmost(this, resizable: false);
            WindowHelper.HideFromTaskbar(this);

            PositionWindow();

            // רקע שקוף לחלון - מתבצע ע"י WS_EX_LAYERED + צבע מפתח
            MakeWindowTransparent();
        }

        private void ApplyButtonAppearance()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ButtonLabel))
            {
                ButtonText.Text = _settings.ButtonLabel;
                ButtonText.Visibility = Visibility.Visible;
            }
            else
            {
                ButtonText.Visibility = Visibility.Collapsed;
            }
        }

        private void PositionWindow()
        {
            var (waX, waY, waW, waH) = WindowHelper.GetWorkArea(this);
            var scale = WindowHelper.GetDpiScale(this);
            int size = (int)(_settings.ButtonSize * scale);
            int margin = (int)(_settings.ButtonMargin * scale);

            int x = _settings.ButtonPosition switch
            {
                FloatingButtonPosition.TopLeft or FloatingButtonPosition.BottomLeft or FloatingButtonPosition.LeftCenter
                    => waX + margin,
                FloatingButtonPosition.TopRight or FloatingButtonPosition.BottomRight or FloatingButtonPosition.RightCenter
                    => waX + waW - size - margin,
                FloatingButtonPosition.TopCenter or FloatingButtonPosition.BottomCenter
                    => waX + (waW - size) / 2,
                _ => waX + waW - size - margin
            };

            int y = _settings.ButtonPosition switch
            {
                FloatingButtonPosition.TopLeft or FloatingButtonPosition.TopRight or FloatingButtonPosition.TopCenter
                    => waY + margin,
                FloatingButtonPosition.BottomLeft or FloatingButtonPosition.BottomRight or FloatingButtonPosition.BottomCenter
                    => waY + waH - size - margin,
                FloatingButtonPosition.LeftCenter or FloatingButtonPosition.RightCenter
                    => waY + (waH - size) / 2,
                _ => waY + waH - size - margin
            };

            WindowHelper.MoveAndResize(this, x, y, size, size);
        }

        private void EnsureTopmost()
        {
            var appWindow = WindowHelper.GetAppWindow(this);
            if (appWindow?.Presenter is OverlappedPresenter op)
            {
                op.IsAlwaysOnTop = true;
            }
        }

        private void MakeWindowTransparent()
        {
            // הפיכת רקע החלון לשקוף כדי שרק הכפתור (העגול/מעוגל) יוצג
            var hWnd = WindowHelper.GetHwnd(this);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            // צבע מפתח - מגנטה (255,0,255) לא מופיע בכפתור הרגיל,
            // ולכן ייהפך לשקוף ב-LWA_COLORKEY
            SetLayeredWindowAttributes(hWnd, 0x00FF00FF, 255, LWA_COLORKEY | LWA_ALPHA);

            // צביעת רקע ה-WinUI Grid במגנטה
            RootGrid.Background = new SolidColorBrush(Color.FromArgb(255, 255, 0, 255));
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            // הסתרת הכפתור בעת פתיחת חלון הקיוסק
            this.AppWindow?.Hide();

            _kioskWindow = new KioskWindow(_settings, OnKioskClosed);
            _kioskWindow.Activate();
        }

        private void OnKioskClosed()
        {
            // החזרת הכפתור הצף לאחר סגירת חלון הקיוסק
            try
            {
                _kioskWindow = null;
                this.AppWindow?.Show();
                EnsureTopmost();
            }
            catch { }
        }

        // P/Invoke - שקיפות חלון
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int LWA_COLORKEY = 0x00000001;
        private const int LWA_ALPHA = 0x00000002;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, int dwFlags);
    }
}
