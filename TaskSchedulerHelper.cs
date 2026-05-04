using System;
using System.Diagnostics;
using System.IO;

namespace Kolsites
{
    /// <summary>
    /// ניהול משימת ה-Watchdog במתזמן המשימות של Windows באמצעות schtasks.exe.
    /// המשימה מפעילה את Kolsites במצב --watchdog: בודקת אם הקיוסק רץ ואם לא - מפעילה אותו.
    /// כשנוצרת מההגדרות הזמנים מכווננים לשעתי (אחת לשעה).
    /// </summary>
    public static class TaskSchedulerHelper
    {
        // אותו שם שבו משתמש המתקין (Installer\Kolsites.iss). שינוי כאן ידרוש סנכרון שם.
        public const string TaskName = "Kolsites Watchdog";

        public static bool IsTaskInstalled()
        {
            var (exit, _, _) = RunSchtasks($"/Query /TN \"{TaskName}\"");
            return exit == 0;
        }

        /// <summary>יוצר/דורס את המשימה לבדיקה אחת לשעה (החלפת לוח זמנים קיים).</summary>
        public static (bool ok, string error) InstallHourly()
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                return (false, "לא ניתן לאתר את קובץ ההפעלה של Kolsites.");

            // /F דורס משימה קיימת באותו שם (גם אם נוצרה ע"י המתקין כ-RUN MINUTE).
            var args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --watchdog\" /SC HOURLY /MO 1 /F";
            var (exit, stdout, stderr) = RunSchtasks(args);
            if (exit == 0) return (true, "");

            var msg = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
            return (false, msg.Trim());
        }

        public static (bool ok, string error) Uninstall()
        {
            if (!IsTaskInstalled()) return (true, "");

            var (exit, stdout, stderr) = RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
            if (exit == 0) return (true, "");

            var msg = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
            return (false, msg.Trim());
        }

        private static (int exitCode, string stdout, string stderr) RunSchtasks(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var p = Process.Start(psi);
                if (p == null) return (-1, "", "כישלון בהפעלת schtasks.exe");

                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                return (p.ExitCode, stdout, stderr);
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message);
            }
        }
    }
}
