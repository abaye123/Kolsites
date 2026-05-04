using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace Kolsites
{
    /// <summary>
    /// סימון "כיבוי ידני" - נוצר בלחיצה על 'סגור את Kolsites' בחלון ההגדרות,
    /// ונמחק כש-Kolsites נטען מחדש במצב Kiosk (מכל מקור: הפעלה ידנית, AutoStart, וכו').
    /// כשהדגל קיים, ה-Watchdog לא יפעיל מחדש - המשתמש סגר במכוון.
    /// </summary>
    public static class ManualStopFlag
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kolsites",
            "manual-stop.flag");

        public static bool IsSet()
        {
            try { return File.Exists(FilePath); }
            catch { return false; }
        }

        public static void Set()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch { /* כישלון לא קריטי */ }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { /* כישלון לא קריטי */ }
        }
    }

    /// <summary>
    /// מצב Watchdog - מצב חיצוני שמופעל ע"י Task Scheduler (או הפעלה ידנית).
    /// הוא בודק האם ה-Kolsites הראשי רץ (במצב Kiosk), ואם לא - מפעיל אותו.
    /// תהליך ה-Watchdog יוצא מיד לאחר וידוא קיום המופע, אלא אם מופעל עם --loop.
    /// כך אפשר להריץ אותו מ-Task Scheduler כל שעה (או דקה).
    /// </summary>
    internal static class WatchdogService
    {
        private const string LockMutexName = "Kolsites_Watchdog_Lock_{1A4F6B8D-2C3E-4D5F-6A7B-8C9D0E1F2A3B}";
        private const string KioskMutexName = "Kolsites_Kiosk_{8F3A2C7E-4B5D-4F1A-9C8E-3D2B1A5F6E7D}";

        // ערך "Kolsites" תחת מפתח Run - נוצר ע"י המתקין כשמסומן Tasks: autostart.
        private const string AutoStartRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartValueName = "Kolsites";

        public static int Run()
        {
            // הגנה מפני מספר Watchdogs במקביל
            using var lockMutex = new Mutex(true, LockMutexName, out bool isNewWatchdog);
            if (!isNewWatchdog)
            {
                // כבר רץ Watchdog אחר
                return 0;
            }

            try
            {
                var args = Environment.GetCommandLineArgs();
                bool loop = args.Any(a => a.Equals("--loop", StringComparison.OrdinalIgnoreCase));

                if (loop)
                {
                    // מצב הפעלה רציפה - שימושי בעת הריצה ידנית או כתהליך משנה של ההתקנה
                    while (true)
                    {
                        EnsureKioskRunning();
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                    }
                }
                else
                {
                    // הרצה חד-פעמית - ל-Task Scheduler
                    EnsureKioskRunning();
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log("שגיאה ב-Watchdog: " + ex);
                return 1;
            }
        }

        private static void EnsureKioskRunning()
        {
            // הדרך האמינה ביותר לזהות שמופע ה-Kiosk רץ זה לבדוק אם ה-Mutex תפוס.
            // אם אנחנו מצליחים להחזיק בו - זה אומר שאין Kiosk רץ.
            bool kioskRunning;
            using (var checkMutex = new Mutex(true, KioskMutexName, out bool created))
            {
                kioskRunning = !created;
                if (created)
                {
                    // משחררים מיד - לפני שאנחנו מפעילים את התהליך החדש
                    checkMutex.ReleaseMutex();
                }
            }

            if (kioskRunning) return;

            // לא מפעילים אם המשתמש סגר ידנית את התוכנה מההגדרות. הדגל יימחק כש-Kolsites
            // ייטען שוב (מהפעלה ידנית, AutoStart, וכו'), ואז ה-Watchdog יחזור לפעולה.
            if (ManualStopFlag.IsSet())
            {
                Log("דילוג: הוגדר 'כיבוי ידני' מההגדרות.");
                return;
            }

            // לא מפעילים אם ההפעלה האוטומטית בעלייה (HKLM/HKCU\...\Run) הוסרה. ההיגיון:
            // אם המשתמש לא רוצה ש-Kolsites יעלה אוטומטית במחשב, הוא בוודאי לא רוצה
            // שמתזמן המשימות יקפיץ אותו במהלך השימוש.
            if (!IsAutoStartEnabled())
            {
                Log("דילוג: ההפעלה האוטומטית בעלייה מנוטרלת.");
                return;
            }

            LaunchKiosk();
        }

        private static bool IsAutoStartEnabled()
        {
            // נבדק גם ב-HKLM (הותקן לכל המשתמשים) וגם ב-HKCU (במידה והוגדר ידנית).
            return HasRunValue(Registry.LocalMachine) || HasRunValue(Registry.CurrentUser);
        }

        private static bool HasRunValue(RegistryKey root)
        {
            try
            {
                using var key = root.OpenSubKey(AutoStartRunKey, writable: false);
                if (key == null) return false;
                var value = key.GetValue(AutoStartValueName) as string;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        private static void LaunchKiosk()
        {
            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    Log("לא ניתן לאתר את קובץ ה-EXE.");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory
                };
                Process.Start(psi);
                Log("הופעל מופע חדש של Kolsites.");
            }
            catch (Exception ex)
            {
                Log("נכשל בהפעלת Kolsites: " + ex.Message);
            }
        }

        private static void Log(string message)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Kolsites");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, "watchdog.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");

                // שמירת גודל סביר ללוג (חיתוך מעבר ל-200KB)
                var info = new FileInfo(logFile);
                if (info.Exists && info.Length > 200_000)
                {
                    var lines = File.ReadAllLines(logFile);
                    File.WriteAllLines(logFile, lines.Skip(Math.Max(0, lines.Length - 500)));
                }
            }
            catch { }
        }
    }
}
