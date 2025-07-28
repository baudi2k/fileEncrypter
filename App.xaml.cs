using System.Windows;
using FileEncrypter.Services;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FileEncrypter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Inicializar el servicio de notificaciones
            NotificationService.Initialize();
            
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Limpiar notificaciones al cerrar
            NotificationService.Cleanup();
            
            base.OnExit(e);
        }

        /// <summary>
        /// Maneja la activación desde notificaciones (cuando el usuario hace clic en una notificación)
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            // Traer la ventana principal al frente si ya está abierta
            if (Current.MainWindow != null)
            {
                Current.MainWindow.WindowState = WindowState.Normal;
                Current.MainWindow.Activate();
                Current.MainWindow.Topmost = true;
                Current.MainWindow.Topmost = false;
            }
            
            base.OnActivated(e);
        }
    }
}
