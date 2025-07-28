# 🔐 FileEncrypter Pro

Una aplicación moderna y segura para encriptar archivos con AES-256, desarrollada en WPF con .NET 8. Protege tus archivos importantes con encriptación de nivel militar y un sistema de recuperación único basado en frases mnemónicas.


## ✨ Características Principales

### 🛡️ Seguridad Avanzada
- **Encriptación AES-256**: Algoritmo de encriptación estándar militar
- **Frases de Recuperación**: Sistema único de 12 palabras para recuperar archivos
- **Derivación de Claves PBKDF2**: 100,000 iteraciones con SHA-256
- **Validación de Integridad**: Verificación automática de archivos

### 🎯 Funcionalidades
- **Drag & Drop**: Arrastra archivos directamente para encriptar/desencriptar
- **Generador de Contraseñas**: Crea contraseñas seguras con análisis de fortaleza
- **Historial Completo**: Rastrea todas las operaciones con estadísticas
- **Notificaciones Nativas**: Integración con el sistema de notificaciones de Windows
- **Interfaz Intuitiva**: Diseño moderno con modo oscuro

### 💼 Gestión de Archivos
- **Compatibilidad Universal**: Funciona con cualquier tipo de archivo
- **Preservación de Nombres**: Mantiene metadatos del archivo original
- **Múltiples Métodos**: Encripta con contraseña o desencripta con frase de recuperación
- **Operaciones Asíncronas**: Procesamiento no bloqueante con progreso visual

## 🚀 Instalación

### Requisitos del Sistema
- Windows 10/11 (versión 1809 o superior)
- .NET 8.0 Runtime
- 50 MB de espacio libre

