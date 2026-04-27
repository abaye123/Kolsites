using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace Kolsites
{
    public static class Program
    {
        // חייב להתאים לגרסת ה-NuGet Microsoft.WindowsAppSDK ב-csproj (1.7)
        private static readonly uint RequiredMajorMinor = 0x00010007;

        [STAThread]
        public static int Main(string[] args)
        {
            // קביעת מצב ההפעלה לפי ארגומנט ראשון
            var mode = ParseMode(args);

            // מצב Watchdog רץ ללא XAML/UI - תהליך עצמאי קליל
            if (mode == AppMode.Watchdog)
            {
                return WatchdogService.Run();
            }

            // הגנה מפני ריצה כפולה של אותו מצב
            // (ל-settings ול-kiosk mutex נפרדים, כדי שיהיה אפשר להפעיל את שניהם במקביל)
            string mutexName = mode switch
            {
                AppMode.Settings => "Kolsites_Settings_{2C8A1F4D-9B5E-4F1A-B3C7-1D2E3F4A5B6C}",
                _ => "Kolsites_Kiosk_{8F3A2C7E-4B5D-4F1A-9C8E-3D2B1A5F6E7D}"
            };

            using var mutex = new Mutex(true, mutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                // כבר רץ - לא פותחים מופע נוסף
                return 0;
            }

            // אתחול Bootstrapper של Windows App SDK (חובה ל-WinUI 3 unpackaged)
            try
            {
                Bootstrap.Initialize(RequiredMajorMinor);
            }
            catch (Exception ex)
            {
                MessageBoxW(IntPtr.Zero,
                    "חסר Windows App Runtime במערכת.\n\n" +
                    "אנא התקן את Microsoft Windows App Runtime 1.7 מהכתובת:\n" +
                    "https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe\n\n" +
                    $"פרטי שגיאה:\n{ex.Message}",
                    "Kolsites - שגיאת התקנה",
                    0x10);
                return 1;
            }

            try
            {
                Application.Start((p) =>
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App(mode);
                });
                return 0;
            }
            finally
            {
                Bootstrap.Shutdown();
            }
        }

        private static AppMode ParseMode(string[] args)
        {
            if (args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
                return AppMode.Settings;
            if (args.Any(a => a.Equals("--watchdog", StringComparison.OrdinalIgnoreCase)))
                return AppMode.Watchdog;
            return AppMode.Kiosk;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }

    public enum AppMode
    {
        Kiosk,
        Settings,
        Watchdog
    }
}
