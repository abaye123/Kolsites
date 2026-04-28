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
        private const int InactivityTimeoutSeconds = 45;

        private readonly AppSettings _settings;
        private readonly Action _onClosed;
        private readonly DispatcherQueueTimer _topmostTimer;
        private readonly DispatcherQueueTimer _internetTimer;
        private readonly DispatcherQueueTimer _inactivityTimer;
        private readonly DispatcherQueueTimer _siteScriptsTimer;
        private bool _webViewReady;
        private string? _currentUrl;
        private SiteButton? _currentButton;
        // ההוסט (לאחר נרמול) של האתר הנוכחי - משמש לחסימת ניווט עליון לאתרים אחרים.
        // נקבע ב-NavigateAsync לפי כתובת הכפתור, ומאופס בלחצן הבית.
        private string? _expectedHost;

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

            // Inactivity timer - חזרה אוטומטית לכפתור הצף לאחר 45 שניות ללא קלט מהמשתמש.
            // משתמשים ב-GetLastInputInfo (Win32) שמחזיר את הזמן האחרון שהמערכת קיבלה קלט (עכבר/מגע/מקלדת) -
            // עובד גם עבור אינטראקציה בתוך WebView2, מה ש-WinUI events לא יחזיקו לבד.
            _inactivityTimer = dispatcher.CreateTimer();
            _inactivityTimer.Interval = TimeSpan.FromSeconds(3);
            _inactivityTimer.Tick += (_, _) => CheckInactivity();
            _inactivityTimer.Start();

            // Site-scripts timer - מריץ את הסקריפטים של האתר הנוכחי כל שנייה.
            // זה מחקה את ה-TimerDirshu/TimerYak וכו' של התוכנה הישנה, שהסירו אלמנטים מסויימים מהדף
            // (לוגואים, פרסומות, תפריטים) באופן רציף - כי הדף עלול לחזור ולטעון אותם דינמית.
            _siteScriptsTimer = dispatcher.CreateTimer();
            _siteScriptsTimer.Interval = TimeSpan.FromSeconds(1);
            _siteScriptsTimer.Tick += async (_, _) => await RunSiteScriptsAsync();

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

            // עדכון ראשוני של אינדיקטורי הגלילה - נדרש מעט עיכוב לצורך layout
            DispatcherQueue.GetForCurrentThread().TryEnqueue(() => UpdateScrollIndicators());
        }

        private void UpdateScrollIndicators()
        {
            try
            {
                bool canScroll = SiteButtonsScroll.ScrollableWidth > 0;
                if (!canScroll)
                {
                    ScrollLeftButton.Visibility = Visibility.Collapsed;
                    ScrollRightButton.Visibility = Visibility.Collapsed;
                    ScrollLeftIndicator.Visibility = Visibility.Collapsed;
                    ScrollRightIndicator.Visibility = Visibility.Collapsed;
                    return;
                }

                // ב-RTL: HorizontalOffset == 0 משמעו "תחילת הרצועה" שזה הצד הימני ויזואלית.
                // אבל WinUI ScrollViewer לא הופך offsets ב-RTL כך שאופי הגלילה זהה - 0 = הקצה הצמוד לתחילת ה-content.
                bool atStart = SiteButtonsScroll.HorizontalOffset <= 1;
                bool atEnd = SiteButtonsScroll.HorizontalOffset >= SiteButtonsScroll.ScrollableWidth - 1;

                ScrollLeftButton.Visibility = !atEnd ? Visibility.Visible : Visibility.Collapsed;
                ScrollLeftIndicator.Visibility = !atEnd ? Visibility.Visible : Visibility.Collapsed;
                ScrollRightButton.Visibility = !atStart ? Visibility.Visible : Visibility.Collapsed;
                ScrollRightIndicator.Visibility = !atStart ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void SiteButtonsScroll_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateScrollIndicators();
        }

        private void SiteButtonsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollIndicators();
        }

        private void ScrollLeftButton_Click(object sender, RoutedEventArgs e)
        {
            // החץ השמאלי - גלילה לכיוון ה-end (offset גבוה יותר)
            double newOffset = Math.Min(
                SiteButtonsScroll.HorizontalOffset + 240,
                SiteButtonsScroll.ScrollableWidth);
            SiteButtonsScroll.ChangeView(newOffset, null, null);
        }

        private void ScrollRightButton_Click(object sender, RoutedEventArgs e)
        {
            // החץ הימני - גלילה לכיוון ה-start (offset נמוך יותר)
            double newOffset = Math.Max(SiteButtonsScroll.HorizontalOffset - 240, 0);
            SiteButtonsScroll.ChangeView(newOffset, null, null);
        }

        private FrameworkElement CreateSiteButtonControl(SiteButton btn)
        {
            bool hasCustomIcon = !string.IsNullOrWhiteSpace(btn.IconPath) && System.IO.File.Exists(btn.IconPath);

            // מצב לוגו: כפתור שקוף ללא מסגרת, רק התמונה גדולה ברוחב הכפתור
            if (hasCustomIcon)
                return CreateLogoButton(btn);

            // אחרת: כפתור צבעוני עם FontIcon + תווית
            var baseColor = TryParseColor(btn.BackgroundColor)
                ?? ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;

            var button = new Button
            {
                Width = 130,
                Height = 100,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(baseColor),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(button, btn.Name);

            // דריסת הצבעים של hover/pressed כדי שלא יהפכו ללבן.
            // ב-WinUI סגנון ברירת המחדל של Button מחליף את הרקע למשאב נושא בעת hover/pressed,
            // אז צריך לדרוס את המשאבים האלה ברמת ה-Button עצמו.
            var hoverBrush = new SolidColorBrush(Lighten(baseColor, 0.12));
            var pressedBrush = new SolidColorBrush(Darken(baseColor, 0.12));
            var whiteBrush = new SolidColorBrush(Colors.White);

            button.Resources["ButtonBackground"] = new SolidColorBrush(baseColor);
            button.Resources["ButtonBackgroundPointerOver"] = hoverBrush;
            button.Resources["ButtonBackgroundPressed"] = pressedBrush;
            button.Resources["ButtonBackgroundDisabled"] = new SolidColorBrush(Lighten(baseColor, 0.30));

            button.Resources["ButtonForeground"] = whiteBrush;
            button.Resources["ButtonForegroundPointerOver"] = whiteBrush;
            button.Resources["ButtonForegroundPressed"] = whiteBrush;
            button.Resources["ButtonForegroundDisabled"] = whiteBrush;

            button.Resources["ButtonBorderBrush"] = new SolidColorBrush(Colors.Transparent);
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Colors.Transparent);
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Colors.Transparent);

            var content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 6
            };

            // FontIcon ברירת מחדל (במצב צבע, ללא לוגו)
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

        private FrameworkElement CreateLogoButton(SiteButton btn)
        {
            var transparent = new SolidColorBrush(Colors.Transparent);

            var button = new Button
            {
                Width = 130,
                Height = 100,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Background = transparent,
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(button, btn.Name);

            // דריסת כל מצבי ה-Button כדי שגם hover/pressed ישארו שקופים ובלי מסגרת
            button.Resources["ButtonBackground"] = transparent;
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            button.Resources["ButtonBackgroundDisabled"] = transparent;

            button.Resources["ButtonBorderBrush"] = transparent;
            button.Resources["ButtonBorderBrushPointerOver"] = transparent;
            button.Resources["ButtonBorderBrushPressed"] = transparent;

            var image = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(btn.IconPath)),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            button.Content = image;
            button.Click += async (_, _) => await NavigateAsync(btn);
            return button;
        }

        private static Color? TryParseColor(string hex)
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
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch { }
            return null;
        }

        private static Color Lighten(Color c, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            byte r = (byte)Math.Round(c.R + (255 - c.R) * amount);
            byte g = (byte)Math.Round(c.G + (255 - c.G) * amount);
            byte b = (byte)Math.Round(c.B + (255 - c.B) * amount);
            return Color.FromArgb(c.A, r, g, b);
        }

        private static Color Darken(Color c, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            byte r = (byte)Math.Round(c.R * (1 - amount));
            byte g = (byte)Math.Round(c.G * (1 - amount));
            byte b = (byte)Math.Round(c.B * (1 - amount));
            return Color.FromArgb(c.A, r, g, b);
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

                    WebView.CoreWebView2.NavigationStarting += (_, e) =>
                    {
                        // חסימת ניווט עליון (ולא משאבים בתוך הדף - אלה לא עוברים כאן)
                        // לכל אתר חיצוני - כלומר שההוסט שלו שונה מההוסט של כתובת הכפתור הנוכחי.
                        // לחיצה על כפתור באתר אחר ב-strip עוברת קודם דרך NavigateAsync שמעדכן את ההוסט,
                        // לכן ניווט יזום של המשתמש מתוך הקיוסק תמיד מותר.
                        if (IsBlockedExternalNavigation(e.Uri))
                        {
                            e.Cancel = true;
                            // ביטול לבד יכול להותיר מסך לבן אם הדף כבר התחיל להתפרק
                            // (למשל בעקבות שליחת טופס או location.href). מטעינים מחדש את האתר המקורי.
                            var fallback = _currentUrl;
                            if (!string.IsNullOrEmpty(fallback))
                            {
                                DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
                                {
                                    try { WebView.CoreWebView2?.Navigate(fallback); }
                                    catch { }
                                });
                            }
                            return;
                        }

                        LoadingPanel.Visibility = Visibility.Visible;
                        WelcomePanel.Visibility = Visibility.Collapsed;
                    };
                    WebView.CoreWebView2.NavigationCompleted += (_, _) =>
                    {
                        LoadingPanel.Visibility = Visibility.Collapsed;
                    };

                    // הזרקת חוסם נטפרי בכל דף - רץ לפני כל סקריפט אחר באמצעות
                    // AddScriptToExecuteOnDocumentCreatedAsync, וכך מצליח להסיר את תגי ה-script
                    // עוד לפני שהדפדפן הספיק לבצע אותם.
                    await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        NetfreeBlockerScript);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה ב-WebView2: {ex.Message}";
            }
        }

        // סקריפט ברירת מחדל שמוזרק לכל אתר ומסיר את כרטסת נטפרי:
        // 1) MutationObserver שמסיר תגי script ואלמנטים של נטפרי ברגע שמתווספים ל-DOM
        //    (לפני שהדפדפן הספיק לבצע את הסקריפט).
        // 2) sweep ראשוני בסיום הטעינה כגיבוי במקרה שמשהו פספס.
        private const string NetfreeBlockerScript = @"
