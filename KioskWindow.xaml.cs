using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.UI;
using WinRT.Interop;

namespace Kolsites
{
    public sealed partial class KioskWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Action _onClosed;
        private readonly DispatcherQueueTimer _topmostTimer;
        private readonly DispatcherQueueTimer _internetTimer;
        private bool _webViewReady;
        private string? _currentUrl;
        private SiteButton? _currentButton;

        public KioskWindow(AppSettings settings, Action onClosed)
        {
            InitializeComponent();
            _settings = settings;
            _onClosed = onClosed;

            Title = "Kolsites - תצוגה עילית";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            if (RootGrid is FrameworkElement fe)
                fe.RequestedTheme = ThemeHelper.ToElementTheme(_settings.Theme);

            ConfigureWindow();
            BuildSiteButtons();
            HookKeyboard();

            // Topmost timer - שומר את החלון מקדימה למעלה (מחקה את Timer1 בתוכנה הישנה)
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            _topmostTimer = dispatcher.CreateTimer();
            _topmostTimer.Interval = TimeSpan.FromSeconds(2);
            _topmostTimer.Tick += (_, _) => EnsureTopmost();
            _topmostTimer.Start();

            // Internet timer
            _internetTimer = dispatcher.CreateTimer();
            _internetTimer.Interval = TimeSpan.FromSeconds(5);
            _internetTimer.Tick += async (_, _) => await CheckInternetAsync();
            _internetTimer.Start();

            Activated += (_, _) => EnsureTopmost();

            Closed += KioskWindow_Closed;

            _ = InitializeWebViewAsync();
            _ = CheckInternetAsync();
        }

        private void ConfigureWindow()
        {
            WindowHelper.SetIcon(this);
            WindowHelper.HideFromTaskbar(this);

            // חלון מסך מלא ללא מסגרת + תמיד למעלה
            var appWindow = WindowHelper.GetAppWindow(this);
            if (appWindow?.Presenter is OverlappedPresenter op)
            {
                op.SetBorderAndTitleBar(false, false);
                op.IsAlwaysOnTop = true;
                op.IsResizable = false;
                op.IsMaximizable = false;
                op.IsMinimizable = false;
            }

            // התאמת גודל ומיקום למסך המלא
            var (waX, waY, waW, waH) = WindowHelper.GetWorkArea(this);
            // המסך המלא כולל taskbar
            var displayArea = DisplayArea.GetFromWindowId(appWindow!.Id, DisplayAreaFallback.Primary);
            int x = displayArea.OuterBounds.X;
            int y = displayArea.OuterBounds.Y;
            int w = displayArea.OuterBounds.Width;
            int h = displayArea.OuterBounds.Height;
            WindowHelper.MoveAndResize(this, x, y, w, h);
        }

        private void EnsureTopmost()
        {
            try
            {
                var appWindow = WindowHelper.GetAppWindow(this);
                if (appWindow?.Presenter is OverlappedPresenter op)
                    op.IsAlwaysOnTop = true;
                SetForegroundWindow(WindowHelper.GetHwnd(this));
            }
            catch { }
        }

        private void BuildSiteButtons()
        {
            SiteButtonsHost.Items.Clear();
            foreach (var btn in _settings.Buttons.Where(b => b.Enabled))
            {
                SiteButtonsHost.Items.Add(CreateSiteButtonControl(btn));
            }
        }

        private FrameworkElement CreateSiteButtonControl(SiteButton btn)
        {
            // ניסיון לפרש HEX לצבע
            var bgBrush = TryParseColorBrush(btn.BackgroundColor)
                ?? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

            var button = new Button
            {
                Width = 130,
                Height = 100,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(12),
                Background = bgBrush,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(button, btn.Name);

            var content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 6
            };

            // אייקון - או מתמונה מותאמת או FontIcon ברירת מחדל
            if (!string.IsNullOrWhiteSpace(btn.IconPath) && System.IO.File.Exists(btn.IconPath))
            {
                content.Children.Add(new Image
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(btn.IconPath)),
                    Width = 40,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
            else
            {
                content.Children.Add(new FontIcon
                {
                    Glyph = "", // Globe / Web
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.White)
                });
            }

            // טקסט תווית
            if (!string.IsNullOrWhiteSpace(btn.Label))
            {
                content.Children.Add(new TextBlock
                {
                    Text = btn.Label,
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.White)
                });
            }

