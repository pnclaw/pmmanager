Unicode True
RequestExecutionLevel admin

!define APP_NAME    "Private Media Manager"
!define SERVICE_NAME "PrivateMediaManager"
!define APP_EXE     "pmm.Api.exe"
!define DATA_DIR    "$COMMONAPPDATA\Private Media Manager"
!define UNINST_REG  "Software\Microsoft\Windows\CurrentVersion\Uninstall\${SERVICE_NAME}"

!ifndef APP_VERSION
  !define APP_VERSION "0.0.0"
!endif

!include "nsDialogs.nsh"
!include "LogicLib.nsh"

Name    "${APP_NAME} ${APP_VERSION}"
OutFile "PrivateMediaManager-${APP_VERSION}-setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
ShowInstDetails show
ShowUninstDetails show

Var PortNum
Var Dialog
Var PortLabel
Var PortInput

; ── Pages ────────────────────────────────────────────────────────────────────
Page custom PortPageCreate PortPageLeave
Page instfiles
UninstPage instfiles

; ── Port page ─────────────────────────────────────────────────────────────────
Function PortPageCreate
  nsDialogs::Create 1018
  Pop $Dialog
  ${If} $Dialog == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 0 100% 20u "HTTP port:"
  Pop $PortLabel

  ${NSD_CreateNumber} 0 25u 80u 14u "8080"
  Pop $PortInput

  nsDialogs::Show
FunctionEnd

Function PortPageLeave
  ${NSD_GetText} $PortInput $PortNum
  ${If} $PortNum == ""
    StrCpy $PortNum "8080"
  ${EndIf}
FunctionEnd

; ── Install ──────────────────────────────────────────────────────────────────
Section "Install"
  ; Stop and remove any previous installation
  nsExec::Exec 'sc stop "${SERVICE_NAME}"'
  Sleep 2000
  nsExec::Exec 'sc delete "${SERVICE_NAME}"'
  Sleep 1000

  SetOutPath "$INSTDIR"
  File /r "publish\*"

  CreateDirectory "${DATA_DIR}"
  CreateDirectory "${DATA_DIR}\logs"

  ; Register Windows Service
  nsExec::ExecToLog 'sc create "${SERVICE_NAME}" binPath= "$INSTDIR\${APP_EXE}" start= auto DisplayName= "${APP_NAME}"'
  nsExec::ExecToLog 'sc description "${SERVICE_NAME}" "${APP_NAME} service"'

  ; Service environment variables (REG_MULTI_SZ — requires NSIS 3.08+)
  WriteRegMultiStr HKLM "SYSTEM\CurrentControlSet\Services\${SERVICE_NAME}" "Environment" \
    "ASPNETCORE_URLS=http://+:$PortNum$\0DB_PATH=${DATA_DIR}\app.db$\0LOGS_PATH=${DATA_DIR}\logs\app-.log$\0ASPNETCORE_ENVIRONMENT=Production"

  nsExec::ExecToLog 'sc start "${SERVICE_NAME}"'

  ; Start Menu shortcut — opens the web UI in the default browser
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  WriteINIStr "$SMPROGRAMS\${APP_NAME}\Open ${APP_NAME}.url" "InternetShortcut" "URL" "http://localhost:$PortNum"

  ; Uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Add/Remove Programs entry
  WriteRegStr   HKLM "${UNINST_REG}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_REG}" "UninstallString"  '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINST_REG}" "InstallLocation"  "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_REG}" "DisplayVersion"   "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_REG}" "Publisher"        "pnclaw"
  WriteRegDWORD HKLM "${UNINST_REG}" "NoModify"         1
  WriteRegDWORD HKLM "${UNINST_REG}" "NoRepair"         1
SectionEnd

; ── Uninstall ────────────────────────────────────────────────────────────────
Section "Uninstall"
  nsExec::Exec 'sc stop "${SERVICE_NAME}"'
  Sleep 2000
  nsExec::Exec 'sc delete "${SERVICE_NAME}"'
  Sleep 1000

  RMDir /r "$INSTDIR"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"

  ; Data in $COMMONAPPDATA\Private Media Manager is intentionally preserved
  DeleteRegKey HKLM "${UNINST_REG}"
SectionEnd
