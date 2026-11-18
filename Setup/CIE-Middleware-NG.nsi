!include "MUI2.nsh"
!include "x64.nsh"
!include "Library.nsh"

!define PRODUCT_VERSION "2.0.0.18"

;--------------------------------
;General

  ;Name and file
  Name "CIE-Middleware-NG"
  OutFile "${Config}\CIE-Middleware-NG.exe"

  !define MUI_ICON "..\CSP\res\CIE.ico"
  !define PRODUCT_UNINST_ROOT_KEY "HKLM"
  !define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\CIEPKI-NG"
  
  
  BrandingText "CIE-Middleware-NG"
  ;Default installation folder
  ; Applicazione a 64 bit: installata in Program Files (non Program Files (x86))
  InstallDir "$PROGRAMFILES64\CIEPKI-NG"

  ;Request application privileges for Windows Vista
  RequestExecutionLevel admin

VIAddVersionKey "ProductName" "CIE-Middleware-NG"
VIAddVersionKey "Comments" "CIE-Middleware-NG"
VIAddVersionKey "CompanyName" ""
VIAddVersionKey "LegalTrademarks" ""
VIAddVersionKey "LegalCopyright" "Copyright (C) CIE-Middleware-NG"
VIAddVersionKey "FileDescription" "CIE-Middleware-NG"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "OriginalFilename" "CIE-Middleware-NG.exe"
VIProductVersion "${PRODUCT_VERSION}"


;--------------------------------
;Interface Configuration

  !define MUI_HEADERIMAGE
  !define MUI_HEADERIMAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Header\nsis.bmp" ; optional
  !define MUI_ABORTWARNING

;--------------------------------
;Pages

  !insertmacro MUI_PAGE_LICENSE "..\License.txt"
  !insertmacro MUI_PAGE_INSTFILES
  
  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES
  
;--------------------------------
;Languages
 
  !insertmacro MUI_LANGUAGE "Italian"

;--------------------------------
;Installer Sections

; Runtime opzionali: se i redistributable sono presenti in Setup\Redist vengono inclusi nell'installer.
; - vc_redist.x64.exe: Microsoft Visual C++ Redistributable 2015-2022 x64 (richiesto da CIEPKI-NG.dll)
; - windowsdesktop-runtime-win-x64.exe: .NET Desktop Runtime 10 x64 (richiesto da CIEID)
!define VCREDIST_FILE "${__FILEDIR__}\Redist\vc_redist.x64.exe"
!define DOTNET_FILE "${__FILEDIR__}\Redist\windowsdesktop-runtime-win-x64.exe"

Section "Runtime"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  !if /FileExists "${VCREDIST_FILE}"
    File "${VCREDIST_FILE}"
    ExecWait '"$PLUGINSDIR\vc_redist.x64.exe" /install /quiet /norestart'
  !else
    !warning "vc_redist.x64.exe non trovato in Setup\Redist: il runtime C++ NON sara' incluso nell'installer"
  !endif
  !if /FileExists "${DOTNET_FILE}"
    File "${DOTNET_FILE}"
    ExecWait '"$PLUGINSDIR\windowsdesktop-runtime-win-x64.exe" /install /quiet /norestart'
  !else
    !warning "windowsdesktop-runtime-win-x64.exe non trovato in Setup\Redist: il runtime .NET NON sara' incluso nell'installer"
  !endif
SectionEnd