            button.Content = content;
            button.Click += async (_, _) => await NavigateAsync(btn);
            return button;
        }

        private static Brush? TryParseColorBrush(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try
            {
                var s = hex.TrimStart('#');
                if (s.Length == 6)
                {
                    byte r = Convert.ToByte(s.Substring(0, 2), 16);
                    byte g = Convert.ToByte(s.Substring(2, 2), 16);
                    byte b = Convert.ToByte(s.Substring(4, 2), 16);
                    return new SolidColorBrush(Color.FromArgb(255, r, g, b));
                }
            }
            catch { }
            return null;
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                // ספריית UserData בתיקיית ההגדרות - לאפשר ניקוי cache
                var userDataFolder = System.IO.Path.Combine(
                    SettingsManager.GetSettingsFolder(), "WebView2");
                System.IO.Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null, userDataFolder, null);

                await WebView.EnsureCoreWebView2Async(environment);
                _webViewReady = true;

                if (WebView.CoreWebView2 != null)
                {
                    var s = WebView.CoreWebView2.Settings;
                    s.AreDefaultContextMenusEnabled = !_settings.BlockContextMenu;
                    s.AreDevToolsEnabled = false;
                    s.IsStatusBarEnabled = false;
                    s.IsZoomControlEnabled = true;

                    WebView.CoreWebView2.NewWindowRequested += (sender, e) =>
                    {
                        // פתיחת חלונות פופ-אפ באותו WebView (קיוסק - לא רוצים דפדפן חיצוני)
                        e.NewWindow = sender;
                        e.Handled = true;
                    };

                    WebView.CoreWebView2.NavigationStarting += (_, _) =>
                    {
                        LoadingPanel.Visibility = Visibility.Visible;
                        WelcomePanel.Visibility = Visibility.Collapsed;
                    };
                    WebView.CoreWebView2.NavigationCompleted += (_, _) =>
                    {
                        LoadingPanel.Visibility = Visibility.Collapsed;
                    };
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה ב-WebView2: {ex.Message}";
            }
        }

        private async Task NavigateAsync(SiteButton btn)
        {
            if (!_webViewReady)
            {
                // עדיין לא מוכן - ננסה שוב לאחר השלמה
                StatusText.Text = "טוען מנוע דפדפן...";
                while (!_webViewReady)
                    await Task.Delay(100);
            }

            _currentButton = btn;
            _currentUrl = AppendCacheBuster(btn.Url);

            WelcomePanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            StatusText.Text = btn.Name;

            try
            {
                WebView.CoreWebView2.Navigate(_currentUrl);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"נכשל בטעינה: {ex.Message}";
            }
        }

        private static string AppendCacheBuster(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            var separator = url.Contains('?') ? "&" : "?";
            return $"{url}{separator}_t={DateTime.Now.Ticks}";
        }

        private void HookKeyboard()
        {
            if (!_settings.ShowVirtualKeyboard)
                return;

            Keyboard.SetInitialLayout(_settings.DefaultKeyboardLayout);

            Keyboard.KeyPressed += async (key) =>
            {
                if (!_webViewReady) return;
                try
                {
                    var escaped = System.Text.Json.JsonSerializer.Serialize(key);
                    var script = $@"(function(){{
                        var el = document.activeElement;
                        if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable)) {{
                            if (el.isContentEditable) {{
                                document.execCommand('insertText', false, {escaped});
                            }} else {{
                                var start = el.selectionStart || el.value.length;
                                var end = el.selectionEnd || el.value.length;
                                el.value = el.value.substring(0, start) + {escaped} + el.value.substring(end);
                                var newPos = start + {escaped}.length;
                                el.setSelectionRange(newPos, newPos);
                                el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                            }}
                            el.focus();
                        }}
                    }})();";
                    await WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch { }
            };

            Keyboard.BackspacePressed += async () =>
            {
                if (!_webViewReady) return;
                try
                {
                    var script = @"(function(){
                        var el = document.activeElement;
                        if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) {
                            var start = el.selectionStart;
                            var end = el.selectionEnd;
                            if (start === end && start > 0) {
                                el.value = el.value.substring(0, start - 1) + el.value.substring(end);
                                el.setSelectionRange(start - 1, start - 1);
                            } else if (start !== end) {
                                el.value = el.value.substring(0, start) + el.value.substring(end);
                                el.setSelectionRange(start, start);
                            }
                            el.dispatchEvent(new Event('input', { bubbles: true }));
                            el.focus();
                        } else if (el && el.isContentEditable) {
                            document.execCommand('delete', false, null);
                        }
                    })();";
                    await WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch { }
            };

            Keyboard.EnterPressed += async () =>
            {
                if (!_webViewReady) return;
                try
                {
                    var script = @"(function(){
                        var el = document.activeElement;
                        if (el) {
                            ['keydown','keypress','keyup'].forEach(function(t){
                                el.dispatchEvent(new KeyboardEvent(t, { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true }));
                            });
                            if (el.form && typeof el.form.requestSubmit === 'function') {
                                try { el.form.requestSubmit(); } catch(_){}
                            }
                        }
                    })();";
                    await WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch { }
            };
        }

        private async Task CheckInternetAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1500);
                bool ok = reply.Status == IPStatus.Success;
                NoInternetPanel.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
            }
            catch
            {
                NoInternetPanel.Visibility = Visibility.Visible;
            }
        }

        // ===== Button handlers =====
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentButton != null)
            {
                await NavigateAsync(_currentButton);
            }
            else if (_webViewReady && WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Reload();
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            _currentButton = null;
            _currentUrl = null;
            WebView.Visibility = Visibility.Collapsed;
            WelcomePanel.Visibility = Visibility.Visible;
            StatusText.Text = "";
            ClearCache();
        }

        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.Visibility = Keyboard.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ClearCache()
        {
            if (!_webViewReady || WebView.CoreWebView2 == null) return;
            try
            {
                _ = WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.AllProfile);
            }
            catch { }
        }

        private void KioskWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                _topmostTimer?.Stop();
                _internetTimer?.Stop();

                if (_settings.ClearCacheOnClose)
                    ClearCache();
            }
            catch { }

            try { _onClosed?.Invoke(); } catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