### Descarga
1. Ve a la sección [Releases](https://github.com/tu-usuario/fileencrypter/releases)
2. Descarga la última versión disponible
3. Ejecuta el instalador y sigue las instrucciones

### Compilación desde Código Fuente
```bash
# Clonar el repositorio
git clone https://github.com/tu-usuario/fileencrypter.git
cd fileencrypter

# Restaurar dependencias
dotnet restore

# Compilar el proyecto
dotnet build --configuration Release

# Ejecutar la aplicación
dotnet run
```

## 📖 Uso

### Encriptar Archivos
1. **Método 1 - Drag & Drop**: Arrastra un archivo a la zona de encriptación
2. **Método 2 - Botón**: Haz clic en "Seleccionar Archivo" y elige tu archivo
3. Ingresa una contraseña segura (utiliza el generador si necesitas ayuda)
4. Haz clic en "Encriptar Archivo"
5. **¡IMPORTANTE!** Guarda la frase de recuperación de 12 palabras en un lugar seguro

### Desencriptar Archivos
1. Arrastra un archivo `.enc` a la zona de desencriptación
2. Elige tu método de desencriptación:
   - **Contraseña**: Usa la contraseña original
   - **Frase de Recuperación**: Usa las 12 palabras generadas
3. Haz clic en "Desencriptar Archivo"

### Ver Historial
- Haz clic en "Historial" para ver todas tus operaciones
- Consulta estadísticas de archivos encriptados y desencriptados
- Elimina entradas individuales o limpia todo el historial

## 🔧 Características Técnicas

### Algoritmos de Seguridad
- **Encriptación**: AES-256-CBC
- **Derivación de Claves**: PBKDF2 con SHA-256 (100,000 iteraciones)
- **Generación de Salt**: RandomNumberGenerator criptográficamente seguro
- **Frases de Recuperación**: Basadas en wordlist española (similar a BIP39)

### Arquitectura
- **Frontend**: WPF con XAML
- **Backend**: .NET 8.0
- **Patrones**: MVVM, Services, Async/Await
- **Almacenamiento**: JSON local con identificación por máquina

### Estructura del Proyecto
```
FileEncrypter/
├── Services/           # Lógica de negocio
│   ├── EncryptionService.cs
│   ├── HistoryService.cs
│   └── NotificationService.cs
├── Helpers/           # Utilidades
│   ├── HashHelper.cs
│   ├── PasswordHelper.cs
│   ├── RecoveryPhraseHelper.cs
│   └── MachineIdentifierHelper.cs
├── Models/            # Modelos de datos
├── Themes/            # Estilos XAML
└── Windows/           # Ventanas adicionales
```

## 🛡️ Seguridad

### Buenas Prácticas Implementadas
- Las contraseñas nunca se almacenan en texto plano
- Los salt son únicos para cada archivo
- Las claves se derivan usando estándares industriales
- Los archivos originales pueden ser eliminados de forma segura
- Validación de integridad en cada operación

### Recomendaciones de Uso
- **Usa contraseñas fuertes**: Mínimo 12 caracteres con mezcla de tipos
- **Guarda la frase de recuperación**: Es tu única alternativa si olvidas la contraseña
- **Mantén backups**: De archivos importantes antes de encriptar
- **Actualiza regularmente**: Para obtener mejoras de seguridad

## 📊 Estadísticas del Proyecto

![GitHub release (latest by date)](https://img.shields.io/github/v/release/tu-usuario/fileencrypter)
![GitHub](https://img.shields.io/github/license/tu-usuario/fileencrypter)
![GitHub top language](https://img.shields.io/github/languages/top/tu-usuario/fileencrypter)
![GitHub code size in bytes](https://img.shields.io/github/languages/code-size/tu-usuario/fileencrypter)

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Si quieres mejorar FileEncrypter:

1. Fork el proyecto
2. Crea una rama para tu característica (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abre un Pull Request

### Áreas donde Puedes Ayudar
- 🐛 Reportar bugs
- 💡 Sugerir nuevas características
- 🌍 Traducciones a otros idiomas
- 📖 Mejorar documentación
- 🧪 Escribir tests
- 🎨 Mejorar la interfaz

## 📝 Changelog

### [v2.0.0] - 2024-XX-XX
- ✨ Sistema de frases de recuperación de 12 palabras
- 🔄 Compatibilidad con archivos legacy
- 🎨 Interfaz rediseñada con modo oscuro
- 📊 Dashboard de historial mejorado
- 🔔 Notificaciones nativas de Windows

### [v1.0.0] - 2024-XX-XX
- 🚀 Lanzamiento inicial
- 🔐 Encriptación AES-256 básica
- 💻 Interfaz WPF moderna
- 📱 Generador de contraseñas

## 📄 Licencia

Este proyecto está licenciado bajo la Licencia MIT - ve el archivo [LICENSE](LICENSE) para detalles.

## 🙏 Agradecimientos

- **Microsoft** por .NET y WPF
- **Comunidad de Criptografía** por los estándares de seguridad
- **Contribuidores** que han mejorado el proyecto
- **Usuarios** por su feedback y pruebas

## 📞 Soporte

¿Necesitas ayuda? Tenemos varias opciones:

- 📋 [Issues de GitHub](https://github.com/tu-usuario/fileencrypter/issues) - Para bugs y sugerencias
- 💬 [Discussions](https://github.com/tu-usuario/fileencrypter/discussions) - Para preguntas generales
- 📧 Email: support@fileencrypter.com
- 🌐 Website: [fileencrypter.com](https://fileencrypter.com)

## ⚠️ Disclaimer

FileEncrypter es una herramienta de encriptación diseñada para proteger archivos personales. Aunque utiliza algoritmos estándar de la industria, no garantizamos protección absoluta contra todos los tipos de ataques. Usa bajo tu propio riesgo y mantén siempre backups de archivos importantes.

---

<div align="center">

**Hecho con ❤️ para la comunidad de código abierto**

[⭐ Dale una estrella si te gusta el proyecto](https://github.com/tu-usuario/fileencrypter)

</div>
