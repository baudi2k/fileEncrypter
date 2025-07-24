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
                MessageBox.Show("Ingrese una contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"Encriptado correctamente:\n{output}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Operación cancelada.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Ingrese una contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"Desencriptado correctamente:\n{output}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Operación cancelada.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (CryptographicException)
            {
                MessageBox.Show("Contraseña incorrecta.", "Error de autenticación", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
