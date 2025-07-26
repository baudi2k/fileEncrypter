using FileEncrypter.Helpers;
using FileEncrypter.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
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
            ProgressSection.Visibility = Visibility.Visible;
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
                ProgressSection.Visibility = Visibility.Collapsed;
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
            ProgressSection.Visibility = Visibility.Visible;
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
                ProgressSection.Visibility = Visibility.Collapsed;
                CancelButton.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        #region Drag & Drop

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                SetDropZoneHighlight(true);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            SetDropZoneHighlight(false);
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            SetDropZoneHighlight(false);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0]; // Procesar solo el primer archivo

                    // Verificar contraseña
                    var pwd = PasswordInput.Password;
                    if (string.IsNullOrWhiteSpace(pwd))
                    {
                        CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña antes de procesar el archivo.", "Contraseña Requerida", this);
                        return;
                    }

                    // Determinar operación basada en la extensión
                    if (Path.GetExtension(filePath).ToLower() == ".enc")
                    {
                        await ProcessDecryptFile(filePath, pwd);
                    }
                    else
                    {
                        await ProcessEncryptFile(filePath, pwd);
                    }
                }
            }
        }

        private void SetDropZoneHighlight(bool highlight)
        {
            var rectangle = DropZone.Children.OfType<System.Windows.Shapes.Rectangle>().First();
            if (highlight)
            {
                DropZone.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0x3B, 0x82, 0xF6));
                rectangle.StrokeThickness = 3;
            }
            else
            {
                DropZone.Background = System.Windows.Media.Brushes.Transparent;
                rectangle.StrokeThickness = 2;
            }
        }

        private async Task ProcessEncryptFile(string inputPath, string password)
        {
            var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
            var hashName = HashHelper.ComputeHash(Path.GetFileName(inputPath));
            var output = Path.Combine(dir, hashName + ".enc");

            await ExecuteFileOperation(async () =>
            {
                await EncryptionService.EncryptFileAsync(inputPath, output, password, GetProgressReporter(), _cts.Token);
                CustomMessageBox.ShowSuccess($"El archivo se ha encriptado correctamente.\n\nUbicación: {output}", "Encriptación Completada", this);
            });
        }

        private async Task ProcessDecryptFile(string inputPath, string password)
        {
            var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;

            await ExecuteFileOperation(async () =>
            {
                var output = await EncryptionService.DecryptFileAsync(inputPath, password, dir, GetProgressReporter(), _cts.Token);
                CustomMessageBox.ShowSuccess($"El archivo se ha desencriptado correctamente.\n\nUbicación: {output}", "Desencriptación Completada", this);
            });
        }

        private async Task ExecuteFileOperation(Func<Task> operation)
        {
            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressSection.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            try
            {
                await operation();
            }
            catch (OperationCanceledException)
            {
                CustomMessageBox.ShowWarning("La operación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (CryptographicException)
            {
                CustomMessageBox.ShowPasswordError(this);
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ocurrió un error durante la operación:\n\n{ex.Message}", "Error", this);
            }
            finally
            {
                ProgressSection.Visibility = Visibility.Collapsed;
                CancelButton.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private IProgress<double> GetProgressReporter()
        {
            return new Progress<double>(pct => ProgressBar.Value = pct);
        }

        #endregion
    }
}
