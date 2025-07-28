using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using Windows.UI.Notifications;

namespace FileEncrypter.Services
{
    /// <summary>
    /// Servicio para manejar las notificaciones nativas de Windows
    /// </summary>
    public static class NotificationService
    {
        private const string APP_ID = "FileEncrypter.Pro";
        private const int DEFAULT_EXPIRATION_MINUTES = 10;
        
        /// <summary>
        /// Inicializa el servicio de notificaciones
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Configurar el identificador de la aplicación para notificaciones
                ToastNotificationManagerCompat.OnActivated += OnNotificationActivated;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inicializando notificaciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Maneja cuando el usuario hace clic en una notificación
        /// </summary>
        private static void OnNotificationActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                var args = ToastArguments.Parse(e.Argument);
                
                // Obtener la acción del argumento
                if (args.TryGetValue("action", out string? action))
                {
                    HandleNotificationAction(action, args);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error manejando activación de notificación: {ex.Message}");
            }
        }

        /// <summary>
        /// Maneja las acciones de las notificaciones
        /// </summary>
        private static void HandleNotificationAction(string action, ToastArguments args)
        {
            switch (action)
            {
                case "openFile":
                    if (args.TryGetValue("filePath", out string? filePath) && File.Exists(filePath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                        catch { /* Error silencioso al abrir archivo */ }
                    }
                    break;
                    
                case "openFolder":
                    if (args.TryGetValue("folderPath", out string? folderPath) && Directory.Exists(folderPath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", folderPath);
                        }
                        catch { /* Error silencioso al abrir carpeta */ }
                    }
                    break;
                    
                case "copyRecoveryPhrase":
                    if (args.TryGetValue("recoveryPhrase", out string? phrase))
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(phrase);
                            ShowInfo("Frase de recuperación copiada", "La frase de recuperación se ha copiado al portapapeles.");
                        }
                        catch { /* Error silencioso al copiar */ }
                    }
                    break;
            }
        }

        /// <summary>
        /// Muestra una notificación de éxito para encriptación
        /// </summary>
        public static void ShowEncryptionSuccess(string fileName, string outputPath, string? recoveryPhrase = null)
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "encryptionComplete")
                .AddText("Archivo Encriptado Exitosamente")
                .AddText($"Archivo: {fileName}")
                .AddText("Tu archivo ha sido protegido con encriptación AES-256")
                .SetToastScenario(ToastScenario.Default);

            // Agregar botones
            builder.AddButton(new ToastButton()
                .SetContent("Abrir Carpeta")
                .AddArgument("action", "openFolder")
                .AddArgument("folderPath", Path.GetDirectoryName(outputPath) ?? ""));

            // Agregar botón para copiar frase de recuperación si está disponible
            if (!string.IsNullOrEmpty(recoveryPhrase))
            {
                builder.AddButton(new ToastButton()
                    .SetContent("Copiar Frase")
                    .AddArgument("action", "copyRecoveryPhrase")
                    .AddArgument("recoveryPhrase", recoveryPhrase)
                    .SetBackgroundActivation());
            }

            ShowNotification(builder, "encryption-success", "completed", DEFAULT_EXPIRATION_MINUTES);
        }

        /// <summary>
        /// Muestra una notificación de éxito para desencriptación
        /// </summary>
        public static void ShowDecryptionSuccess(string fileName, string outputPath)
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "decryptionComplete")
                .AddText("Archivo Desencriptado Exitosamente")
                .AddText($"Archivo: {fileName}")
                .AddText("Tu archivo ha sido restaurado correctamente")
                .SetToastScenario(ToastScenario.Default);

            // Agregar botones
            builder.AddButton(new ToastButton()
                .SetContent("Abrir Carpeta")
                .AddArgument("action", "openFolder")
                .AddArgument("folderPath", Path.GetDirectoryName(outputPath) ?? ""));

            builder.AddButton(new ToastButton()
                .SetContent("Abrir Archivo")
                .AddArgument("action", "openFile")
                .AddArgument("filePath", outputPath));

            ShowNotification(builder, "decryption-success", "completed", DEFAULT_EXPIRATION_MINUTES);
        }

        /// <summary>
        /// Muestra una notificación de progreso para operaciones largas
        /// </summary>
        public static void ShowProgressNotification(string operation, string fileName, double progress)
        {
            var progressValue = Math.Max(0, Math.Min(100, progress));
            
            var builder = new ToastContentBuilder()
                .AddArgument("action", "progressUpdate")
                .AddText($"{operation} en Progreso")
                .AddText($"Archivo: {fileName}")
                .AddProgressBar(
                    title: $"Procesando {fileName}...",
                    value: progressValue / 100.0,
                    isIndeterminate: false,
                    valueStringOverride: $"{progressValue:F1}%",
                    status: "Procesando...")
                .SetToastScenario(ToastScenario.Default);

            ShowNotification(builder, "operation-progress", "progress", 1);
        }

        /// <summary>
        /// Actualiza una notificación de progreso existente
        /// </summary>
        public static void UpdateProgressNotification(string fileName, double progress)
        {
            try
            {
                var progressValue = Math.Max(0, Math.Min(100, progress));
                var progressData = new NotificationData();
                progressData.Values["defaultProgressBar"] = (progressValue / 100.0).ToString();
                progressData.Values["progressValueString"] = $"{progressValue:F1}%";
                progressData.Values["progressStatus"] = $"Procesando {fileName}...";
                
                ToastNotificationManagerCompat.CreateToastNotifier()
                    .Update(progressData, "operation-progress");
            }
            catch
            {
                // Error silencioso al actualizar progreso
            }
        }

        /// <summary>
        /// Muestra una notificación de error
        /// </summary>
        public static void ShowError(string title, string message)
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "error")
                .AddText($"Error: {title}")
                .AddText(message)
                .AddText("Verifica la configuración e intenta nuevamente")
                .SetToastScenario(ToastScenario.Alarm);

            ShowNotification(builder, "error", "errors", 5);
        }

        /// <summary>
        /// Muestra una notificación de advertencia
        /// </summary>
        public static void ShowWarning(string title, string message)
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "warning")
                .AddText($"Advertencia: {title}")
                .AddText(message)
                .SetToastScenario(ToastScenario.Default);

            ShowNotification(builder, "warning", "warnings", 3);
        }

        /// <summary>
        /// Muestra una notificación informativa
        /// </summary>
        public static void ShowInfo(string title, string message)
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "info")
                .AddText($"Info: {title}")
                .AddText(message)
                .SetToastScenario(ToastScenario.Default);

            ShowNotification(builder, "info", "information", 2);
        }

        /// <summary>
        /// Muestra una notificación especial para la frase de recuperación
        /// </summary>
        public static void ShowRecoveryPhraseReminder(string recoveryPhrase)
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "recoveryReminder")
                .AddText("Frase de Recuperación Generada")
                .AddText("Se ha generado una frase de recuperación para tu archivo")
                .AddText("¡Guárdala en un lugar seguro!")
                .SetToastScenario(ToastScenario.Reminder);

            builder.AddButton(new ToastButton()
                .SetContent("Copiar Frase")
                .AddArgument("action", "copyRecoveryPhrase")
                .AddArgument("recoveryPhrase", recoveryPhrase)
                .SetBackgroundActivation());

            ShowNotification(builder, "recovery-phrase", "security", 15);
        }

        /// <summary>
        /// Limpia todas las notificaciones de la aplicación
        /// </summary>
        public static void ClearAllNotifications()
        {
            try
            {
                ToastNotificationManagerCompat.History.Clear();
            }
            catch
            {
                // Error silencioso al limpiar notificaciones
            }
        }

        /// <summary>
        /// Limpia notificaciones de un grupo específico
        /// </summary>
        public static void ClearNotificationGroup(string group)
        {
            try
            {
                ToastNotificationManagerCompat.History.RemoveGroup(group);
            }
            catch
            {
                // Error silencioso al limpiar grupo
            }
        }

        /// <summary>
        /// Método privado para mostrar notificaciones
        /// </summary>
        private static void ShowNotification(ToastContentBuilder builder, string tag, string group, 
            int expirationMinutes = DEFAULT_EXPIRATION_MINUTES)
        {
            try
            {
                builder.Show(toast =>
                {
                    toast.Tag = tag;
                    toast.Group = group;
                    toast.ExpirationTime = DateTime.Now.AddMinutes(expirationMinutes);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error mostrando notificación: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene la URI del icono de la aplicación
        /// </summary>
        private static Uri GetAppIconUri()
        {
            try
            {
                // Intentar usar el icono de la aplicación si existe
                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var iconPath = Path.Combine(appDir ?? "", "app_icon.png");
                
                if (File.Exists(iconPath))
                {
                    return new Uri($"file:///{iconPath}");
                }
                
                // Fallback a un icono genérico del sistema
                return new Uri("ms-appx:///Assets/app_icon.png");
            }
            catch
            {
                // En caso de error, usar un URI por defecto
                return new Uri("ms-appx:///Assets/StoreLogo.png");
            }
        }

        /// <summary>
        /// Verifica si las notificaciones están habilitadas
        /// </summary>
        public static bool AreNotificationsEnabled()
        {
            try
            {
                var notifier = ToastNotificationManagerCompat.CreateToastNotifier();
                return notifier.Setting == NotificationSetting.Enabled;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Limpia recursos cuando se cierra la aplicación
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                // Limpiar notificaciones de progreso al cerrar
                ClearNotificationGroup("progress");
                
                // Opcional: limpiar todas las notificaciones al cerrar
                // ClearAllNotifications();
            }
            catch
            {
                // Error silencioso durante limpieza
            }
        }
    }

    /// <summary>
    /// Tipos de notificación para organización
    /// </summary>
    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info,
        Progress
    }
}
