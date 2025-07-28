using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileEncrypter
{
    public partial class CustomMessageBox : Window
    {
        public enum MessageType
        {
            Error,
            Warning,
            Information,
            Success
        }

        public CustomMessageBox()
        {
            InitializeComponent();
        }

        public static void Show(string message, string title = "FileEncrypter", MessageType type = MessageType.Error, Window? owner = null)
        {
            var messageBox = new CustomMessageBox();
            messageBox.MessageText.Text = message;
            messageBox.TitleText.Text = title;
            
            if (owner != null)
                messageBox.Owner = owner;

            // Configurar icono y colores según el tipo
            var iconContainer = (Border)messageBox.FindName("IconContainer");
            var iconText = (TextBlock)messageBox.FindName("IconText");
            
            if (iconContainer != null && iconText != null)
            {
                switch (type)
                {
                    case MessageType.Error:
                        iconContainer.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Rojo
                        iconText.Text = "!";
                        break;
                    case MessageType.Warning:
                        iconContainer.Background = new SolidColorBrush(Color.FromRgb(241, 196, 15)); // Amarillo
                        iconText.Text = "⚠";
                        break;
                    case MessageType.Information:
                        iconContainer.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Azul
                        iconText.Text = "i";
                        break;
                    case MessageType.Success:
                        iconContainer.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Verde
                        iconText.Text = "✓";
                        break;
                }
            }

            messageBox.ShowDialog();
        }

        public static void ShowPasswordError(Window? owner = null)
        {
            Show(
                "La contraseña ingresada es incorrecta.\n\n" +
                "• Verifique que haya escrito la contraseña correctamente\n" +
                "• Asegúrese de que las mayúsculas y minúsculas coincidan\n" +
                "• Recuerde que la contraseña es sensible a mayúsculas\n\n" +
                "Intente nuevamente con la contraseña correcta.",
                "Contraseña Incorrecta",
                MessageType.Warning,
                owner
            );
        }

        public static void ShowSuccess(string message, string title = "Éxito", Window? owner = null)
        {
            Show(message, title, MessageType.Success, owner);
        }

        public static void ShowError(string message, string title = "Error", Window? owner = null)
        {
            Show(message, title, MessageType.Error, owner);
        }

        public static void ShowWarning(string message, string title = "Advertencia", Window? owner = null)
        {
            Show(message, title, MessageType.Warning, owner);
        }

        public static void ShowInfo(string message, string title = "Información", Window? owner = null)
        {
            Show(message, title, MessageType.Information, owner);
        }

        public static MessageBoxResult ShowQuestion(string message, string title = "Confirmación", Window? owner = null)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 