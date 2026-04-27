using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kolsites
{
    public enum AppTheme { System, Light, Dark }

    /// <summary>מיקום הכפתור הצף על המסך</summary>
    public enum FloatingButtonPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        LeftCenter,
        RightCenter,
        TopCenter,
        BottomCenter
    }

    public class SiteButton
    {
        /// <summary>מזהה ייחודי - GUID</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>שם תצוגה (לניהול בהגדרות)</summary>
        public string Name { get; set; } = "";

        /// <summary>כתובת האתר שתיפתח</summary>
        public string Url { get; set; } = "";

        /// <summary>נתיב לקובץ אייקון מותאם (אופציונלי - אם ריק משתמש בברירת מחדל)</summary>
        public string IconPath { get; set; } = "";

        /// <summary>תווית טקסט שתוצג מתחת לאייקון</summary>
        public string Label { get; set; } = "";

        /// <summary>צבע רקע אופציונלי בפורמט HEX (#RRGGBB)</summary>
        public string BackgroundColor { get; set; } = "";

        /// <summary>האם הכפתור מופעל ומוצג</summary>
        public bool Enabled { get; set; } = true;
    }

    public class AppSettings
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppTheme Theme { get; set; } = AppTheme.System;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FloatingButtonPosition ButtonPosition { get; set; } = FloatingButtonPosition.BottomRight;

        /// <summary>גודל הכפתור הצף בפיקסלים (לוגיים)</summary>
        public int ButtonSize { get; set; } = 80;

        /// <summary>מרווח מקצה המסך בפיקסלים</summary>
        public int ButtonMargin { get; set; } = 20;

        /// <summary>תווית הכפתור הצף (אם ריק - מציג אייקון בלבד)</summary>
        public string ButtonLabel { get; set; } = "";

        /// <summary>רשימת הכפתורים בחלון העילי, בסדר התצוגה</summary>
        public List<SiteButton> Buttons { get; set; } = new();

        /// <summary>האם להפעיל מקלדת וירטואלית בחלון העילי</summary>
        public bool ShowVirtualKeyboard { get; set; } = true;

        /// <summary>מקלדת ברירת מחדל - "he" עברית, "en" אנגלית</summary>
        public string DefaultKeyboardLayout { get; set; } = "he";

        /// <summary>האם לחסום תפריט הקשר ימני בדף האינטרנט</summary>
        public bool BlockContextMenu { get; set; } = true;

        /// <summary>זמן ניקוי cache אוטומטי בעת סגירת הדפדפן (true תמיד)</summary>
        public bool ClearCacheOnClose { get; set; } = true;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kolsites");

        private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

        public static string GetSettingsPath() => SettingsPath;
        public static string GetSettingsFolder() => SettingsFolder;

        public static AppSettings Load()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);

                if (!File.Exists(SettingsPath))
                {
                    var defaults = CreateDefaults();
                    Save(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? CreateDefaults();

                // השלמת ID לכפתורים שאולי אין להם (אחורה תאימות)
                foreach (var b in settings.Buttons.Where(b => string.IsNullOrEmpty(b.Id)))
                    b.Id = Guid.NewGuid().ToString();

                return settings;
            }
            catch
            {
                return CreateDefaults();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Native.MessageBoxW(IntPtr.Zero,
                    $"שגיאה בשמירת ההגדרות: {ex.Message}",
                    "Kolsites",
                    0x10);
            }
        }

        /// <summary>הגדרות ברירת מחדל - כולל הכפתורים מהתוכנה הישנה</summary>
        public static AppSettings CreateDefaults()
        {
            return new AppSettings
            {
                Buttons = new List<SiteButton>
                {
                    new() { Name = "נדרים פלוס - לידת בן",      Url = "https://www.matara.pro/nedarimplus/Forms/976.html",                                                  Label = "לידת\nבן",       BackgroundColor = "#4CAF50" },
                    new() { Name = "דרשו",                      Url = "https://www.matara.pro/nedarimplus/Forms/Dirshu.html?Keyboard=tel&MasofId=&ClientId=&Zeout=&Version=75", Label = "דרשו",          BackgroundColor = "#2196F3" },
                    new() { Name = "שיח התורה",                Url = "https://rishumon.net/SiachAtorah.html",                                                              Label = "שיח\nהתורה",   BackgroundColor = "#9C27B0" },
                    new() { Name = "שיננא",                    Url = "https://www.matara.pro/nedarimplus/Forms/1953.html?Version=1",                                       Label = "שיננא",         BackgroundColor = "#FF5722" },
                    new() { Name = "ישיבה של כיברו",           Url = "https://www.matara.pro/YeshivaHalKivro",                                                              Label = "ישיבה של\nכיברו", BackgroundColor = "#795548" },
                    new() { Name = "רב קו אונליין",            Url = "https://ravkavonline.co.il/he/store/connect",                                                         Label = "רב קו",         BackgroundColor = "#3F51B5" },
                    new() { Name = "רישום למערכת מתרא",        Url = "https://www.matara.pro/nedarimplus/Forms/3347.html",                                                  Label = "רישום\nראשוני", BackgroundColor = "#009688" },
                    new() { Name = "עדכון לימוד חודשי",       Url = "https://www.matara.pro/nedarimplus/Forms/3348.html",                                                  Label = "עדכון\nחודשי",  BackgroundColor = "#607D8B" }
                }
            };
        }
    }
}
