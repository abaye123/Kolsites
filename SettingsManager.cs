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

    public class SiteScript
    {
        /// <summary>שם התיאור של הסקריפט (לניהול בהגדרות)</summary>
        public string Name { get; set; } = "";

        /// <summary>קוד JavaScript להרצה. רץ בהקשר של הדף הטעון.</summary>
        public string Code { get; set; } = "";

        /// <summary>האם להריץ את הסקריפט - מאפשר השבתה זמנית בלי למחוק</summary>
        public bool Enabled { get; set; } = true;
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

        /// <summary>
        /// הקשחת ניווט: כשמופעל, מותר לנווט רק לכתובות שמתחת לנתיב של ה-URL של הכפתור
        /// (לדוגמה: URL "https://ladaat.co/gilyonot/" -> מותר רק /gilyonot/*, וייחסמו /, /?cat=38 וכד').
        /// בנוסף, נחסמים תת-דומיינים (sub.ladaat.co לא יתאפשר) - מצב "דומיין מדויק" כדי שלא תהיה
        /// עקיפה דרך מארח אחר תחת אותו דומיין-על.
        /// </summary>
        public bool RestrictToPath { get; set; } = false;

        /// <summary>
        /// סקריפטים שירוצו על האתר (כמו TimerDirshu/TimerYak במקור) -
        /// מסירים אלמנטים מסויימים, מסתירים פרסומות, וכו'.
        /// רצים כל שנייה בזמן שהאתר טעון.
        /// </summary>
        public List<SiteScript> Scripts { get; set; } = new();
    }

    /// <summary>
    /// טווח זמן שבו התוכנה אמורה להיות מנוטרלת - לחיצה על הכפתור הצף תציג חלון
    /// "התוכנה לא פעילה כרגע" במקום לפתוח את חלון הקיוסק.
    /// טווח שחוצה חצות: EndTime &lt; StartTime (למשל 22:00-06:00).
    /// </summary>
    public class BlockedTimeRange
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>תיאור אופציונלי (לזיהוי בהגדרות)</summary>
        public string Name { get; set; } = "";

        /// <summary>ימי השבוע שעליהם הטווח חל. 0=ראשון, 1=שני, ..., 6=שבת</summary>
        public List<int> Days { get; set; } = new();

        /// <summary>שעת התחלה (TimeSpan: HH:MM)</summary>
        public TimeSpan StartTime { get; set; } = TimeSpan.Zero;

        /// <summary>שעת סיום (TimeSpan: HH:MM). אם קטנה מ-StartTime - הטווח חוצה חצות.</summary>
        public TimeSpan EndTime { get; set; } = TimeSpan.Zero;

        public bool Enabled { get; set; } = true;
    }

    public class AppSettings
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppTheme Theme { get; set; } = AppTheme.System;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FloatingButtonPosition ButtonPosition { get; set; } = FloatingButtonPosition.TopLeft;

        /// <summary>גודל הכפתור הצף בפיקסלים (לוגיים)</summary>
        public int ButtonSize { get; set; } = 80;

        /// <summary>מרווח מקצה המסך בפיקסלים</summary>
        public int ButtonMargin { get; set; } = 5;

        /// <summary>תווית הכפתור הצף (אם ריק - מציג אייקון בלבד)</summary>
        public string ButtonLabel { get; set; } = "";

        /// <summary>רשימת הכפתורים בחלון העילי, בסדר התצוגה</summary>
        public List<SiteButton> Buttons { get; set; } = new();

        /// <summary>האם להפעיל מקלדת וירטואלית בחלון העילי</summary>
        public bool ShowVirtualKeyboard { get; set; } = true;

        /// <summary>
        /// האם לפתוח את המקלדת הווירטואלית אוטומטית כשפקד קלט בדף האינטרנט מקבל פוקוס.
        /// תקף רק כש-ShowVirtualKeyboard = true.
        /// </summary>
        public bool AutoShowKeyboardOnFocus { get; set; } = true;

        /// <summary>מקלדת ברירת מחדל - "he" עברית, "en" אנגלית</summary>
        public string DefaultKeyboardLayout { get; set; } = "he";

        /// <summary>
        /// מכפיל גודל המקלדת הווירטואלית. 1.0 = רגיל. ערך גבוה יותר מגדיל את גובה
        /// המקלדת ואת המקשים (שימושי במסך לאורך שיש בו מקום לגובה גדול יותר).
        /// </summary>
        public double KeyboardScale { get; set; } = 1.0;

        /// <summary>האם לחסום תפריט הקשר ימני בדף האינטרנט</summary>
        public bool BlockContextMenu { get; set; } = true;

        /// <summary>
        /// האם להשבית זום באמצעות צביטה במסך מגע. שימושי בעמדות עם מסכים בעלי רגישות גבוהה,
        /// שבהן משתמשים מתקשים לבטל זום שנעשה בטעות. כשמופעל - גם Ctrl+גלגלת מנוטרל.
        /// </summary>
        public bool DisablePinchZoom { get; set; } = false;

        /// <summary>זמן ניקוי cache אוטומטי בעת סגירת הדפדפן (true תמיד)</summary>
        public bool ClearCacheOnClose { get; set; } = true;

        /// <summary>
        /// האם להציג מסך מלא של "אין אינטרנט" כאשר זוהה ניתוק.
        /// במקרה של ניתוקי רשת קלים זה יכול להפריע - אז עדיף להציג רק אייקון התראה בסרגל.
        /// </summary>
        public bool ShowNoInternetOverlay { get; set; } = false;

        /// <summary>
        /// סיסמה לפתיחת ההגדרות מתוך הקיוסק (לחיצה כפולה על הלוגו ב-About).
        /// אם ריק - הקיצור מנוטרל לחלוטין; ברירת המחדל היא "ללא סיסמה" כדי שהקיוסק לא ייפתח להגדרות בטעות.
        /// </summary>
        public string KioskSettingsPassword { get; set; } = "";

        /// <summary>
        /// טווחי זמן שבהם התוכנה אמורה להיות חסומה. בלחיצה על הכפתור הצף בזמן חסום
        /// יוצג חלון "התוכנה לא פעילה כרגע" עם מונה דקות עד לסיום הטווח.
        /// </summary>
        public List<BlockedTimeRange> BlockedTimeRanges { get; set; } = new();

        /// <summary>
        /// מצב "ללא כפתור צף": הפעלת התוכנה תפתח ישר את חלון הקיוסק (עם title bar רגיל ו-X),
        /// וסגירת החלון ב-X תסיים את התהליך. במצב הזה: אין כפתור צף, אין always-on-top, אין מסך
        /// מלא, אין טיימר חוסר-פעילות, ובסגירה מוצב ManualStopFlag כדי שה-Watchdog לא יפעיל מחדש.
        /// </summary>
        public bool SkipFloatingButton { get; set; } = false;
    }

    /// <summary>
    /// ניהול ערכי localStorage שיש לשמר על אף ניקוי cache (למשל API_KEY של אתרי abaye).
    /// הערכים נשמרים בקובץ נפרד בתיקיית ההגדרות, ומשוחזרים אוטומטית בטעינת דף של ההוסט המתאים.
    /// </summary>
    public static class PreservedStorageManager
    {
        // מפתח: "host|key" (למשל "shulchoni.abaye.co|API_KEY"). ערך: ערך ה-localStorage השמור.
        private static readonly object _lock = new();

        private static string FilePath => Path.Combine(
            SettingsManager.GetSettingsFolder(), "preserved-storage.json");

        public static Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new Dictionary<string, string>();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public static void Save(Dictionary<string, string> values)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(SettingsManager.GetSettingsFolder());
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(values, options));
                }
            }
            catch { }
        }
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

                // מיגרציה: אם כפתור עם URL זהה לברירת מחדל לא מכיל סקריפטים, נשלים מברירת המחדל
                MergeDefaultScriptsIfMissing(settings);

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

        /// <summary>
        /// מיגרציה לתאימות אחורנית: אם כפתור קיים עם URL שתואם לברירת מחדל אך ללא סקריפטים
        /// או ללא IconPath - משלים מברירת המחדל. לא משנה ערכים מותאמים שכבר קיימים.
        /// </summary>
        private static void MergeDefaultScriptsIfMissing(AppSettings settings)
        {
            try
            {
                var defaults = CreateDefaults();
                foreach (var btn in settings.Buttons)
                {
                    var match = defaults.Buttons.FirstOrDefault(d =>
                        !string.IsNullOrEmpty(d.Url) &&
                        string.Equals(d.Url, btn.Url, StringComparison.OrdinalIgnoreCase));

                    if (match == null) continue;

                    // השלמת סקריפטים אם חסרים
                    if ((btn.Scripts == null || btn.Scripts.Count == 0) && match.Scripts.Count > 0)
                    {
                        btn.Scripts = match.Scripts.Select(s => new SiteScript
                        {
                            Name = s.Name,
                            Code = s.Code,
                            Enabled = s.Enabled
                        }).ToList();
                    }

                    // השלמת תמונה אם חסרה - בודק גם שהתמונה הנכנסת קיימת בפועל בדיסק
                    if (string.IsNullOrWhiteSpace(btn.IconPath) &&
                        !string.IsNullOrWhiteSpace(match.IconPath) &&
                        File.Exists(match.IconPath))
                    {
                        btn.IconPath = match.IconPath;
                    }
                }
            }
            catch { /* מיגרציה לא קריטית - בהצלחה או לא, נמשיך עם מה שיש */ }
        }

        /// <summary>נתיב מלא לתמונת ברירת מחדל בתיקיית Assets\DefaultButtons</summary>
        public static string DefaultIconPath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultButtons", fileName);

        /// <summary>הגדרות ברירת מחדל - כולל הכפתורים מהתוכנה הישנה והסקריפטים שלהם</summary>
        public static AppSettings CreateDefaults()
        {
            // עוזר ליצירת סקריפט שמסיר אלמנטים לפי IDs (התבנית הנפוצה ביותר במקור)
            static SiteScript RemoveByIds(string name, params string[] ids)
            {
                var list = string.Join(",", ids.Select(i => $"'{i}'"));
                return new SiteScript
                {
                    Name = name,
                    Code = $"[{list}].forEach(function(id){{ var el=document.getElementById(id); if(el) el.remove(); }});",
                    Enabled = true
                };
            }

            // עוזר לסקריפט שמסיר .MainBt לפי attribute של onclick
            static SiteScript RemoveMainBtByOnclick(string name, string onclickValue) =>
                new()
                {
                    Name = name,
                    Code = $"document.querySelectorAll('.MainBt').forEach(function(el){{ if(el.getAttribute('onclick')===\"{onclickValue}\") el.remove(); }});",
                    Enabled = true
                };

            return new AppSettings
            {
                Buttons = new List<SiteButton>
                {
                    new()
                    {
                        Name = "לדעת",
                        Url = "https://www.matara.pro/nedarimplus/Forms/976.html",
                        Label = "לדעת",
                        BackgroundColor = "#4CAF50",
                        IconPath = DefaultIconPath("ladat.png"),
                        Scripts = new List<SiteScript>
                        {
                            RemoveByIds("הסר לוגו ופוטר", "FooterLogo", "SecurityLogo", "task1-2")
                        }
                    },
                    new()
                    {
                        Name = "דרשו",
                        Url = "https://www.matara.pro/nedarimplus/Forms/Dirshu.html?Keyboard=tel&MasofId=&ClientId=&Zeout=&Version=75",
                        Label = "דרשו",
                        BackgroundColor = "#2196F3",
                        IconPath = DefaultIconPath("dirshu.png"),
                        Scripts = new List<SiteScript>
                        {
                            RemoveByIds("הסר תפריט וכותרות", "MenuBack", "FooterLogo", "task1-2")
                        }
                    },
                    new()
                    {
                        Name = "שיח התורה",
                        Url = "https://rishumon.net/SiachAtorah.html",
                        Label = "שיח התורה",
                        BackgroundColor = "#9C27B0",
                        IconPath = DefaultIconPath("siach_atorah.png"),
                        Scripts = new List<SiteScript>
                        {
                            RemoveByIds("הסר תפריט וכותרות", "TopMenu", "TopNedarimBack", "TopMenuBg", "FooterLogo", "SignPad", "task1-2"),
                            RemoveMainBtByOnclick("הסר כפתור פניות", "GetPniyot()")
                        }
                    },
                    new()
                    {
                        Name = "שיננא",
                        Url = "https://www.matara.pro/nedarimplus/Forms/1953.html",
                        Label = "שיננא",
                        BackgroundColor = "#FF5722",
                        IconPath = DefaultIconPath("shinana.png"),
                        Scripts = new List<SiteScript>
                        {
                            RemoveByIds("הסר אלמנטים", "Event_Bt", "SignPad", "FooterLogo", "SecurityLogo", "task1-2")
                        }
                    },
                    new()
                    {
                        Name = "ישיבה על קברו",
                        Url = "https://www.matara.pro/YeshivaHalKivro",
                        Label = "ישיבה על קברו",
                        BackgroundColor = "#795548",
                        IconPath = DefaultIconPath("yeshiva_yakir.png"),
                        Scripts = new List<SiteScript>
                        {
                            RemoveByIds("הסר פרסומות וכותרות", "AdsDiv", "ChooseDayBt", "TopMenuBack", "TopMenu", "TopNedarimBack", "SignPad", "FooterLogo", "task1-2"),
                            RemoveMainBtByOnclick("הסר כפתור פניה", "GoTo('Pniya')")
                        }
                    },
                    new()
                    {
                        Name = "רב קו אונליין",
                        Url = "https://ravkavonline.co.il/he/store/connect",
                        Label = "רב קו",
                        BackgroundColor = "#3F51B5",
                        Enabled = false,
                        IconPath = DefaultIconPath("ravkav.png"),
                        Scripts = new List<SiteScript>
                        {
                            new()
                            {
                                Name = "ניקוי ממשק רב-קו",
                                Code = @"
['ravkav-logo','reusableId','task1-2'].forEach(function(id){var el=document.getElementById(id);if(el)el.remove();});
document.querySelectorAll('.top-nav__item').forEach(function(el){
  var a = el.querySelector('a');
  if(!a) return;
  var h = a.getAttribute('href');
  if(h==='/he/store/ravkav-issue-request/check' || h==='/he/store/account' || h==='/he/store/profile-loading/check') el.remove();
  if(el.querySelector('.language-picker')) el.remove();
});
document.querySelectorAll('.flex.flex--align-start.flex--wrap.m--top--small').forEach(function(el){el.remove();});
document.querySelectorAll('.bg--stable .notification').forEach(function(el){el.remove();});
",
                                Enabled = true
                            }
                        }
                    },
                    new()
                    {
                        Name = "תורה שבכתב",
                        Url = "https://tora.irom.co.il/",
                        Label = "תורה שבכתב",
                        BackgroundColor = "#607D8B",
                        IconPath = DefaultIconPath("torasebktav.png"),
                        Scripts = new List<SiteScript>
                        {
                            RemoveByIds("הסר לוגו ופוטר", "FooterLogo", "SecurityLogo", "task1-2")
                        }
                    }
                }
            };
        }
    }
}
