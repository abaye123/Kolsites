using System;
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

            ButtonSizeBox.Value = _settings.ButtonSize;
            ButtonMarginBox.Value = _settings.ButtonMargin;
            ButtonLabelBox.Text = _settings.ButtonLabel;

            ShowKeyboardToggle.IsOn = _settings.ShowVirtualKeyboard;
            BlockContextMenuToggle.IsOn = _settings.BlockContextMenu;
            ClearCacheOnCloseToggle.IsOn = _settings.ClearCacheOnClose;

            ButtonsList.ItemsSource = _buttons;

            ApplyCurrentTheme();
            UpdateProcessStatus();
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

            appWindow.Resize(new SizeInt32(720, 920));

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

        private void PopulateAbout()
        {
            try
            {
                var v = typeof(SettingsWindow).Assembly.GetName().Version;
                AboutVersionText.Text = v != null
                    ? $"Kolsites · גרסה {v.Major}.{v.Minor}.{v.Build}"
                    : "Kolsites · גרסה 1.0.0";
            }
            catch
            {
                AboutVersionText.Text = "Kolsites · גרסה 1.0.0";
            }

            SettingsPathText.Text = $"קובץ הגדרות: {SettingsManager.GetSettingsPath()}";
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
            _settings.DefaultKeyboardLayout = GetTag(KeyboardLayoutCombo) ?? "he";
            _settings.BlockContextMenu = BlockContextMenuToggle.IsOn;
            _settings.ClearCacheOnClose = ClearCacheOnCloseToggle.IsOn;

            _settings.Buttons = _buttons.ToList();

            SettingsManager.Save(_settings);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        #endregion
    }
}
