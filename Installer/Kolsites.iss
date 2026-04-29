; ============================================================================
;  Kolsites - Inno Setup Installer Script
; ============================================================================
;  לבניה:
;    1. dotnet publish -c Release -r win-x64 (בתיקיית הפרויקט)
;    2. iscc Installer\Kolsites.iss
;    3. Setup ייווצר בתיקיית Release
; ============================================================================

#define AppName "Kolsites"
#define AppVersion "1.2.0"
#define AppPublisher "abaye"
#define AppExeName "Kolsites.exe"

; נתיב הפלט של dotnet publish
#define SourceFolder "..\bin\x64\Release\net8.0-windows10.0.19041.0"

; כתובת ה-Windows App Runtime (לצורך הוראת התקנה אם חסר)
#define WinAppRuntimeUrl "https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe"

[Setup]
AppId={{B1E9C8D3-2A4F-4E5C-9A6B-3F8D1C2E4A7B}
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
    Parameters: "/Create /TN ""Kolsites Watchdog"" /TR ""\""{app}\{#AppExeName}\"" --watchdog"" /SC MINUTE /MO 1 /RU SYSTEM /F"; \
    Flags: runhidden; \
    Tasks: watchdog; \
    StatusMsg: "מתקין משימת Watchdog..."

; הפעלת התוכנה בסיום ההתקנה
Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; הסרת המשימה המתוזמנת
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/Delete /TN ""Kolsites Watchdog"" /F"; \
    Flags: runhidden; \
    RunOnceId: "DeleteWatchdogTask"

; סגירת כל המופעים הפעילים בזמן ההסרה
Filename: "{sys}\taskkill.exe"; \
    Parameters: "/IM {#AppExeName} /F"; \
    Flags: runhidden; \
    RunOnceId: "KillKolsitesProcesses"

[Code]
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
  if NeedsWinAppRuntime then
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
