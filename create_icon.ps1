# Script para crear un icono simple para FileEncrypter
# Ejecutar en PowerShell como administrador si es necesario

Write-Host "=== Creador de Icono para FileEncrypter ===" -ForegroundColor Cyan
Write-Host ""

# Verificar si tenemos las herramientas necesarias
$iconPath = "C:\Users\baudi\Desktop\fileEncrypter\app_icon.ico"

Write-Host "Opciones para crear un icono:" -ForegroundColor Yellow
Write-Host "1. Descargar un icono de internet (recomendado)"
Write-Host "2. Usar el icono por defecto de Windows"
Write-Host "3. Crear manualmente más tarde"
Write-Host ""

# Opción 1: URL de iconos gratuitos
Write-Host "📥 OPCIÓN 1 - Descargar icono:" -ForegroundColor Green
Write-Host "Puedes descargar iconos gratuitos de:"
Write-Host "• https://www.flaticon.com (busca 'encryption' o 'security')"
Write-Host "• https://icons8.com (busca 'lock' o 'security')"
Write-Host "• https://www.iconfinder.com (busca 'encryption')"
Write-Host ""
Write-Host "Descarga un archivo .ico de 64x64 o superior y guárdalo como:"
Write-Host $iconPath -ForegroundColor Cyan
Write-Host ""

# Opción 2: Copiar icono del sistema
Write-Host "🔧 OPCIÓN 2 - Usar icono del sistema:" -ForegroundColor Green
$systemIcon = "C:\Windows\System32\imageres.dll"
if (Test-Path $systemIcon) {
    Write-Host "Puedes extraer iconos del sistema usando herramientas como:"
    Write-Host "• IconsExtract (gratuito)"
    Write-Host "• Resource Hacker (gratuito)"
    Write-Host "Archivo fuente: $systemIcon"
} else {
    Write-Host "No se encontró el archivo de iconos del sistema."
}
Write-Host ""

# Opción 3: Información sobre crear manualmente
Write-Host "✏️ OPCIÓN 3 - Crear manualmente:" -ForegroundColor Green
Write-Host "Puedes crear un icono usando:"
Write-Host "• GIMP (gratuito) - Exportar como .ico"
Write-Host "• Paint.NET con plugin ICO (gratuito)"
Write-Host "• Canva.com (online, gratuito)"
Write-Host "• Adobe Illustrator/Photoshop (de pago)"
Write-Host ""

Write-Host "📋 Especificaciones recomendadas:" -ForegroundColor Yellow
Write-Host "• Tamaño: 64x64 píxeles (mínimo) o 256x256 (recomendado)"
Write-Host "• Formato: .ico"
Write-Host "• Tema: Candado, escudo, llave, o símbolo de seguridad"
Write-Host "• Colores: Azul (#3B82F6) y blanco para consistencia con la app"
Write-Host ""

Write-Host "🎯 Una vez que tengas el icono:" -ForegroundColor Magenta
Write-Host "1. Guárdalo como: $iconPath"
Write-Host "2. Agrega esta línea al archivo FileEncrypter.csproj:"
Write-Host "   <ApplicationIcon>app_icon.ico</ApplicationIcon>" -ForegroundColor Gray
Write-Host "3. Recompila el proyecto"
Write-Host ""

Write-Host "✅ Por ahora, la aplicación funcionará perfectamente sin icono." -ForegroundColor Green
Write-Host "Las notificaciones usarán el icono por defecto del sistema." -ForegroundColor Green
Write-Host ""

Read-Host "Presiona Enter para continuar"
