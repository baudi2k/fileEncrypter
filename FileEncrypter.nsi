; Script NSIS para FileEncrypter Pro
!define PRODUCT_NAME "FileEncrypter Pro"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "FileEncrypter Corp"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\FileEncrypter.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"

; Configuración del instalador
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "FileEncrypter-Setup.exe"
InstallDir "$PROGRAMFILES\FileEncrypter Pro"
ShowInstDetails show
ShowUnInstDetails show

; Páginas del instalador
Page directory
Page instfiles
UninstPage uninstconfirm
UninstPage instfiles

; Sección principal de instalación
Section "MainSection" SEC01
  SetOutPath "$INSTDIR"
  SetOverwrite on
  
  ; Copiar TODOS los archivos de la carpeta publish
  File /r "bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\*.*"
  
  ; Crear accesos directos
  CreateDirectory "$SMPROGRAMS\FileEncrypter Pro"
  CreateShortCut "$SMPROGRAMS\FileEncrypter Pro\FileEncrypter Pro.lnk" "$INSTDIR\FileEncrypter.exe"
  CreateShortCut "$DESKTOP\FileEncrypter Pro.lnk" "$INSTDIR\FileEncrypter.exe"
  
  ; Crear desinstalador
  WriteUninstaller "$INSTDIR\uninst.exe"
  
  ; Escribir información del registro
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\FileEncrypter.exe"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\FileEncrypter.exe"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
SectionEnd

; Sección de desinstalación
Section Uninstall
  ; Eliminar accesos directos
  Delete "$SMPROGRAMS\FileEncrypter Pro\FileEncrypter Pro.lnk"
  Delete "$DESKTOP\FileEncrypter Pro.lnk"
  RMDir "$SMPROGRAMS\FileEncrypter Pro"
  
  ; Eliminar archivos y carpetas
  RMDir /r "$INSTDIR"
  
  ; Limpiar registro
  DeleteRegKey HKLM "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  
  SetAutoClose true
SectionEnd 