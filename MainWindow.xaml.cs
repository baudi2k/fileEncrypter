using FileEncrypter.Helpers;
using FileEncrypter.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileEncrypter
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void EncryptFile_Click(object sender, RoutedEventArgs e)
        {
            var pwd = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña antes de continuar.", "Contraseña Requerida", this);
                return;
            }

            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() != true) return;

            var input = dlg.FileName;
            var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;
            var hashName = HashHelper.ComputeHash(Path.GetFileName(input));
            var output = Path.Combine(dir, hashName + ".enc");

            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressBar.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            var progress = new Progress<double>(pct => ProgressBar.Value = pct);
            try
            {
                await EncryptionService.EncryptFileAsync(input, output, pwd, progress, _cts.Token);
                CustomMessageBox.ShowSuccess($"El archivo se ha encriptado correctamente.\n\nUbicación: {output}", "Encriptación Completada", this);
            }
            catch (OperationCanceledException)
            {
                CustomMessageBox.ShowWarning("La operación de encriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ocurrió un error durante la encriptación:\n\n{ex.Message}", "Error de Encriptación", this);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                CancelButton.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async void DecryptFile_Click(object sender, RoutedEventArgs e)
        {
            var pwd = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña antes de continuar.", "Contraseña Requerida", this);
                return;
            }

            var dlg = new OpenFileDialog { Filter = "Archivos (*.enc)|*.enc" };
            if (dlg.ShowDialog() != true) return;

            var input = dlg.FileName;
            var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;

            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressBar.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            var progress = new Progress<double>(pct => ProgressBar.Value = pct);
            try
            {
                var output = await EncryptionService.DecryptFileAsync(input, pwd, dir, progress, _cts.Token);
                CustomMessageBox.ShowSuccess($"El archivo se ha desencriptado correctamente.\n\nUbicación: {output}", "Desencriptación Completada", this);
            }
            catch (OperationCanceledException)
            {
                CustomMessageBox.ShowWarning("La operación de desencriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (CryptographicException)
            {
                CustomMessageBox.ShowPasswordError(this);
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ocurrió un error durante la desencriptación:\n\n{ex.Message}", "Error de Desencriptación", this);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                CancelButton.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }
    }
}
