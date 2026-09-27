; Установщик Go Engine для Windows (Inno Setup 6).
;
; Собирается в CI на windows-latest: см. .github/workflows/build.yml, шаг «Установщик (Windows)».
; Внутрь кладётся вся публикация win-x64 и каталог models — то есть игра ставится сразу с сетями
; (19×19 и 9×9), без ручного скачивания файлов моделей.
;
; Определения передаются из workflow:
;   MyVersion  — версия в списке установленных программ;
;   MySuffix   — суффикс имени файла: пусто для свежей сборки, «-v0.1.0» для версии;
;   SourceDir  — каталог публикации приложения;
;   ModelsDir  — каталог с файлами моделей.

#ifndef MyVersion
  #define MyVersion "0.1.0"
#endif

#ifndef MySuffix
  #define MySuffix ""
#endif

#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

#ifndef ModelsDir
  #define ModelsDir "..\models"
#endif

#define MyAppName "Go Engine"
#define MyAppExeName "GoEngine.App.Desktop.exe"
#define MyAppPublisher "Go Engine"

[Setup]
; AppId — постоянный идентификатор приложения: по нему Windows понимает, что это обновление,
; а не вторая копия, поэтому его нельзя менять между версиями.
AppId={{7B1F4C2E-9A3D-4E6B-8F21-5C0D7A9E4B31}
AppName={#MyAppName}
AppVersion={#MyVersion}
AppVerName={#MyAppName} {#MyVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist-installer
OutputBaseFilename=GoEngine-Setup{#MySuffix}
SetupIconFile=..\src\GoEngine.App\Assets\goengine.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
; Ставим в профиль пользователя и без запроса прав администратора: для игры этого достаточно,
; а лишний UAC только пугает. Кому нужно — выберет «для всех пользователей» в начале установки.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Приложение целиком: exe, библиотеки Avalonia, SkiaSharp, ONNX Runtime и прочее.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Модели нейросети — в подкаталог models: приложение ищет их рядом с собой.
Source: "{#ModelsDir}\*.onnx"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "{#ModelsDir}\README.md"; DestDir: "{app}\models"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
