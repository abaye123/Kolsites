using System;
using System.IO;
using System.Text.Json;

namespace Kolsites
{
    /// <summary>
    /// מצב נעילה של מנגנון הסיסמה לפתיחת ההגדרות מתוך הקיוסק.
    /// נשמר בקובץ נפרד מ-settings.json כדי שלא יידרס אם המשתמש מייצא/מייבא הגדרות,
    /// ושלא יחזור לאחור גם אם תהליך הקיוסק נפל ועלה מחדש.
    /// </summary>
    public class KioskAuthState
    {
        public const int MaxAttempts = 3;
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(30);

        public int FailedAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }

        private static string FilePath => Path.Combine(
            SettingsManager.GetSettingsFolder(), "auth_state.json");

        public static KioskAuthState Load()
        {
            KioskAuthState state;
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    state = JsonSerializer.Deserialize<KioskAuthState>(json) ?? new KioskAuthState();
                }
                else
                {
                    state = new KioskAuthState();
                }
            }
            catch
            {
                state = new KioskAuthState();
            }

            // ניקוי אוטומטי אם הנעילה פגה - איפוס מלא של המונה (3 נסיונות חדשים).
            if (state.LockedUntil.HasValue && state.LockedUntil.Value <= DateTime.Now)
            {
                state.LockedUntil = null;
                state.FailedAttempts = 0;
                state.Save();
            }

            return state;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsManager.GetSettingsFolder());
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public bool IsLocked(out TimeSpan remaining)
        {
            if (LockedUntil.HasValue && LockedUntil.Value > DateTime.Now)
            {
                remaining = LockedUntil.Value - DateTime.Now;
                return true;
            }
            remaining = TimeSpan.Zero;
            return false;
        }

        /// <summary>רושם נסיון כושל. בהגעה ל-MaxAttempts - מפעיל נעילה ל-LockDuration.</summary>
        public void RecordFailure()
        {
            FailedAttempts++;
            if (FailedAttempts >= MaxAttempts)
                LockedUntil = DateTime.Now + LockDuration;
            Save();
        }

        public void RecordSuccess()
        {
            FailedAttempts = 0;
            LockedUntil = null;
            Save();
        }
    }
}
