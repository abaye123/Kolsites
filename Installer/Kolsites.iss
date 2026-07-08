; ============================================================================
;  Kolsites - Inno Setup Installer Script
; ============================================================================
;  לבניה:
;    1. dotnet publish -c Release -r win-x64 (בתיקיית הפרויקט)
;    2. iscc Installer\Kolsites.iss
;    3. Setup ייווצר בתיקיית Release
; ============================================================================

#define AppName "Kolsites"
#ifndef AppVersion
  #define AppVersion "1.7.0"
#endif
#define AppPublisher "abaye"
#define AppExeName "Kolsites.exe"

; שם המשימה המתוזמנת של ה-Watchdog - חייב להתאים ל-WatchdogService.cs ולסעיף [Run]
#define WatchdogTaskName "Kolsites Watchdog"

; נתיב הפלט של dotnet publish
#define SourceFolder "..\bin\x64\Release\net8.0-windows10.0.19041.0"

; כתובת ה-Windows App Runtime (לצורך הוראת התקנה אם חסר)
#define WinAppRuntimeUrl "https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe"

[Setup]
AppId={{B1E9C8D3-2A4F-4E5C-9A6B-3F8D1C2E4A7B}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL=https://github.com/abaye123/Kolsites
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\Release
OutputBaseFilename=Kolsites-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=".\license.txt"
InfoAfterFile=".\thanks.txt"
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\Assets\AppIcon.ico
ShowLanguageDialog=auto
; ה-Restart Manager יסגור כל תהליך שמחזיק קבצים ב-{app}. אנחנו מפעילים מחדש בעצמנו
; מסעיף [Run], ולכן Setup לא צריך להפעיל מחדש (מונע הפעלה כפולה).
CloseApplications=yes
RestartApplications=no
; מיוטקס גלובלי שה-Watchdog בודק כדי לא להפעיל את Kolsites באמצע התקנה/עדכון.
; חייב להתאים ל-WatchdogService.SetupMutexName.
SetupMutex=Kolsites_Setup_Mutex,Global\Kolsites_Setup_Mutex

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"

[Tasks]
Name: "desktopicon";   Description: "צור קיצור דרך לכפתור הצף על שולחן העבודה"; GroupDescription: "קיצורי דרך נוספים:"
Name: "settingsicon";  Description: "צור קיצור דרך להגדרות על שולחן העבודה"; GroupDescription: "קיצורי דרך נוספים:"
Name: "autostart";     Description: "הפעל את Kolsites אוטומטית בעלייה (מומלץ לעמדת קיוסק)"; GroupDescription: "אוטומציה:"
Name: "watchdog";      Description: "התקן Watchdog שיפעיל את התוכנה מחדש אם תיסגר (מומלץ)"; GroupDescription: "אוטומציה:"

[Files]
Source: "{#SourceFolder}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; קיצור דרך ראשי - מפעיל את הכפתור הצף
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
; קיצור דרך נפרד להגדרות
Name: "{group}\הגדרות {#AppName}"; Filename: "{app}\{#AppExeName}"; Parameters: "--settings"; IconFilename: "{app}\Assets\AppIcon.ico"; Comment: "פתח את חלון ההגדרות של Kolsites"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"

; שולחן עבודה - לפי בחירת המשתמש
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon
Name: "{autodesktop}\הגדרות {#AppName}"; Filename: "{app}\{#AppExeName}"; Parameters: "--settings"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: settingsicon

[Registry]
; הפעלה אוטומטית בעלייה (לכל המשתמשים)
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Kolsites"; ValueData: """{app}\{#AppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; יצירת המשימה המתוזמנת ל-Watchdog (כל דקה - בודקת ומפעילה אם צריך)
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/Create /TN ""{#WatchdogTaskName}"" /TR ""\""{app}\{#AppExeName}\"" --watchdog"" /SC MINUTE /MO 1 /RU SYSTEM /F"; \
    Flags: runhidden; \
    Tasks: watchdog; \
    StatusMsg: "מתקין משימת Watchdog..."

; הפעלת התוכנה בסיום התקנה אינטראקטיבית (המשתמש לוחץ סיום)
Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

; הפעלה אוטומטית בסיום התקנה שקטה (עדכון מתוך התוכנה). בלי זה, אחרי עדכון שקט
; התוכנה נשארת סגורה עד שה-Watchdog יקפיץ אותה (או לצמיתות, אם הוא לא הותקן).
; runasoriginaluser מפעיל כמשתמש שיזם את העדכון, ולא כ-Admin.
Filename: "{app}\{#AppExeName}"; \
    Flags: nowait runasoriginaluser; \
    Check: WizardSilent

[UninstallRun]
; הסרת המשימה המתוזמנת
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/Delete /TN ""{#WatchdogTaskName}"" /F"; \
    Flags: runhidden; \
    RunOnceId: "DeleteWatchdogTask"

; סגירת כל המופעים הפעילים בזמן ההסרה
Filename: "{sys}\taskkill.exe"; \
    Parameters: "/IM {#AppExeName} /F"; \
    Flags: runhidden; \
    RunOnceId: "KillKolsitesProcesses"

[Code]
// ============================================================================
//  סגירת התוכנה לפני החלפת הקבצים (קריטי לעדכון האוטומטי)
// ----------------------------------------------------------------------------
//  כשהעדכון מופעל מתוך התוכנה, חלון ההגדרות מריץ את המתקין ואז סוגר את עצמו -
//  אבל תהליך הקיוסק (אותו Kolsites.exe) ממשיך לרוץ ומחזיק את הקבצים ב-{app}
//  נעולים, ובנוסף משימת ה-Watchdog מפעילה מופע חדש כל דקה. התוצאה: העתקת
//  הקבצים נכשלת, ועם /NORESTART העדכון נדחה בשקט לאתחול הבא.
//
//  הפתרון, לפי הסדר:
//    1. משביתים את משימת ה-Watchdog כדי שלא תקפיץ מופע חדש באמצע ההתקנה.
//    2. הורגים את כל התהליכים של Kolsites.exe (כולל מופעים תקועים).
//    3. ממתינים עד שה-Mutexes של הקיוסק/ההגדרות משתחררים - סימן שהתהליכים
//       נעלמו לגמרי והקבצים כבר לא נעולים.
//  המשימה מוחזרת לפעולה ב-ssPostInstall, אחרי שהקבצים הוחלפו.
//  שמות ה-Mutex חייבים להתאים ל-Program.cs.
// ============================================================================
const
  KioskMutexName    = 'Kolsites_Kiosk_{8F3A2C7E-4B5D-4F1A-9C8E-3D2B1A5F6E7D}';
  SettingsMutexName = 'Kolsites_Settings_{2C8A1F4D-9B5E-4F1A-B3C7-1D2E3F4A5B6C}';

var
  WatchdogDisabledByUs: Boolean;

function RunSchTasks(const Params: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\schtasks.exe'), Params, '',
                 SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure KillRunningApp;
var
  ResultCode: Integer;
begin
  // taskkill ולא רק Restart Manager: חלון קיוסק במסך מלא לא בהכרח נסגר ב-WM_CLOSE,
  // ומופע שהופעל ע"י ה-Watchdog תחת SYSTEM לא נראה כלל ל-RM של סשן המשתמש.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExeName} /F', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function AppIsRunning: Boolean;
begin
  Result := CheckForMutexes(KioskMutexName) or CheckForMutexes(SettingsMutexName);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  I: Integer;
begin
  Result := '';

  if RunSchTasks('/Query /TN "{#WatchdogTaskName}"') then
    WatchdogDisabledByUs := RunSchTasks('/Change /TN "{#WatchdogTaskName}" /DISABLE');

  KillRunningApp;

  // המתנה של עד ~10 שניות לשחרור ה-Mutexes, עם ניסיון kill חוזר כל 2 שניות.
  for I := 1 to 40 do
  begin
    if not AppIsRunning then
      Exit;
    if (I mod 8) = 0 then
      KillRunningApp;
    Sleep(250);
  end;

  // חלף הזמן: ממשיכים בכל זאת ונותנים ל-Restart Manager / החלפה-באתחול לטפל
  // במקרה הנדיר שנתקע, במקום להפיל את העדכון השקט.
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // החזרת ה-Watchdog לפעולה אחרי שהקבצים הוחלפו וסעיף [Run] כבר רץ. אם המשימה
  // נוצרה מחדש (Tasks: watchdog) היא ממילא פעילה, והפקודה פשוט לא תשנה דבר.
  if (CurStep = ssPostInstall) and WatchdogDisabledByUs then
    RunSchTasks('/Change /TN "{#WatchdogTaskName}" /ENABLE');
end;

function NeedsWinAppRuntime: Boolean;
var
  SubKey: string;
begin
  Result := True;
  SubKey := 'Software\Microsoft\WindowsAppRuntime\Packages';
  if RegKeyExists(HKLM, SubKey) or RegKeyExists(HKCU, SubKey) then
    Result := False;
end;

procedure InitializeWizard;
begin
  // האזהרה על Windows App Runtime חסרה - מוצגת רק במצב אינטראקטיבי.
  // במצב /SILENT או /VERYSILENT (עדכון אוטומטי) - דילוג מלא, כדי שלא לחסום את ההתקנה.
  // המתקין אוטומטי מפעיל מתוך תוכנה רצה => כבר יש Runtime במחשב, אחרת לא היינו יכולים לרוץ.
  if NeedsWinAppRuntime and not WizardSilent then
  begin
    MsgBox(
      'שים לב:' + #13#10 + #13#10 +
      'נראה ש-Windows App Runtime 1.7 לא מותקן במחשב.' + #13#10 +
      'התוכנה דורשת אותו כדי לפעול.' + #13#10 + #13#10 +
      'ניתן להוריד אותו מהכתובת:' + #13#10 +
      '{#WinAppRuntimeUrl}' + #13#10 + #13#10 +
      'התקנה ללא ה-Runtime תכשיל בהפעלה הראשונה.',
      mbInformation, MB_OK);
  end;
end;
