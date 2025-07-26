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
using System.Windows.Controls;
using System.Windows.Media;

namespace FileEncrypter
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;
        private bool _isPasswordVisible = false;
        private TextBox? _passwordTextBox;

        public MainWindow()
        {
            InitializeComponent();
            // Inicializar después de que todos los controles estén listos
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
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

        #region Password Generator

        private void ToggleGenerator_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordGenerator == null || ToggleGeneratorButton == null)
                return;

            if (PasswordGenerator.Visibility == Visibility.Visible)
            {
                PasswordGenerator.Visibility = Visibility.Collapsed;
                ToggleGeneratorButton.Content = "🎲 Generar";
            }
            else
            {
                PasswordGenerator.Visibility = Visibility.Visible;
                ToggleGeneratorButton.Content = "❌ Cerrar";
                UpdatePasswordPreview();
            }
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
        }

        private void ShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordInput == null || ShowPasswordButton == null)
                return;

            if (!_isPasswordVisible)
            {
                // Crear TextBox para mostrar la contraseña
                if (_passwordTextBox == null)
                {
                    _passwordTextBox = new TextBox
                    {
                        Style = PasswordInput.Style,
                        FontFamily = new FontFamily("Consolas"),
                        ToolTip = PasswordInput.ToolTip
                    };
                    Grid.SetColumn(_passwordTextBox, 0);
                }

                _passwordTextBox.Text = PasswordInput.Password;
                var parent = (Grid)PasswordInput.Parent;
                if (parent != null)
                {
                    parent.Children.Remove(PasswordInput);
                    parent.Children.Add(_passwordTextBox);
                }
                
                ShowPasswordButton.Content = "🙈";
                _isPasswordVisible = true;
            }
            else
            {
                // Volver al PasswordBox
                if (_passwordTextBox != null)
                {
                    PasswordInput.Password = _passwordTextBox.Text ?? "";
                    var parent = (Grid)_passwordTextBox.Parent;
                    if (parent != null)
                    {
                        parent.Children.Remove(_passwordTextBox);
                        parent.Children.Add(PasswordInput);
                    }
                }
                
                ShowPasswordButton.Content = "👁️";
                _isPasswordVisible = false;
            }
        }

        private void LengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LengthValue != null)
            {
                LengthValue.Text = ((int)e.NewValue).ToString();
            }
            UpdatePasswordPreview();
        }

        private void CharacterType_Changed(object sender, RoutedEventArgs e)
        {
            // Solo actualizar si el generador está visible para evitar errores durante inicialización
            if (PasswordGenerator?.Visibility == Visibility.Visible)
            {
                UpdatePasswordPreview();
            }
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            if (LengthSlider == null || IncludeUppercase == null || IncludeLowercase == null || 
                IncludeNumbers == null || IncludeSpecial == null || GeneratedPasswordPreview == null)
                return;

            try
            {
                var length = (int)LengthSlider.Value;
                var includeUppercase = IncludeUppercase.IsChecked == true;
                var includeLowercase = IncludeLowercase.IsChecked == true;
                var includeNumbers = IncludeNumbers.IsChecked == true;
                var includeSpecial = IncludeSpecial.IsChecked == true;

                if (!includeUppercase && !includeLowercase && !includeNumbers && !includeSpecial)
                {
                    CustomMessageBox.ShowWarning("Debe seleccionar al menos un tipo de caracter.", "Configuración Inválida", this);
                    return;
                }

                var password = PasswordHelper.GeneratePassword(length, includeUppercase, includeLowercase, includeNumbers, includeSpecial);
                GeneratedPasswordPreview.Text = password;
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error al generar contraseña: {ex.Message}", "Error", this);
            }
        }

        private void UseGeneratedPassword_Click(object sender, RoutedEventArgs e)
        {
            if (GeneratedPasswordPreview == null || PasswordInput == null)
                return;

            if (!string.IsNullOrEmpty(GeneratedPasswordPreview.Text) && 
                GeneratedPasswordPreview.Text != "Haz clic en 'Generar' para crear una contraseña")
            {
                if (_isPasswordVisible && _passwordTextBox != null)
                {
                    _passwordTextBox.Text = GeneratedPasswordPreview.Text;
                }
                else
                {
                    PasswordInput.Password = GeneratedPasswordPreview.Text;
                }
                
                UpdatePasswordStrength();
                CustomMessageBox.ShowSuccess("Contraseña aplicada correctamente.", "Éxito", this);
            }
            else
            {
                CustomMessageBox.ShowWarning("Primero debe generar una contraseña.", "Sin Contraseña", this);
            }
        }

        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (GeneratedPasswordPreview == null)
                return;

            if (!string.IsNullOrEmpty(GeneratedPasswordPreview.Text) && 
                GeneratedPasswordPreview.Text != "Haz clic en 'Generar' para crear una contraseña")
            {
                try
                {
                    System.Windows.Clipboard.SetText(GeneratedPasswordPreview.Text);
                    CustomMessageBox.ShowSuccess("Contraseña copiada al portapapeles.", "Copiado", this);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.ShowError($"Error al copiar: {ex.Message}", "Error", this);
                }
            }
            else
            {
                CustomMessageBox.ShowWarning("No hay contraseña que copiar.", "Sin Contraseña", this);
            }
        }

        private void UpdatePasswordPreview()
        {
            // Verificar que todos los controles necesarios estén inicializados
            if (LengthSlider == null || IncludeUppercase == null || IncludeLowercase == null || 
                IncludeNumbers == null || IncludeSpecial == null || GeneratedPasswordPreview == null) 
                return;

            try
            {
                var length = (int)LengthSlider.Value;
                var includeUppercase = IncludeUppercase.IsChecked == true;
                var includeLowercase = IncludeLowercase.IsChecked == true;
                var includeNumbers = IncludeNumbers.IsChecked == true;
                var includeSpecial = IncludeSpecial.IsChecked == true;

                if (!includeUppercase && !includeLowercase && !includeNumbers && !includeSpecial)
                {
                    GeneratedPasswordPreview.Text = "Seleccione al menos un tipo de caracter";
                    return;
                }

                var preview = PasswordHelper.GeneratePassword(length, includeUppercase, includeLowercase, includeNumbers, includeSpecial);
                GeneratedPasswordPreview.Text = preview;
            }
            catch
            {
                if (GeneratedPasswordPreview != null)
                {
                    GeneratedPasswordPreview.Text = "Error en la configuración";
                }
            }
        }

        private void UpdatePasswordStrength()
        {
            // Verificar que los controles estén inicializados
            if (PasswordInput == null || StrengthBar == null || StrengthText == null)
                return;

            try
            {
                var password = _isPasswordVisible ? (_passwordTextBox?.Text ?? "") : PasswordInput.Password;
                var strength = PasswordHelper.EvaluatePasswordStrength(password);

                StrengthBar.Value = strength.Score;
                StrengthText.Text = strength.Level;
                
                // Actualizar color del texto
                var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(strength.Color));
                StrengthText.Foreground = colorBrush;
                
                // Actualizar color de la barra de progreso
                StrengthBar.Foreground = colorBrush;
            }
            catch (Exception)
            {
                // Si hay algún error, establecer valores predeterminados
                if (StrengthBar != null) StrengthBar.Value = 0;
                if (StrengthText != null) StrengthText.Text = "N/A";
            }
        }

        #endregion
    }
}
