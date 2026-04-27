<div align="center">
  <img src="Assets/AppIcon.png" alt="Kolsites" width="128" height="128" />
</div>

# Kolsites

תוכנת קיוסק לעמדות מסך מגע ציבוריות, מבוססת WebView2 ו-WinUI 3. מציגה כפתור צף תמיד-למעלה שפותח חלון מסך מלא עם רשימת אתרים ניתנת להגדרה — הכל מוגן מפני יציאה, עם מקלדת וירטואלית, ניטור אינטרנט ו-Watchdog שמפעיל מחדש אוטומטית.

מתאים לעמדות עם דפי טפסים מקוונים (תרומות, פניות, רישומים) שצריכות להישאר נעולות על זרימת השימוש בלבד, בלי שמשתמשים יוכלו לצאת לדפדפן או למערכת ההפעלה.

## ✨ תכונות עיקריות

- **כפתור צף תמיד-למעלה** — נשאר מעל כל החלונות במערכת (כולל אפליקציות מסך מלא), בלי להופיע ב-Alt+Tab או ב-taskbar
- **חלון קיוסק מסך מלא** — נפתח בלחיצה על הכפתור, ללא מסגרת ובלי דרך פשוטה לצאת
- **רשימת אתרים מוגדרת מראש** — שבעה אתרים דיפולטיים (לדעת, דרשו, שיח התורה, שיננא, ישיבה על קברו, רב קו, תורה שבכתב), ניתנים לעריכה והוספה
- **סקריפטים מותאמים לכל אתר** — הזרקת JavaScript אוטומטית כל שנייה כדי להסיר לוגואים, תפריטים, פרסומות וכפתורים שלא רצויים בעמדה ציבורית
- **מקלדת וירטואלית** — תמיכה בעברית ואנגלית עם הקלדה ישירה לתוך שדות באתר הטעון
- **ניתוק אוטומטי לאי-פעילות** — לאחר 45 שניות ללא קלט (עכבר/מגע/מקלדת) החלון נסגר וחוזר לכפתור הצף, מנקה cache בדרך
- **ניטור אינטרנט** — בדיקת ping כל 5 שניות, עם אפשרות להציג overlay מסך מלא או רק badge דיסקרטי בסרגל
- **חסימת תפריט הקשר ימני** ו-DevTools, בלי דרך פשוטה לפתוח כתובת אחרת
- **Watchdog Service** — משימה מתוזמנת ב-Task Scheduler שמוודאת כל דקה שהתוכנה רצה, ומפעילה מחדש אוטומטית אם נסגרה
- **ניקוי cache אוטומטי** בעת סגירת הקיוסק — להגנה על פרטיות המשתמש הבא
- **מצב כהה/בהיר/לפי המערכת** — ברירת מחדל עוקבת אחר הגדרות Windows
- **תמיכה ב-RTL** — ממשק מלא בעברית
- **חלון הגדרות נפרד** — לעריכת רשימת האתרים, הסקריפטים, מיקום הכפתור הצף ושאר ההגדרות

## 📥 התקנה