(function() {
    var scriptUrls = [
        'https://netfree.link/card/card.js',
        'https://netfree.link/api/card/data.js',
        'https://netfree.link/injection-script/go-payment.js',
        'https://netfree.link/injection-script/popup-card-init.js'
    ];
    var ids = [
        'netfree-popup-window',
        'netfree-popup-window-main',
        'netfree-popup-window-hand-pull',
        'netfree-popup-window-iframe'
    ];

    function tryRemove(node) {
        if (!node || node.nodeType !== 1) return;
        if (node.tagName === 'SCRIPT') {
            var src = node.getAttribute && node.getAttribute('src');
            if (src && scriptUrls.indexOf(src) !== -1) {
                if (node.parentNode) node.parentNode.removeChild(node);
                return;
            }
        }
        if (node.id && ids.indexOf(node.id) !== -1) {
            if (node.parentNode) node.parentNode.removeChild(node);
        }
    }

    function sweep() {
        for (var i = 0; i < ids.length; i++) {
            var el = document.getElementById(ids[i]);
            if (el && el.parentNode) el.parentNode.removeChild(el);
        }
        var scripts = document.querySelectorAll('script[src]');
        for (var j = 0; j < scripts.length; j++) {
            var src = scripts[j].getAttribute('src');
            if (src && scriptUrls.indexOf(src) !== -1 && scripts[j].parentNode) {
                scripts[j].parentNode.removeChild(scripts[j]);
            }
        }
    }

    try {
        var observer = new MutationObserver(function(mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var added = mutations[i].addedNodes;
                for (var j = 0; j < added.length; j++) tryRemove(added[j]);
            }
        });
        observer.observe(document, { childList: true, subtree: true });
    } catch (e) { }

    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        sweep();
    } else {
        document.addEventListener('DOMContentLoaded', sweep);
    }
})();
";

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
            _expectedHost = TryGetHost(btn.Url);

            WelcomePanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            StatusText.Text = btn.Name;

            try
            {
                WebView.CoreWebView2.Navigate(_currentUrl);

                // הפעלת טיימר הסקריפטים של האתר הזה (אם יש סקריפטים מופעלים)
                bool hasEnabledScripts = btn.Scripts != null && btn.Scripts.Any(s => s.Enabled);
                if (hasEnabledScripts) _siteScriptsTimer.Start();
                else _siteScriptsTimer.Stop();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"נכשל בטעינה: {ex.Message}";
            }
        }

        private async Task RunSiteScriptsAsync()
        {
            if (!_webViewReady || WebView.CoreWebView2 == null) return;
            if (_currentButton?.Scripts == null) return;

            var enabled = _currentButton.Scripts.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Code)).ToList();
            if (enabled.Count == 0) return;

            // איגוד כל הסקריפטים לקריאה אחת + try/catch כדי שכישלון של אחד לא יקרוס את האחרים
            var combined = string.Join("\n", enabled.Select(s =>
                $"try {{ {s.Code} }} catch(e) {{ /* {s.Name} */ }}"));

            try { await WebView.CoreWebView2.ExecuteScriptAsync(combined); }
            catch { }
        }

        private static string AppendCacheBuster(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            var separator = url.Contains('?') ? "&" : "?";
            return $"{url}{separator}_t={DateTime.Now.Ticks}";
        }

        // סכמות שמפעילות אפליקציה חיצונית (לקוח דוא"ל, חייגן טלפון וכד') - חסומות תמיד בקיוסק.
        private static readonly string[] BlockedSchemes =
            { "mailto", "tel", "callto", "sms", "skype", "whatsapp" };

        private bool IsBlockedExternalNavigation(string targetUri)
        {
            if (string.IsNullOrWhiteSpace(targetUri)) return false;

            // חסימה קבועה של סכמות שפותחות אפליקציות חיצוניות (mailto, tel וכו'),
            // ללא תלות ב-_expectedHost - גם בטעינה הראשונית.
            if (Uri.TryCreate(targetUri, UriKind.Absolute, out var uri))
            {
                foreach (var s in BlockedSchemes)
                    if (string.Equals(uri.Scheme, s, StringComparison.OrdinalIgnoreCase))
                        return true;
            }

            // לפני שנקבע אתר נוכחי - מאפשרים הכל (טעינה ראשונית של welcome וכד').
            if (string.IsNullOrEmpty(_expectedHost)) return false;

            var targetHost = TryGetHost(targetUri);
            // סכמות לא-http (about:blank, data:, javascript:) - לא חוסמים, לא ניווט לאתר חיצוני.
            if (targetHost == null) return false;

            // התאמה: אותו הוסט בדיוק או תת-דומיין שלו (sub.example.com מקובל אם הכפתור הוא example.com).
            if (targetHost == _expectedHost) return false;
            if (targetHost.EndsWith("." + _expectedHost, StringComparison.Ordinal)) return false;
            return true;
        }

        private static string? TryGetHost(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            return host;
        }

        private void HookKeyboard()
        {
            if (!_settings.ShowVirtualKeyboard)
            {
                ShowKeyboardButton.Visibility = Visibility.Collapsed;
                KeyboardContainer.Visibility = Visibility.Collapsed;
                return;
            }

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

        private void CheckInactivity()
        {
            try
            {
                var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (!GetLastInputInfo(ref lii)) return;

                uint idleMs = unchecked((uint)Environment.TickCount - lii.dwTime);
                if (idleMs >= InactivityTimeoutSeconds * 1000)
                {
                    // עברו 45 שניות בלי קלט - חזרה לכפתור הצף
                    _inactivityTimer.Stop();
                    this.Close();
                }
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private async Task CheckInternetAsync()
        {
            bool online;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1500);
                online = reply.Status == IPStatus.Success;
            }
            catch
            {
                online = false;
            }

            // אם המשתמש השבית את המסך המלא - מציגים רק badge קטן בסרגל
            if (_settings.ShowNoInternetOverlay)
            {
                NoInternetPanel.Visibility = online ? Visibility.Collapsed : Visibility.Visible;
                NoInternetBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoInternetPanel.Visibility = Visibility.Collapsed;
                NoInternetBadge.Visibility = online ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        // ===== Button handlers =====
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // אם אנחנו במסך הברוכים-הבאים אין מה לרענן.
            // קריאה ל-Reload() על WebView ריק תפעיל NavigationStarting שמסתיר את ה-WelcomePanel
            // ומשאיר מסך לבן.
            if (_currentButton == null)
                return;

            await NavigateAsync(_currentButton);
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            _currentButton = null;
            _currentUrl = null;
            _expectedHost = null;
            _siteScriptsTimer.Stop();
            WebView.Visibility = Visibility.Collapsed;
            WelcomePanel.Visibility = Visibility.Visible;
            StatusText.Text = "";
            ClearCache();
        }

        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            // טוגל בין הכפתור הגדול בתחתית ובין המקלדת עצמה
            bool keyboardShown = KeyboardContainer.Visibility == Visibility.Visible;
            if (keyboardShown)
            {
                KeyboardContainer.Visibility = Visibility.Collapsed;
                ShowKeyboardButton.Visibility = Visibility.Visible;
            }
            else
            {
                KeyboardContainer.Visibility = Visibility.Visible;
                ShowKeyboardButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowAboutDialogAsync();
        }

        private async System.Threading.Tasks.Task ShowAboutDialogAsync()
        {
            // בניית תוכן ה-About ב-code (זהה ל-About שבהגדרות, ללא לינקי גיטהאב)
            var version = "1.0.0";
            try
            {
                var v = typeof(KioskWindow).Assembly.GetName().Version;
                if (v != null) version = $"{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { }

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            // לוגו התוכנה - לחיצה כפולה מהירה עליו תפתח את ההגדרות (אם הוגדרה סיסמה)
            var appLogo = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                    new Uri("ms-appx:///Assets/AppIcon.png")),
                Width = 56,
                Height = 56,
                VerticalAlignment = VerticalAlignment.Top
            };
            headerStack.Children.Add(appLogo);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Kolsites",
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = $"גרסה {version}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "תוכנת קיוסק לעמדות מסך מגע ציבוריות",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            headerStack.Children.Add(titleStack);

            // קרדיט עם הלוגו של abaye בצד
            var creditTextStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4
            };
            creditTextStack.Children.Add(new TextBlock
            {
                Text = "פותח על ידי abaye",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            creditTextStack.Children.Add(new TextBlock
            {
                Text = "קוד פתוח · תרומות ודיווח על באגים מתקבלים בברכה",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            var creditStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            var abayeLogo = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                    new Uri("ms-appx:///Assets/abaye.png")),
                Width = 56,
                Height = 56,
                VerticalAlignment = VerticalAlignment.Center
            };
            creditStack.Children.Add(abayeLogo);
            creditStack.Children.Add(creditTextStack);

            // אייקון נר נשמה - להבה צהובה מעל גוף נר בצבע טקסט.
            // שני Path נפרדים בתוך Grid 24x24, עטוף ב-Viewbox כדי לקבע לגודל 22x22.
            var flamePath = new Microsoft.UI.Xaml.Shapes.Path
            {
                Fill = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
                Data = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry),
                    "M12,2C9.5,5 9,7 9,9C9,10.5 10.3,11 12,11C13.7,11 15,10.5 15,9C15,7 14.5,5 12,2Z")
            };
            var candleBodyPath = new Microsoft.UI.Xaml.Shapes.Path
            {
                Fill = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                Data = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry),
                    "M9,12L9,22L15,22L15,12Z")
            };
            var candleInner = new Grid { Width = 24, Height = 24 };
            candleInner.Children.Add(candleBodyPath);
            candleInner.Children.Add(flamePath);
            var candleHost = new Viewbox { Width = 22, Height = 22, Child = candleInner };

            var dedicationStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dedicationStack.Children.Add(candleHost);
            dedicationStack.Children.Add(new TextBlock
            {
                Text = "התוכנה לע\"נ ר' משה ב\"ר אברהם זצ\"ל",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });

            var dedicationBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Child = dedicationStack
            };

            var content = new StackPanel { Spacing = 16, Width = 460 };
            content.Children.Add(headerStack);
            content.Children.Add(creditStack);
            content.Children.Add(dedicationBorder);

            var dialog = new ContentDialog
            {
                Title = "אודות",
                Content = content,
                CloseButtonText = "סגור",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            // קיצור גישה להגדרות: לחיצה כפולה מהירה על לוגו התוכנה פותחת בקשת סיסמה.
            // הקיצור פעיל רק אם המשתמש הגדיר סיסמה בהגדרות (KioskSettingsPassword); אחרת
            // הלחיצה הכפולה מתעלמת לחלוטין כדי שלא תהיה דרך לעקוף את הקיוסק במחשבים ציבוריים.
            appLogo.DoubleTapped += async (_, _) =>
            {
                if (string.IsNullOrEmpty(_settings.KioskSettingsPassword)) return;
                dialog.Hide();
                if (await PromptForKioskSettingsPasswordAsync())
                {
                    try
                    {
                        // הפעלת ההגדרות כתהליך נפרד (כמו הפעלה רגילה דרך הקיצור) -
                        // יש מיוטקס נפרד ל-Settings ב-Program.cs כך שזה לא מתנגש.
                        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exe))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = exe!,
                                Arguments = "--settings",
                                UseShellExecute = true
                            });
                        }
                        // סגירת חלון הקיוסק כדי שחלון ההגדרות יוצג מקדימה
                        // (הקיוסק תמיד-למעלה והיה מסתיר את ההגדרות).
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = $"שגיאה בפתיחת ההגדרות: {ex.Message}";
                    }
                }
            };

            await dialog.ShowAsync();
        }

        private async Task<bool> PromptForKioskSettingsPasswordAsync()
        {
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "סיסמה",
                Width = 260,
                PasswordRevealMode = PasswordRevealMode.Peek
            };
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(new TextBlock
            {
                Text = "הזן סיסמה לפתיחת ההגדרות:",
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(passwordBox);

            var dialog = new ContentDialog
            {
                Title = "פתיחת הגדרות",
                Content = content,
                PrimaryButtonText = "אישור",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            // פוקוס אוטומטי על שדה הסיסמה כשהדיאלוג נפתח
            dialog.Opened += (_, _) => passwordBox.Focus(FocusState.Programmatic);

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return false;

            if (PasswordHasher.Verify(passwordBox.Password, _settings.KioskSettingsPassword))
                return true;

            // סיסמה שגויה - הודעה למשתמש (לא חושפים אם המשתמש קרוב לנכון)
            var err = new ContentDialog
            {
                Title = "סיסמה שגויה",
                Content = "הסיסמה שהוזנה אינה נכונה.",
                CloseButtonText = "סגור",
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };
            await err.ShowAsync();
            return false;
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
                _inactivityTimer?.Stop();
                _siteScriptsTimer?.Stop();

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
