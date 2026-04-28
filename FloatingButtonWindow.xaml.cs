using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
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
        private DispatcherQueueTimer? _topmostTimer;

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
            StartTopmostEnforcement();

            Activated += (_, _) => EnsureTopmost();
            Closed += (_, _) => _topmostTimer?.Stop();
        }

        /// <summary>
        /// Timer שמוודא שהכפתור הצף נשאר תמיד-למעלה מעל כל החלונות.
        /// IsAlwaysOnTop של WinUI לבד לא תמיד מספיק מול תוכנות מסך מלא;
        /// קריאה תקופתית ל-SetWindowPos עם HWND_TOPMOST מבטיחה שמירה על הקדמה.
        /// </summary>
        private void StartTopmostEnforcement()
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            _topmostTimer = dispatcher.CreateTimer();
            _topmostTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _topmostTimer.Tick += (_, _) => EnsureTopmost();
            _topmostTimer.Start();
        }

        private void ConfigureWindow()
        {
            WindowHelper.SetIcon(this);
            WindowHelper.ConfigureBorderlessTopmost(this, resizable: false);
            WindowHelper.HideFromTaskbar(this);

            PositionWindow();

            // החלון מסומן כ-WS_EX_TOOLWINDOW כדי שלא יופיע ב-Alt+Tab.
            // רקע החלון לבן אטום (אין יותר מנגנון color-key) - הכפתור מלבני עם פינות מעוגלות.
            ApplyToolWindowExStyle();
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
                FloatingButtonPosition.TopLeft
                or FloatingButtonPosition.BottomLeft
                or FloatingButtonPosition.LeftCenter => waX + margin,
                FloatingButtonPosition.TopRight
                or FloatingButtonPosition.BottomRight
                or FloatingButtonPosition.RightCenter => waX + waW - size - margin,
                FloatingButtonPosition.TopCenter or FloatingButtonPosition.BottomCenter => waX
                    + (waW - size) / 2,
                _ => waX + waW - size - margin,
            };

            int y = _settings.ButtonPosition switch
            {
                FloatingButtonPosition.TopLeft
                or FloatingButtonPosition.TopRight
                or FloatingButtonPosition.TopCenter => waY + margin,
                FloatingButtonPosition.BottomLeft
                or FloatingButtonPosition.BottomRight
                or FloatingButtonPosition.BottomCenter => waY + waH - size - margin,
                FloatingButtonPosition.LeftCenter or FloatingButtonPosition.RightCenter => waY
                    + (waH - size) / 2,
                _ => waY + waH - size - margin,
            };

            WindowHelper.MoveAndResize(this, x, y, size, size);
        }

        private void EnsureTopmost()
        {
            try
            {
                var appWindow = WindowHelper.GetAppWindow(this);
                if (appWindow?.Presenter is OverlappedPresenter op)
                {
                    op.IsAlwaysOnTop = true;
                }

                // חשוב: SetWindowPos עם HWND_TOPMOST + SWP_NOACTIVATE
                // זה הדרך האמינה ביותר לשמור על הכפתור מעל הכל,
                // בלי לגנוב פוקוס מהתוכנה הפעילה (חשוב כי הכפתור לא צריך פוקוס - רק נראות).
                var hWnd = WindowHelper.GetHwnd(this);
                // ללא SWP_SHOWWINDOW כדי שלא נחזיר לראווה כפתור שהוסתר ידנית בעת פתיחת הקיוסק
                SetWindowPos(
                    hWnd,
                    HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
                );
            }
            catch { }
        }

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );

        private void ApplyToolWindowExStyle()
        {
            // הוספת WS_EX_TOOLWINDOW כדי שהחלון לא יופיע ב-Alt+Tab או ב-taskbar.
            var hWnd = WindowHelper.GetHwnd(this);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            _topmostTimer?.Stop();

            // הצגת חיווי טעינה במקום האייקון - משוב מיידי למשתמש שהלחיצה התקבלה
            // ושהמערכת בעיצומה של פתיחת החלון.
            ShowLoadingIndicator(true);
            OpenButton.IsEnabled = false;

            // Hiding the floating button only after the kiosk window has actually come up on the screen
            // (Activated event fires after the window is already visible to the user).
            // This avoids a "blank" moment where there is no button and no window, which makes you think the software has closed.
            var kiosk = new KioskWindow(_settings, OnKioskClosed);
            _kioskWindow = kiosk;

            bool floatingHidden = false;
            kiosk.Activated += (_, _) =>
            {
                if (floatingHidden)
                    return;
                floatingHidden = true;
                this.AppWindow?.Hide();
            };
            kiosk.Activate();
        }

        private void OnKioskClosed()
        {
            // החזרת הכפתור הצף לאחר סגירת חלון הקיוסק
            try
            {
                _kioskWindow = null;
                ShowLoadingIndicator(false);
                OpenButton.IsEnabled = true;
                this.AppWindow?.Show();
                EnsureTopmost();
                _topmostTimer?.Start();
            }
            catch { }
        }

        private void ShowLoadingIndicator(bool show)
        {
            LoadingRing.IsActive = show;
            LoadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ButtonImage.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            ButtonText.Visibility =
                show || string.IsNullOrWhiteSpace(_settings.ButtonLabel)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        // P/Invoke - extended window style
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
