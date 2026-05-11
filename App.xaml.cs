using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace Kolsites
{
    public partial class App : Application
    {
        private Window? _mainWindow;
        private readonly AppMode _mode;

        public App(AppMode mode)
        {
            _mode = mode;
            InitializeComponent();
            UnhandledException += (_, e) => TryLogError(e.Exception);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settings = SettingsManager.Load();

            switch (_mode)
            {
                case AppMode.Settings:
                    _mainWindow = new SettingsWindow(settings);
                    break;

                case AppMode.Kiosk:
                default:
                    // טעינת Kolsites במצב הקיוסק = מבטל את דגל ה'כיבוי ידני', כך שה-Watchdog
                    // יחזור לפעולה אם התוכנה תיפול שוב מאוחר יותר.
                    ManualStopFlag.Clear();

                    // מצב "ללא כפתור צף": פתיחה ישירה של חלון הקיוסק, וסגירה ב-X מסיימת את התהליך.
                    // ה-Watchdog מנוטרל ע"י ManualStopFlag שמוצב בסגירה.
                    if (settings.SkipFloatingButton || Program.ForceNoFloatingButton)
                    {
                        _mainWindow = new KioskWindow(settings, OnDirectKioskClosed, directMode: true);
                    }
                    else
                    {
                        _mainWindow = new FloatingButtonWindow(settings);
                    }
                    break;
            }

            _mainWindow.Activate();
        }

        private void OnDirectKioskClosed()
        {
            // במצב "ללא כפתור צף" סגירת חלון הקיוסק = יציאה מהתוכנה. מציבים ManualStopFlag
            // כדי שה-Watchdog לא יקפיץ מופע חדש בעוד 30ש'.
            ManualStopFlag.Set();
            Exit();
        }

        private static void TryLogError(Exception ex)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Kolsites");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, "errors.log");
                File.AppendAllText(logFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }
    }

    internal static class Native
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }
}