Section "Install"

  SetOutPath $SYSDIR
       
  ${If} ${RunningX64}
    ; InstallLib sostituisce la DLL solo se la versione e' piu' recente: rimuovo prima quella
    ; installata, cosi' una ricompilazione con la stessa versione viene comunque installata.
    ${DisableX64FSRedirection}
    ExecWait '"$SYSDIR\regsvr32.exe" /u /s "$SYSDIR\CIEPKI-NG.dll"'
    Delete "$SYSDIR\CIEPKI-NG.dll"
    ${If} ${FileExists} "$SYSDIR\CIEPKI-NG.dll"
      ; DLL caricata da un altro processo: si puo' rinominare ma non cancellare.
      ; Il vecchio file viene eliminato al prossimo riavvio, la nuova DLL e' installata subito.
      Delete "$SYSDIR\CIEPKI-NG.dll.old"
      Rename "$SYSDIR\CIEPKI-NG.dll" "$SYSDIR\CIEPKI-NG.dll.old"
      Delete /REBOOTOK "$SYSDIR\CIEPKI-NG.dll.old"
    ${EndIf}
    ${EnableX64FSRedirection}
    !define LIBRARY_X64
	!echo ${Config}
    !insertmacro InstallLib REGDLL NOTSHARED REBOOT_PROTECTED "..\x64\${Config}\CIEPKI-NG.dll" "$SYSDIR\CIEPKI-NG.dll" $SYSDIR
    !undef LIBRARY_X64
  ${Else}
  ${EndIf}

  # RMDir /r "$LOCALAPPDATA\CIEID"
  # RMDir /r "$%LOCALAPPDATA%\CIE-Middleware-NG"
 
  SetShellVarContext all
  RMDir /r "$SMPROGRAMS\CIE Middleware NG"
  
  # Start Menu
	createDirectory "$SMPROGRAMS\CIE Middleware NG"
	
  ;Create uninstaller
  createDirectory "$INSTDIR"
  ; Cartella dati condivisa (cache, log, config): creata dall'installer con ACL esplicita e proprietario
  ; Administrators, cosi' un utente non privilegiato non puo' crearla in anticipo e controllarla.
  ; SID noti (indipendenti dalla lingua): SYSTEM, Administrators, Users, CREATOR OWNER, ALL APPLICATION PACKAGES
  CreateDirectory "$%PROGRAMDATA%\CIEPKI-NG"
  nsExec::Exec 'icacls "$%PROGRAMDATA%\CIEPKI-NG" /setowner *S-1-5-32-544 /C /Q'
  nsExec::Exec 'icacls "$%PROGRAMDATA%\CIEPKI-NG" /grant *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX *S-1-5-32-545:(AD,WD) *S-1-3-0:(OI)(CI)(IO)F *S-1-15-2-1:(OI)(CI)RX /C /Q'
  nsExec::Exec 'icacls "$%PROGRAMDATA%\CIEPKI-NG" /inheritance:r /C /Q'
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  SetOutPath $INSTDIR    
	createShortCut "$SMPROGRAMS\CIE Middleware NG\Disinstalla.lnk" "$INSTDIR\Uninstall.exe"
	
	
	File "..\x64\${Config}\CIE-Middleware-NG.exe"
	File "..\x64\${Config}\CIE-Middleware-NG.dll"
	File "..\x64\${Config}\CIE-Middleware-NG.dll.config"
	File "..\x64\${Config}\CIE-Middleware-NG.deps.json"
	File "..\x64\${Config}\CIE-Middleware-NG.runtimeconfig.json"
	File "..\x64\${Config}\Newtonsoft.Json.dll"
	File "..\x64\${Config}\System.Management.dll"
		
  SetOutPath $SYSDIR
	
	;createShortCut "$SMPROGRAMS\CIE Middleware NG\Cambio PIN.lnk" "$INSTDIR\CIE-Middleware-NG.exe" 
	;createShortCut "$SMPROGRAMS\CIE Middleware NG\Sblocco PIN.lnk" "$INSTDIR\CIE-Middleware-NG.exe" 
	createShortCut "$SMPROGRAMS\CIE Middleware NG\CIE Middleware NG.lnk" "$INSTDIR\CIE-Middleware-NG.exe"

	;createShortCut "$SMPROGRAMS\CIE Middleware NG\Cambio PIN.lnk" "$SYSDIR\rundll32.exe" "CIEPKI-NG.dll CambioPIN" "$SYSDIR\CIEPKI-NG.dll" 0
	;createShortCut "$SMPROGRAMS\CIE Middleware NG\Sblocco PIN.lnk" "$SYSDIR\rundll32.exe" "CIEPKI-NG.dll SbloccoPIN" "$SYSDIR\CIEPKI-NG.dll" 0

  SetOutPath $INSTDIR
	
  ${If} ${RunningX64}	
	SetRegView 64
  ${EndIf}
  WriteRegStr HKLM "Software\CIEPKI-NG" "" $INSTDIR
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "CIE Middleware NG"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$SYSDIR\CIEPKI-NG.dll,0"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "CIE-Middleware-NG"
  WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoModify" "1"
  ;WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "VersionMajor" "0"
  ;WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "VersionMinor" "1"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  SetRegView lastused

  
  IfRebootFlag 0 noreboot
  MessageBox MB_YESNO "Per completare l'installazione e' necessario il riavvio del computer. Riavviare adesso?" IDNO noreboot
  Reboot
  noreboot:
SectionEnd

;--------------------------------
;Uninstaller Section

Section "Uninstall"

  ;ADD YOUR OWN FILES HERE...

  ${If} ${RunningX64}	
    !define LIBRARY_X64
    !insertmacro UnInstallLib REGDLL NOTSHARED REBOOT_PROTECTED "$SYSDIR\CIEPKI-NG.dll"
    !undef LIBRARY_X64

    ${DisableX64FSRedirection}
    Delete "$SYSDIR\CIEPKI-NG.dll"
    ${EnableX64FSRedirection}

    !insertmacro UnInstallLib REGDLL NOTSHARED REBOOT_PROTECTED "$SYSDIR\CIEPKI-NG.dll"
	Delete "$SYSDIR\CIEPKI-NG.dll"
  ${Else}
    !insertmacro UnInstallLib REGDLL NOTSHARED REBOOT_PROTECTED "$SYSDIR\CIEPKI-NG.dll"
	Delete "$SYSDIR\CIEPKI-NG.dll"
  ${EndIf}
  
  ;!echo $%LOCALAPPDATA%

  ; La disinstallazione ripulisce tutto, compresi i dati dell'utente (elenco CIE e cache).
  RMDir /r "$LOCALAPPDATA\CIE-Middleware-NG"
  ReadEnvStr $0 PROGRAMDATA
  ${If} $0 != ""
    RMDir /r "$0\CIEPKI-NG"
  ${EndIf}
  Delete "$INSTDIR\CIE-Middleware-NG.exe"
  Delete "$INSTDIR\CIE-Middleware-NG.dll"
  Delete "$INSTDIR\CIE-Middleware-NG.dll.config"
  Delete "$INSTDIR\CIE-Middleware-NG.deps.json"
  Delete "$INSTDIR\CIE-Middleware-NG.runtimeconfig.json"
  Delete "$INSTDIR\Newtonsoft.Json.dll"
  Delete "$INSTDIR\System.Management.dll"
  Delete "$INSTDIR\UpdateTask.xml"
  ; file di versioni precedenti dell'installer
  Delete "$INSTDIR\CIE-Middleware-NG.exe.config"
  Delete "$INSTDIR\Newtonsoft.Json.xml"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  

  ${If} ${RunningX64}	
	SetRegView 64
  ${EndIf}
  DeleteRegKey HKLM "Software\CIEPKI-NG"
  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  SetRegView lastused

  SetShellVarContext all
  RMDir /r "$SMPROGRAMS\CIE Middleware NG"


  IfRebootFlag 0 noreboot2
  MessageBox MB_YESNO "Per completare l'installazione e' necessario il riavvio del computer. Riavviare adesso?" IDNO noreboot2
  Reboot
  noreboot2:
SectionEnd