1. הורד את `Kolsites-Setup-1.0.0.exe` מ-[דף ה-Releases](https://github.com/abaye123/Kolsites/releases)
2. הרץ את המתקין (דורש הרשאות אדמין — מתקין לכל המשתמשים במחשב)
3. בזמן ההתקנה אפשר לבחור:
   - קיצור דרך לכפתור הצף על שולחן העבודה
   - קיצור דרך להגדרות על שולחן העבודה
   - הפעלה אוטומטית בעלייה (מומלץ לעמדת קיוסק)
   - התקנת Watchdog שיפעיל את התוכנה מחדש אם תיסגר (מומלץ)
4. אם **Windows App Runtime 1.7** חסר, המתקין יתריע ויפנה לדף ההורדה של Microsoft

### דרישות מערכת

- Windows 10 build 17763 (1809) ומעלה, או Windows 11
- .NET 8 Desktop Runtime
- Windows App Runtime 1.7
- WebView2 Runtime (מותקן אוטומטית בכל Windows מודרני)

## 🚀 שימוש

### שימוש רגיל
לאחר ההתקנה הכפתור הצף מופיע באחת מפינות המסך (דיפולטית למעלה-שמאל). לחיצה על הכפתור פותחת את חלון הקיוסק עם רשימת האתרים. בחירת אתר טוענת אותו במסך מלא.

לאחר 45 שניות ללא פעילות החלון נסגר אוטומטית, מנקה cache, וחוזר לכפתור הצף. אפשר גם לסגור ידנית בכפתור הסגירה.

### חלון ההגדרות
פתח את "הגדרות Kolsites" מתפריט התחל או משולחן העבודה. אפשר לערוך:
- **רשימת אתרים** — להוסיף, להסיר, לשנות סדר, להעלות אייקון מותאם, לבחור צבע רקע
- **סקריפטים לכל אתר** — JavaScript שירוץ כל שנייה על האתר (להסרת אלמנטים, חסימת פעולות וכו')
- **מקלדת וירטואלית** — הפעלה/השבתה ובחירת שפה דיפולטית
- **מיקום הכפתור הצף** — אחת משמונה פוזיציות סביב המסך
- **גודל הכפתור הצף** ומרווח מקצה המסך
- **מצב כהה/בהיר/לפי המערכת**
- **התנהגות בעת ניתוק אינטרנט** — overlay מסך מלא או badge קטן

### שורת פקודה
```
Kolsites.exe                 # מצב רגיל - כפתור צף
Kolsites.exe --settings      # פתיחת חלון ההגדרות
Kolsites.exe --watchdog      # מצב Watchdog (חד-פעמי)
Kolsites.exe --watchdog --loop  # Watchdog רציף (כל 30 שניות)
```

## 🛠️ בנייה מהמקור

### כלים נדרשים
```powershell
winget install Microsoft.DotNet.SDK.8
winget install JRSoftware.InnoSetup
```

### בנייה
```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained false
```

התוצר: `bin\x64\Release\net8.0-windows10.0.19041.0\`

### יצירת setup.exe
```powershell
cd Installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Kolsites.iss
```

הפלט: `Release\Kolsites-Setup-1.0.0.exe`

## 📁 מבנה הפרויקט

```
Kolsites/
├── Kolsites.csproj                # WinUI 3 unpackaged
├── app.manifest                   # DPI awareness, Windows 10/11 support
├── Program.cs                     # Main() עם Bootstrap.Initialize של WinAppSDK + ניתוב מצב הפעלה
├── App.xaml(.cs)                  # Application class
├── FloatingButtonWindow.xaml(.cs) # הכפתור הצף תמיד-למעלה
├── KioskWindow.xaml(.cs)          # החלון הראשי במסך מלא + WebView2
├── SettingsWindow.xaml(.cs)       # חלון ההגדרות
├── OnScreenKeyboard.xaml(.cs)     # מקלדת וירטואלית עברית/אנגלית
├── SettingsManager.cs             # קריאה/כתיבה של settings.json + ברירות מחדל
├── WatchdogService.cs             # מצב Watchdog שמוודא שהתוכנה רצה
├── WindowHelper.cs                # P/Invoke wrappers לחלונות
├── ThemeHelper.cs                 # מצב כהה/בהיר
├── Assets/
│   ├── AppIcon.ico / AppIcon.png  # אייקון התוכנה
│   ├── PleaseWait.html            # מסך טעינה זמני
│   └── DefaultButtons/            # אייקונים לאתרים הדיפולטיים
└── Installer/
    └── Kolsites.iss               # סקריפט Inno Setup
```

## 💾 מיקומי קבצים

- **הגדרות**: `%APPDATA%\Kolsites\settings.json`
- **יומן שגיאות**: `%APPDATA%\Kolsites\errors.log`
- **יומן Watchdog**: `%APPDATA%\Kolsites\watchdog.log`
- **WebView2 UserData**: `%APPDATA%\Kolsites\WebView2\` (מנוקה אוטומטית בעת סגירת הקיוסק)
- **התוכנה**: `C:\Program Files\Kolsites\`
- **משימת Watchdog**: `Task Scheduler → Kolsites Watchdog` (רצה כ-SYSTEM כל דקה)

## 🔧 פתרון בעיות

**"Windows App Runtime לא מותקן"** — הורד והרץ את [המתקין של Microsoft](https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe) ואז הרץ שוב את Kolsites.

**הכפתור הצף לא מופיע מעל אפליקציה במסך מלא** — חלק מהאפליקציות (משחקים, נגני וידאו) חוסמות חלונות topmost. סגור אותן או הוצא אותן ממצב מסך מלא.

**הכפתור הצף הוסתר בטעות** — Watchdog יפעיל מחדש את התוכנה תוך כדקה. אפשר גם להפעיל ידנית מקיצור הדרך.

**שינויים בהגדרות לא נכנסים לתוקף** — סגור את הכפתור הצף (דרך Task Manager — `Kolsites.exe`) והפעל מחדש. הקיוסק קורא את ההגדרות בעת ההפעלה.

**Watchdog מפעיל את התוכנה כל דקה במקום פעם אחת** — בדוק את `%APPDATA%\Kolsites\watchdog.log`. אם רואים "הופעל מופע חדש" שוב ושוב, ייתכן שהתוכנה קורסת בעלייה — בדוק את `errors.log`.

**להסרת ה-Watchdog ידנית**: `schtasks /Delete /TN "Kolsites Watchdog" /F`

## 📚 קישורים

- [Windows App SDK documentation](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [WebView2 documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
- [Inno Setup](https://jrsoftware.org/isinfo.php)
