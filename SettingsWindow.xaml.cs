using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace Kolsites
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly ObservableCollection<SiteButton> _buttons;
        private MicaController? _micaController;
        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _backdropConfiguration;

        // ה-hash המקורי שנטען מההגדרות. אם המשתמש לא מקליד סיסמה חדשה - שומרים אותו כמו שהוא.
        private string _originalKioskPasswordHash = "";
        // נדלק כש"הסר סיסמה" נלחץ - בשמירה נכתוב מחרוזת ריקה.
        private bool _clearKioskPassword;

        private readonly List<BlockedTimeRange> _blockedRanges = new();

        // מצב המשימה המתוזמנת בעת פתיחת חלון ההגדרות. בעת שמירה משווים מול ה-Toggle ופועלים רק אם השתנה.
        private bool _watchdogTaskInitiallyInstalled;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _buttons = new ObservableCollection<SiteButton>(_settings.Buttons);

            Title = "הגדרות - Kolsites";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            SetupTitleBar();
            TrySetSystemBackdrop();
            SetupWindowSize();

            // אכלוס הקומבו-בוקסים
            SelectComboByTag(ThemeCombo, _settings.Theme.ToString());
            SelectComboByTag(PositionCombo, _settings.ButtonPosition.ToString());
            SelectComboByTag(KeyboardLayoutCombo, _settings.DefaultKeyboardLayout);
            SelectComboByTag(KeyboardScaleCombo, KeyboardScaleToTag(_settings.KeyboardScale));

            ButtonSizeBox.Value = _settings.ButtonSize;
            ButtonMarginBox.Value = _settings.ButtonMargin;
            ButtonLabelBox.Text = _settings.ButtonLabel;

            ShowKeyboardToggle.IsOn = _settings.ShowVirtualKeyboard;
            AutoShowKeyboardOnFocusToggle.IsOn = _settings.AutoShowKeyboardOnFocus;
            BlockContextMenuToggle.IsOn = _settings.BlockContextMenu;
            DisablePinchZoomToggle.IsOn = _settings.DisablePinchZoom;
            ClearCacheOnCloseToggle.IsOn = _settings.ClearCacheOnClose;
            ShowNoInternetOverlayToggle.IsOn = _settings.ShowNoInternetOverlay;

            // לא טוענים את ה-hash לתוך הטופס - משאירים את השדה ריק ומציגים סטטוס.
            _originalKioskPasswordHash = _settings.KioskSettingsPassword ?? "";
            _clearKioskPassword = false;
            UpdateKioskPasswordStatus();

            LoadBlockedRanges();

            ButtonsList.ItemsSource = _buttons;

            ApplyCurrentTheme();
            UpdateProcessStatus();
            InitializeWatchdogToggle();
            PopulateAbout();

            Closed += (_, _) =>
            {
                _micaController?.Dispose();
                _acrylicController?.Dispose();
            };
        }

        #region Window setup

        private AppWindow? GetAppWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            return AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hWnd));
        }

        private void SetupTitleBar()
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                    appWindow.SetIcon(iconPath);
            }
            catch { }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var tb = appWindow.TitleBar;
                tb.ExtendsContentIntoTitleBar = true;
                tb.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                SetTitleBar(AppTitleBar);
            }
        }

        private void TrySetSystemBackdrop()
        {
            // מבנה זהה ל-TapLingo: Mica עדיף, נופל ל-Acrylic על Win10
            if (MicaController.IsSupported())
            {
                _backdropConfiguration = new SystemBackdropConfiguration
                {
                    Theme = SystemBackdropTheme.Default,
                    IsInputActive = true
                };
                _micaController = new MicaController();
                _micaController.AddSystemBackdropTarget(
                    WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(this));
                _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                _backdropConfiguration = new SystemBackdropConfiguration
                {
                    Theme = SystemBackdropTheme.Default,
                    IsInputActive = true
                };
                _acrylicController = new DesktopAcrylicController();
                _acrylicController.AddSystemBackdropTarget(
                    WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(this));
                _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);
            }
        }

        private void SetupWindowSize()
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            appWindow.Resize(new SizeInt32(950, 980));

            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (area != null)
            {
                appWindow.Move(new PointInt32(
                    area.WorkArea.X + (area.WorkArea.Width - appWindow.Size.Width) / 2,
                    area.WorkArea.Y + (area.WorkArea.Height - appWindow.Size.Height) / 2));
            }

            if (appWindow.Presenter is OverlappedPresenter op)
            {
                op.IsMaximizable = true;
                op.IsMinimizable = true;
            }
        }

        #endregion

        #region Theme & helpers

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyCurrentTheme();
        }

        private AppTheme GetSelectedTheme()
        {
            if (ThemeCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag &&
                Enum.TryParse<AppTheme>(tag, out var theme))
                return theme;
            return AppTheme.System;
        }

        private void ApplyCurrentTheme()
        {
            ThemeHelper.Apply(this, GetSelectedTheme(), _backdropConfiguration);
        }

        private static void SelectComboByTag(ComboBox combo, string? tag)
        {
            if (tag == null) { combo.SelectedIndex = 0; return; }
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem c && c.Tag is string s &&
                    string.Equals(s, tag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static string? GetTag(ComboBox combo) =>
            (combo.SelectedItem as ComboBoxItem)?.Tag as string;

        // ממיר ערך KeyboardScale ל-Tag הקרוב ביותר ב-ComboBox (1.0 / 1.3 / 1.6 / 2.0).
        private static string KeyboardScaleToTag(double scale)
        {
            string[] options = { "1.0", "1.3", "1.6", "2.0" };
            string best = "1.0";
            double minDiff = double.MaxValue;
            foreach (var o in options)
            {
                if (!double.TryParse(o, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v)) continue;
                double diff = Math.Abs(v - scale);
                if (diff < minDiff) { minDiff = diff; best = o; }
            }
            return best;
        }

        private static double ParseKeyboardScale(string? tag)
        {
            if (double.TryParse(tag, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0)
                return v;
            return 1.0;
        }

        private void PopulateAbout()
        {
            try
            {
                var v = typeof(SettingsWindow).Assembly.GetName().Version;
                AboutVersionText.Text = v != null
                    ? $"גרסה {v.Major}.{v.Minor}.{v.Build}"
                    : "גרסה 1.3.0";
            }
            catch
            {
                AboutVersionText.Text = "גרסה 1.3.0";
            }

            SettingsPathText.Text = $"קובץ הגדרות: {SettingsManager.GetSettingsPath()}";
        }

        #endregion

        #region Update check

        // נשמר בין 'בדוק עדכונים' ל-'הורד והתקן' כדי שלא נצטרך לקרוא שוב ל-API.
        private UpdateChecker.ReleaseInfo? _pendingUpdate;

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            _pendingUpdate = null;
            UpdateStatusText.Text = "בודק עדכונים...";

            try
            {
                var release = await UpdateChecker.GetLatestReleaseAsync();
                var current = UpdateChecker.GetCurrentVersion();

                if (release.ParsedVersion == null)
                {
                    UpdateStatusText.Text = $"לא ניתן לפרש את גרסת ה-Release ({release.TagName}).";
                    return;
                }

                if (string.IsNullOrEmpty(release.AssetUrl))
                {
                    UpdateStatusText.Text =
                        $"גרסה {release.ParsedVersion} זמינה, אבל לא נמצא קובץ Setup ב-Release.";
                    return;
                }

                if (UpdateChecker.IsNewer(current, release.ParsedVersion))
                {
                    var sizeMb = release.AssetSize / (1024.0 * 1024.0);
                    UpdateStatusText.Text =
                        $"זמינה גרסה חדשה: {release.ParsedVersion} (הנוכחית: {current}). " +
                        $"קובץ ההתקנה: {release.AssetName} ({sizeMb:0.0} MB).";
                    InstallUpdateButton.Visibility = Visibility.Visible;
                    _pendingUpdate = release;
                }
                else
                {
                    UpdateStatusText.Text = $"אתה משתמש בגרסה העדכנית ביותר ({current}).";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"שגיאה בבדיקת עדכונים: {ex.Message}";
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            // אישור לפני התקנה - התוכנה תיסגר במהלכה
            var confirm = new ContentDialog
            {
                Title = "התקנת עדכון",
                Content =
                    $"גרסה {_pendingUpdate.ParsedVersion} תורד ותותקן. " +
                    "התוכנה תיסגר אוטומטית לפני ההתקנה. להמשיך?",
                PrimaryButtonText = "המשך",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            CheckUpdatesButton.IsEnabled = false;
            InstallUpdateButton.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;
            UpdateStatusText.Text = "מוריד את קובץ ההתקנה...";

            try
            {
                var progress = new Progress<double>(p =>
                {
                    UpdateProgressBar.Value = p;
                    UpdateStatusText.Text = $"מוריד... {p * 100:0}%";
                });

                var installerPath = await UpdateChecker.DownloadAssetAsync(
                    _pendingUpdate.AssetUrl, progress);

                UpdateStatusText.Text = "מפעיל את ההתקנה...";
                UpdateChecker.RunInstaller(installerPath, SilentUpdateToggle.IsOn);

                // יוצאים מהתוכנה - המתקין צריך להיות חופשי להחליף את הקבצים.
                // ה-Watchdog לא ירים אותנו מחדש: המתקין יעצור אותו וירים בסיום (Inno Setup
                // משתמש ב-Restart Manager). אם זה לא מצליח - המשתמש יפעיל מחדש בעצמו.
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"שגיאה בהתקנת העדכון: {ex.Message}";
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                CheckUpdatesButton.IsEnabled = true;
                InstallUpdateButton.IsEnabled = true;
            }
        }

        #endregion

        #region Buttons list management

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newBtn = new SiteButton
            {
                Name = "כפתור חדש",
                Url = "https://",
                Label = "חדש",
                BackgroundColor = "#0078D4",
                Enabled = true
            };

            var dialog = CreateEditDialog(newBtn);
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _buttons.Add(newBtn);
                ButtonsList.SelectedItem = newBtn;
            }
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonsList.SelectedItem is not SiteButton selected) return;

            // נערוך עותק - שאם המשתמש מבטל לא נשנה את המקור
            var copy = new SiteButton
            {
                Id = selected.Id,
                Name = selected.Name,
                Url = selected.Url,
                IconPath = selected.IconPath,
                Label = selected.Label,
                BackgroundColor = selected.BackgroundColor,
                Enabled = selected.Enabled
            };

            var dialog = CreateEditDialog(copy);
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                int idx = _buttons.IndexOf(selected);
                if (idx >= 0)
                {
                    _buttons[idx] = copy;
                    ButtonsList.SelectedIndex = idx;
                }
            }
        }

        private async void ScriptsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonsList.SelectedItem is not SiteButton selected) return;
            await ShowScriptsDialogAsync(selected);
        }

        private async System.Threading.Tasks.Task ShowScriptsDialogAsync(SiteButton btn)
        {
            // עותק עבודה - שינויים יוחלו רק בלחיצה על אישור
            var working = btn.Scripts?.Select(s => new SiteScript
            {
                Name = s.Name,
                Code = s.Code,
                Enabled = s.Enabled
            }).ToList() ?? new List<SiteScript>();

            // WinUI 3 לא מאפשר nested ContentDialog. במקום לפתוח דיאלוג עריכה נפרד,
            // אנחנו עושים החלפת תוכן בתוך אותו דיאלוג: מצב רשימה ↔ מצב עריכה.
            var contentHost = new Grid { Width = 560 };
            var dialog = new ContentDialog
            {
                Title = "ניהול סקריפטים",
                Content = new ScrollViewer { Content = contentHost, MaxHeight = 600 },
                PrimaryButtonText = "אישור",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            SiteScript? selected = null;
            // מצב נוכחי: null = רשימה, אחרת = עריכת הסקריפט הזה (יכול להיות פריט חדש או קיים)
            SiteScript? editing = null;
            bool isNewScript = false;

            void ShowList()
            {
                editing = null;
                contentHost.Children.Clear();
                contentHost.Children.Add(BuildListView());
                dialog.PrimaryButtonText = "אישור";
                dialog.CloseButtonText = "ביטול";
            }

            void ShowEdit(SiteScript script, bool isNew)
            {
                editing = script;
                isNewScript = isNew;
                contentHost.Children.Clear();
                contentHost.Children.Add(BuildEditView(script));
                // במצב עריכה אנחנו מסתירים את כפתורי האישור/ביטול הראשיים -
                // יש לנו כפתורי שמירה/ביטול נפרדים בתוך התצוגה
                dialog.PrimaryButtonText = "";
                dialog.CloseButtonText = "";
            }

            FrameworkElement BuildListView()
            {
                var rowsPanel = new StackPanel { Spacing = 4 };

                FrameworkElement BuildRow(SiteScript script)
                {
                    var grid = new Grid { Padding = new Thickness(6, 6, 6, 6), ColumnSpacing = 10 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var cb = new CheckBox { IsChecked = script.Enabled, VerticalAlignment = VerticalAlignment.Center };
                    cb.Checked += (_, _) => script.Enabled = true;
                    cb.Unchecked += (_, _) => script.Enabled = false;
                    Grid.SetColumn(cb, 0);
                    grid.Children.Add(cb);

                    var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = script.Name,
                        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                    });
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = (script.Code ?? "").Replace('\n', ' ').Replace('\r', ' '),
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    });
                    Grid.SetColumn(textPanel, 1);
                    grid.Children.Add(textPanel);

                    var container = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(2),
                        Background = script == selected
                            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorTertiaryBrush"]
                            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
                        Child = grid,
                        Tag = script
                    };

                    container.Tapped += (_, _) =>
                    {
                        selected = script;
                        foreach (var c in rowsPanel.Children)
                        {
                            if (c is Border b)
                                b.Background = b.Tag == script
                                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorTertiaryBrush"]
                                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"];
                        }
                    };
                    return container;
                }

                if (working.Count == 0)
                {
                    rowsPanel.Children.Add(new TextBlock
                    {
                        Text = "אין סקריפטים. לחץ 'הוסף סקריפט' כדי להוסיף.",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                        Margin = new Thickness(12)
                    });
                }
                else
                {
                    foreach (var s in working) rowsPanel.Children.Add(BuildRow(s));
                }

                var rowsBorder = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6),
                    Child = new ScrollViewer
                    {
                        MinHeight = 220,
                        MaxHeight = 320,
                        Content = rowsPanel,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    }
                };

                var addBtn = new Button { Content = "הוסף סקריפט" };
                var editBtn = new Button { Content = "ערוך" };
                var removeBtn = new Button { Content = "הסר" };

                addBtn.Click += (_, _) =>
                {
                    var s = new SiteScript { Name = "סקריפט חדש", Code = "", Enabled = true };
                    ShowEdit(s, isNew: true);
                };

                editBtn.Click += (_, _) =>
                {
                    if (selected == null) return;
                    // מעבר למצב עריכה - מקבל עותק של ה-Script הקיים, רק מחילים אם המשתמש שומר
                    var copy = new SiteScript { Name = selected.Name, Code = selected.Code, Enabled = selected.Enabled };
                    ShowEdit(copy, isNew: false);
                };

                removeBtn.Click += (_, _) =>
                {
                    if (selected == null) return;
                    working.Remove(selected);
                    selected = null;
                    ShowList();
                };

                var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                actionsRow.Children.Add(addBtn);
                actionsRow.Children.Add(editBtn);
                actionsRow.Children.Add(removeBtn);

                var infoBar = new InfoBar
                {
                    IsOpen = true,
                    IsClosable = false,
                    Severity = InfoBarSeverity.Informational,
                    Title = "סקריפטים פר-אתר",
                    Message = "סקריפטים אלו רצים כל שנייה בעת ניווט לאתר. שימושי להסרת לוגואים, פרסומות, או אלמנטים מפריעים. הקוד הוא JavaScript רגיל בהקשר של הדף."
                };

                var content = new StackPanel { Spacing = 12 };
                content.Children.Add(infoBar);
                content.Children.Add(new TextBlock
                {
                    Text = $"כפתור: {btn.Name}",
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                });
                content.Children.Add(rowsBorder);
                content.Children.Add(actionsRow);
                return content;
            }

            FrameworkElement BuildEditView(SiteScript script)
            {
                var nameBox = new TextBox { Header = "שם", Text = script.Name, PlaceholderText = "תיאור קצר של הסקריפט" };
                var codeBox = new TextBox
                {
                    Header = "קוד JavaScript",
                    Text = script.Code,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinHeight = 220,
                    MaxHeight = 320,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 13,
                    PlaceholderText = "לדוגמה: document.getElementById('FooterLogo')?.remove();"
                };
                var enabledToggle = new ToggleSwitch
                {
                    Header = "מופעל",
                    IsOn = script.Enabled,
                    OnContent = "כן",
                    OffContent = "לא"
                };

                var saveBtn = new Button
                {
                    Content = isNewScript ? "הוסף" : "שמור",
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                    MinWidth = 110
                };
                var cancelBtn = new Button { Content = "ביטול", MinWidth = 110 };

                saveBtn.Click += (_, _) =>
                {
                    script.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "סקריפט" : nameBox.Text.Trim();
                    script.Code = codeBox.Text ?? "";
                    script.Enabled = enabledToggle.IsOn;

                    if (isNewScript)
                    {
                        working.Add(script);
                        selected = script;
                    }
                    else if (selected != null)
                    {
                        // עדכון הסקריפט הקיים בעותק העבודה
                        int i = working.IndexOf(selected);
                        if (i >= 0)
                        {
                            working[i] = script;
                            selected = script;
                        }
                    }
                    ShowList();
                };

                cancelBtn.Click += (_, _) => ShowList();

                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                actions.Children.Add(saveBtn);
                actions.Children.Add(cancelBtn);

                var content = new StackPanel { Spacing = 12 };
                content.Children.Add(new TextBlock
                {
                    Text = isNewScript ? "סקריפט חדש" : "עריכת סקריפט",
                    Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
                });
                content.Children.Add(nameBox);
                content.Children.Add(codeBox);
                content.Children.Add(enabledToggle);
                content.Children.Add(actions);
                return content;
            }

            // אם המשתמש לוחץ על "ביטול" של הדיאלוג בזמן עריכה - חוזרים לרשימה במקום לסגור
            dialog.Closing += (_, args) =>
            {
                if (editing != null)
                {
                    args.Cancel = true;
                    ShowList();
                }
            };

            ShowList();

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                btn.Scripts = working.ToList();
            }
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonsList.SelectedItem is not SiteButton selected) return;

            var confirm = new ContentDialog
            {
                Title = "הסרת כפתור",
                Content = $"האם להסיר את הכפתור \"{selected.Name}\"?",
                PrimaryButtonText = "הסר",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                _buttons.Remove(selected);
        }

        private async void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ContentDialog
            {
                Title = "שחזור ברירת מחדל",
                Content = "פעולה זו תחליף את כל הכפתורים הנוכחיים ברשימת ברירת המחדל. להמשיך?",
                PrimaryButtonText = "שחזר",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                _buttons.Clear();
                foreach (var b in SettingsManager.CreateDefaults().Buttons)
                    _buttons.Add(b);
            }
        }

        private ContentDialog CreateEditDialog(SiteButton btn)
        {
            var nameBox = new TextBox { Header = "שם", Text = btn.Name, PlaceholderText = "שם הכפתור (לניהול)" };
            var urlBox = new TextBox { Header = "כתובת אינטרנט (URL)", Text = btn.Url, PlaceholderText = "https://example.com" };
            var labelBox = new TextBox { Header = "תווית על הכפתור", Text = btn.Label, PlaceholderText = "טקסט שיוצג מתחת לאייקון", AcceptsReturn = true };
            var iconPathBox = new TextBox { Header = "נתיב לאייקון מותאם (אופציונלי)", Text = btn.IconPath, PlaceholderText = "השאר ריק לאייקון ברירת מחדל" };
            var colorBox = new TextBox { Header = "צבע רקע HEX", Text = btn.BackgroundColor, PlaceholderText = "#0078D4" };
            var enabledToggle = new ToggleSwitch { Header = "מופעל", IsOn = btn.Enabled, OnContent = "כן", OffContent = "לא" };

            var pickButton = new Button { Content = "בחר אייקון..." };
            pickButton.Click += async (_, _) =>
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                var hWnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hWnd);
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".ico");
                var file = await picker.PickSingleFileAsync();
                if (file != null) iconPathBox.Text = file.Path;
            };

            var iconRow = new Grid { ColumnSpacing = 8 };
            iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(iconPathBox, 0);
            Grid.SetColumn(pickButton, 1);
            pickButton.VerticalAlignment = VerticalAlignment.Bottom;
            iconRow.Children.Add(iconPathBox);
            iconRow.Children.Add(pickButton);

            var panel = new StackPanel { Spacing = 12, Width = 460 };
            panel.Children.Add(nameBox);
            panel.Children.Add(urlBox);
            panel.Children.Add(labelBox);
            panel.Children.Add(iconRow);
            panel.Children.Add(colorBox);
            panel.Children.Add(enabledToggle);

            var dialog = new ContentDialog
            {
                Title = "עריכת כפתור",
                PrimaryButtonText = "אישור",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            dialog.PrimaryButtonClick += (sender, args) =>
            {
                btn.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "כפתור" : nameBox.Text.Trim();
                btn.Url = urlBox.Text?.Trim() ?? "";
                btn.Label = labelBox.Text ?? "";
                btn.IconPath = iconPathBox.Text?.Trim() ?? "";
                btn.BackgroundColor = colorBox.Text?.Trim() ?? "";
                btn.Enabled = enabledToggle.IsOn;

                if (string.IsNullOrEmpty(btn.Url) || (!btn.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                                       !btn.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                                                       !btn.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)))
                {
                    args.Cancel = true;
                    _ = ShowErrorAsync("כתובת חייבת להתחיל ב-http:// או https://");
                }
            };

            return dialog;
        }

        private async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            var dlg = new ContentDialog
            {
                Title = "שגיאה",
                Content = message,
                CloseButtonText = "אישור",
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };
            await dlg.ShowAsync();
        }

        #endregion

        #region Process control

        private void UpdateProcessStatus()
        {
            // בודק אם מופע Kolsites Kiosk רץ באמצעות בדיקת ה-Mutex
            const string KioskMutexName = "Kolsites_Kiosk_{8F3A2C7E-4B5D-4F1A-9C8E-3D2B1A5F6E7D}";
            bool running;
            try
            {
                using var m = new Mutex(false, KioskMutexName, out _);
                running = !m.WaitOne(TimeSpan.Zero);
                if (!running) m.ReleaseMutex();
            }
            catch (AbandonedMutexException)
            {
                running = false;
            }
            catch
            {
                running = false;
            }

            ProcessStatusText.Text = running
                ? "סטטוס: Kolsites פועל כרגע"
                : "סטטוס: Kolsites אינו פועל כרגע";
        }

        private void StartKioskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = exe!,
                    UseShellExecute = true
                });

                // ניסיון להציג עדכון סטטוס לאחר זמן קצר
                _ = System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
                {
                    DispatcherQueue.TryEnqueue(UpdateProcessStatus);
                });
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("נכשל בהפעלה: " + ex.Message);
            }
        }

        private void StopKioskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // סימון כיבוי ידני - מונע מה-Watchdog להפעיל מחדש עד שהמשתמש יטען את Kolsites בעצמו.
                ManualStopFlag.Set();

                int killed = 0;
                int currentPid = Process.GetCurrentProcess().Id;
                var name = Path.GetFileNameWithoutExtension(
                    Process.GetCurrentProcess().MainModule?.FileName ?? "Kolsites");

                foreach (var p in Process.GetProcessesByName(name))
                {
                    if (p.Id == currentPid) continue;
                    try
                    {
                        p.Kill(entireProcessTree: false);
                        killed++;
                    }
                    catch { }
                }

                ProcessStatusText.Text = killed > 0
                    ? $"נסגרו {killed} מופעי Kolsites"
                    : "לא נמצאו מופעי Kolsites פעילים";

                _ = System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
                {
                    DispatcherQueue.TryEnqueue(UpdateProcessStatus);
                });
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("נכשל בסגירה: " + ex.Message);
            }
        }

        private void InitializeWatchdogToggle()
        {
            _watchdogTaskInitiallyInstalled = TaskSchedulerHelper.IsTaskInstalled();
            WatchdogToggle.IsOn = _watchdogTaskInitiallyInstalled;
            UpdateWatchdogStatusText();
        }

        private void WatchdogToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateWatchdogStatusText();
        }

        private void UpdateWatchdogStatusText()
        {
            if (!_watchdogTaskInitiallyInstalled && !WatchdogToggle.IsOn)
            {
                WatchdogStatusText.Text = "אין כרגע משימה מתוזמנת.";
            }
            else if (_watchdogTaskInitiallyInstalled && WatchdogToggle.IsOn)
            {
                WatchdogStatusText.Text = $"המשימה '{TaskSchedulerHelper.TaskName}' קיימת. בעת השמירה תעודכן ללוח זמנים שעתי.";
            }
            else if (!_watchdogTaskInitiallyInstalled && WatchdogToggle.IsOn)
            {
                WatchdogStatusText.Text = $"בעת השמירה תיווצר משימה '{TaskSchedulerHelper.TaskName}' שתרוץ אחת לשעה.";
            }
            else
            {
                WatchdogStatusText.Text = $"בעת השמירה תוסר המשימה '{TaskSchedulerHelper.TaskName}'.";
            }
        }

        /// <summary>
        /// מיישם את שינוי מצב המשימה המתוזמנת בהתאם ל-Toggle. מחזיר false אם הייתה שגיאה
        /// (וממלא error). מעדכן את _watchdogTaskInitiallyInstalled לאחר הצלחה.
        /// </summary>
        private bool ApplyWatchdogTaskChange(out string error)
        {
            error = "";
            bool desired = WatchdogToggle.IsOn;
            if (desired == _watchdogTaskInitiallyInstalled) return true;

            if (desired)
            {
                var (ok, err) = TaskSchedulerHelper.InstallHourly();
                if (!ok)
                {
                    error = "לא ניתן ליצור את משימת ה-Watchdog במתזמן המשימות.\n\n" +
                            "נסה לפתוח את חלון ההגדרות כמנהל (לחיצה ימנית על קיצור ההגדרות > 'הפעל כמנהל').\n\n" +
                            "פרטי שגיאה:\n" + err;
                    return false;
                }
            }
            else
            {
                var (ok, err) = TaskSchedulerHelper.Uninstall();
                if (!ok)
                {
                    error = "לא ניתן להסיר את משימת ה-Watchdog ממתזמן המשימות.\n\n" +
                            "נסה לפתוח את חלון ההגדרות כמנהל (לחיצה ימנית על קיצור ההגדרות > 'הפעל כמנהל').\n\n" +
                            "פרטי שגיאה:\n" + err;
                    return false;
                }
            }

            _watchdogTaskInitiallyInstalled = desired;
            UpdateWatchdogStatusText();
            return true;
        }

        #endregion

        #region Save / Cancel

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.Theme = GetSelectedTheme();

            if (Enum.TryParse<FloatingButtonPosition>(GetTag(PositionCombo), out var pos))
                _settings.ButtonPosition = pos;

            _settings.ButtonSize = (int)Math.Round(ButtonSizeBox.Value);
            _settings.ButtonMargin = (int)Math.Round(ButtonMarginBox.Value);
            _settings.ButtonLabel = ButtonLabelBox.Text?.Trim() ?? "";

            _settings.ShowVirtualKeyboard = ShowKeyboardToggle.IsOn;
            _settings.AutoShowKeyboardOnFocus = AutoShowKeyboardOnFocusToggle.IsOn;
            _settings.DefaultKeyboardLayout = GetTag(KeyboardLayoutCombo) ?? "he";
            _settings.KeyboardScale = ParseKeyboardScale(GetTag(KeyboardScaleCombo));
            _settings.BlockContextMenu = BlockContextMenuToggle.IsOn;
            _settings.DisablePinchZoom = DisablePinchZoomToggle.IsOn;
            _settings.ClearCacheOnClose = ClearCacheOnCloseToggle.IsOn;
            _settings.ShowNoInternetOverlay = ShowNoInternetOverlayToggle.IsOn;

            // לוגיקת סיסמת הקיוסק:
            // 1) אם 'הסר סיסמה' נלחץ - שומרים ריק.
            // 2) אם המשתמש הקליד סיסמה חדשה - מצפינים ושומרים hash.
            // 3) אחרת - משאירים את ה-hash הקיים (אין שינוי).
            var newPwd = KioskSettingsPasswordBox.Password ?? "";
            if (_clearKioskPassword)
                _settings.KioskSettingsPassword = "";
            else if (!string.IsNullOrEmpty(newPwd))
                _settings.KioskSettingsPassword = PasswordHasher.Hash(newPwd);
            else
                _settings.KioskSettingsPassword = _originalKioskPasswordHash;

            _settings.Buttons = _buttons.ToList();
            _settings.BlockedTimeRanges = _blockedRanges.ToList();

            SettingsManager.Save(_settings);

            // הפעלה/הסרה של המשימה המתוזמנת רק אם המצב השתנה. אם נכשל - מציג שגיאה אך
            // לא מבטל את שמירת שאר ההגדרות שכבר נכתבו לדיסק.
            if (!ApplyWatchdogTaskChange(out var watchdogError))
            {
                _ = ShowErrorAsync(watchdogError);
                return; // משאיר את חלון ההגדרות פתוח כדי שהמשתמש יוכל לראות את השגיאה
            }

            // הפעלה מחדש של החלון העילי כדי שיטען את ההגדרות החדשות.
            // FloatingButtonWindow מקבל snapshot של ההגדרות בקונסטרקטור ולא מנטר את הקובץ,
            // לכן הדרך היחידה להחיל את השינויים על המופע הרץ היא להפעיל מחדש.
            RestartKioskIfRunning();

            Close();
        }

        /// <summary>
        /// אם מופע Kiosk רץ - סוגר אותו ומפעיל חדש כדי שייטען עם ההגדרות החדשות.
        /// אם לא רץ - לא עושה כלום (לא מפעיל מופע חדש שלא היה קיים).
        /// </summary>
        private void RestartKioskIfRunning()
        {
            const string KioskMutexName = "Kolsites_Kiosk_{8F3A2C7E-4B5D-4F1A-9C8E-3D2B1A5F6E7D}";

            bool kioskRunning;
            try
            {
                using var m = new Mutex(false, KioskMutexName, out _);
                kioskRunning = !m.WaitOne(TimeSpan.Zero);
                if (!kioskRunning) m.ReleaseMutex();
            }
            catch
            {
                kioskRunning = false;
            }

            if (!kioskRunning) return;

            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;

                var name = Path.GetFileNameWithoutExtension(exe);

                foreach (var p in Process.GetProcessesByName(name))
                {
                    if (p.Id == currentPid) continue;
                    try { p.Kill(entireProcessTree: false); }
                    catch { }
                    try { p.WaitForExit(2000); }
                    catch { }
                }

                // המתנה קצרה לשחרור ה-Mutex לפני הרצת מופע חדש
                Thread.Sleep(200);

                Process.Start(new ProcessStartInfo
                {
                    FileName = exe!,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
                });
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("ההגדרות נשמרו, אך הפעלה מחדש של החלון העילי נכשלה: " + ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ClearKioskPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _clearKioskPassword = true;
            KioskSettingsPasswordBox.Password = "";
            UpdateKioskPasswordStatus();
        }

        private void LoadBlockedRanges()
        {
            _blockedRanges.Clear();
            BlockedRangesPanel.Children.Clear();

            // עותק עמוק כדי שביטול ההגדרות לא ישפיע (העותק נכתב חזרה רק בשמירה)
            foreach (var src in _settings.BlockedTimeRanges)
            {
                _blockedRanges.Add(new BlockedTimeRange
                {
                    Id = src.Id,
                    Name = src.Name,
                    Days = new List<int>(src.Days),
                    StartTime = src.StartTime,
                    EndTime = src.EndTime,
                    Enabled = src.Enabled
                });
            }
            foreach (var range in _blockedRanges)
                AppendRangeEditor(range);

            UpdateBlockedRangesEmptyState();
        }

        private void AppendRangeEditor(BlockedTimeRange range)
        {
            var editor = new BlockedTimeRangeEditor();
            editor.Bind(range);
            editor.RemoveRequested += (_, _) =>
            {
                _blockedRanges.Remove(range);
                BlockedRangesPanel.Children.Remove(editor);
                UpdateBlockedRangesEmptyState();
            };
            BlockedRangesPanel.Children.Add(editor);
        }

        private void UpdateBlockedRangesEmptyState()
        {
            NoBlockedRangesText.Visibility = _blockedRanges.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AddBlockedRangeButton_Click(object sender, RoutedEventArgs e)
        {
            // ברירת מחדל סבירה: שעות לילה (22:00-06:00) על כל ימי השבוע. המשתמש יערוך לפי הצורך.
            var range = new BlockedTimeRange
            {
                Days = new List<int> { 0, 1, 2, 3, 4, 5, 6 },
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(6, 0, 0),
                Enabled = true
            };
            _blockedRanges.Add(range);
            AppendRangeEditor(range);
            UpdateBlockedRangesEmptyState();
        }

        private void UpdateKioskPasswordStatus()
        {
            bool hasExisting =
                !_clearKioskPassword && !string.IsNullOrEmpty(_originalKioskPasswordHash);

            if (_clearKioskPassword)
                KioskPasswordStatusText.Text = "הסיסמה תוסר בעת השמירה (הקיצור יושבת).";
            else if (hasExisting)
                KioskPasswordStatusText.Text = "סיסמה מוגדרת. השאר ריק כדי לא לשנות.";
            else
                KioskPasswordStatusText.Text = "אין סיסמה - הקיצור מנוטרל. הזן סיסמה כדי להפעיל אותו.";

            ClearKioskPasswordButton.IsEnabled = hasExisting;
        }

        #endregion
    }
}
