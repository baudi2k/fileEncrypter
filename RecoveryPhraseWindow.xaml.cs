using FileEncrypter.Helpers;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace FileEncrypter
{
    public partial class RecoveryPhraseWindow : Window
    {
        private string _recoveryPhrase;

        public RecoveryPhraseWindow(string recoveryPhrase)
        {
            InitializeComponent();
            _recoveryPhrase = recoveryPhrase;
            DisplayRecoveryPhrase();
        }

        private void DisplayRecoveryPhrase()
        {
            if (!string.IsNullOrEmpty(_recoveryPhrase))
            {
                RecoveryPhraseDisplay.Text = RecoveryPhraseHelper.FormatPhraseForDisplay(_recoveryPhrase);
            }
        }

        private void CopyRecoveryPhrase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_recoveryPhrase);
                CustomMessageBox.ShowSuccess("Frase de recuperación copiada al portapapeles.", "Copiado", this);
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error al copiar: {ex.Message}", "Error", this);
            }
        }

        private void SaveRecoveryPhrase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Guardar Frase de Recuperación",
                    Filter = "Archivo de Texto (*.txt)|*.txt",
                    DefaultExt = "txt",
                    FileName = $"frase_recuperacion_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var content = $"FRASE DE RECUPERACIÓN - FILEENCRYPTER\n";
                    content += $"Generada: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
                    content += $"FRASE DE RECUPERACIÓN:\n{_recoveryPhrase}\n\n";
                    content += $"FRASE FORMATEADA:\n{RecoveryPhraseHelper.FormatPhraseForDisplay(_recoveryPhrase)}\n\n";
                    content += "INSTRUCCIONES:\n";
                    content += "• Esta frase permite recuperar archivos encriptados si olvidas la contraseña\n";
                    content += "• Guarda este archivo en un lugar seguro y privado\n";
                    content += "• No compartas esta información con nadie\n";
                    content += "• Considera hacer una copia física escrita a mano\n";
                    content += "• Si pierdes tanto la contraseña como esta frase, no podrás recuperar tus archivos\n";

                    File.WriteAllText(dialog.FileName, content);
                    CustomMessageBox.ShowSuccess($"Frase de recuperación guardada en:\n{dialog.FileName}", "Guardado", this);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error al guardar el archivo: {ex.Message}", "Error", this);
            }
        }

        private void ConfirmationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            CloseButton.IsEnabled = ConfirmationCheckBox.IsChecked == true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
