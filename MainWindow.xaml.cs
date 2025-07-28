using FileEncrypter.Helpers;
using FileEncrypter.Services;
using FileEncrypter.Models;
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
        private bool _isEncryptPasswordVisible = false;
        private bool _isDecryptPasswordVisible = false;
        private TextBox? _encryptPasswordTextBox;
        private TextBox? _decryptPasswordTextBox;
        private string _currentSection = "Encrypt"; // "Encrypt" or "Decrypt"

        public MainWindow()
        {
            InitializeComponent();
            // Inicializar después de que todos los controles estén listos
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateEncryptPasswordStrength();
        }

        private async void EncryptFile_Click(object sender, RoutedEventArgs e)
        {
            var pwd = _isEncryptPasswordVisible ? (_encryptPasswordTextBox?.Text ?? "") : EncryptPasswordInput.Password;
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

            var progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
                // Solo actualizar la barra de progreso local, sin notificaciones
            });
            
            // No mostrar notificación de progreso inicial
            
            try
            {
                var result = await EncryptionService.EncryptFileWithRecoveryAsync(input, output, pwd, progress, _cts.Token);
                
                // No limpiar notificación de progreso (ya que no la mostramos)
                
                // Mostrar notificación de éxito
                NotificationService.ShowEncryptionSuccess(Path.GetFileName(input), output, result.RecoveryPhrase);
                
                // Agregar al historial
                var fileInfo = new FileInfo(input);
                await HistoryService.AddEncryptionEntryAsync(input, output, fileInfo.Length);
                
                // Mostrar ventana de frase de recuperación
                var recoveryWindow = new RecoveryPhraseWindow(result.RecoveryPhrase)
                {
                    Owner = this
                };
                recoveryWindow.ShowDialog();
                
                // Recordatorio adicional de la frase de recuperación
                NotificationService.ShowRecoveryPhraseReminder(result.RecoveryPhrase);
            }
            catch (OperationCanceledException)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowWarning("Operación Cancelada", "La encriptación fue cancelada por el usuario.");
                CustomMessageBox.ShowWarning("La operación de encriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (Exception ex)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowError("Error de Encriptación", $"Error procesando {Path.GetFileName(input)}: {ex.Message}");
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
            // Determinar método de desencriptación
            bool usePassword = UsePasswordRadio?.IsChecked == true;
            string? password = null;
            string? recoveryPhrase = null;

            if (usePassword)
            {
                password = _isDecryptPasswordVisible ? (_decryptPasswordTextBox?.Text ?? "") : DecryptPasswordInput.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña antes de continuar.", "Contraseña Requerida", this);
                    return;
                }
            }
            else
            {
                recoveryPhrase = DecryptRecoveryPhraseInput?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(recoveryPhrase))
                {
                    CustomMessageBox.ShowWarning("Por favor, ingrese la frase de recuperación antes de continuar.", "Frase de Recuperación Requerida", this);
                    return;
                }

                if (!RecoveryPhraseHelper.ValidateRecoveryPhrase(recoveryPhrase))
                {
                    CustomMessageBox.ShowWarning("La frase de recuperación debe contener exactamente 12 palabras válidas.", "Frase de Recuperación Inválida", this);
                    return;
                }
            }

            var dlg = new OpenFileDialog { Filter = "Archivos (*.enc)|*.enc" };
            if (dlg.ShowDialog() != true) return;

            var input = dlg.FileName;
            var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;

            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressSection.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            var progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
                // Solo actualizar la barra de progreso local, sin notificaciones
            });
            
            // No mostrar notificación de progreso inicial
            
            try
            {
                var output = await EncryptionService.DecryptFileWithPasswordOrRecoveryAsync(
                    input, password, recoveryPhrase, dir, progress, _cts.Token);
                
                // Marcar como desencriptado en el historial
                await HistoryService.MarkAsDecryptedAsync(input, output);
                
                // No limpiar notificación de progreso (ya que no la mostramos)
                
                // Mostrar notificación de éxito
                NotificationService.ShowDecryptionSuccess(Path.GetFileName(output), output);
                
                var method = usePassword ? "contraseña" : "frase de recuperación";
                CustomMessageBox.ShowSuccess($"El archivo se ha desencriptado correctamente usando {method}.\n\nUbicación: {output}", "Desencriptación Completada", this);
            }
            catch (OperationCanceledException)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowWarning("Operación Cancelada", "La desencriptación fue cancelada por el usuario.");
                CustomMessageBox.ShowWarning("La operación de desencriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (CryptographicException ex)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                
                if (ex.Message.Contains("frase de recuperación"))
                {
                    NotificationService.ShowError("Frase de Recuperación Incorrecta", "La frase de recuperación ingresada no es válida.");
                    CustomMessageBox.ShowError("Frase de recuperación incorrecta o archivo corrupto.", "Error de Desencriptación", this);
                }
                else if (ex.Message.Contains("no tiene frase de recuperación"))
                {
                    NotificationService.ShowWarning("Archivo Legacy", "Este archivo no tiene frase de recuperación. Use la contraseña original.");
                    CustomMessageBox.ShowError("Este archivo fue encriptado con una versión anterior y no tiene frase de recuperación. Use la contraseña original.", "Sin Frase de Recuperación", this);
                }
                else
                {
                    NotificationService.ShowError("Contraseña Incorrecta", "La contraseña ingresada no es válida para este archivo.");
                    CustomMessageBox.ShowPasswordError(this);
                }
            }
            catch (Exception ex)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowError("Error de Desencriptación", $"Error procesando {Path.GetFileName(input)}: {ex.Message}");
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
                    bool isEncFile = Path.GetExtension(filePath).ToLower() == ".enc";

                    // Determinar qué contraseña usar según la sección actual
                    string pwd;
                    if (_currentSection == "Encrypt" && !isEncFile)
                    {
                        pwd = _isEncryptPasswordVisible ? (_encryptPasswordTextBox?.Text ?? "") : EncryptPasswordInput.Password;
                        if (string.IsNullOrWhiteSpace(pwd))
                        {
                            CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña de encriptación antes de procesar el archivo.", "Contraseña Requerida", this);
                            return;
                        }
                        await ProcessEncryptFile(filePath, pwd);
                    }
                    else if (_currentSection == "Decrypt" && isEncFile)
                    {
                        pwd = _isDecryptPasswordVisible ? (_decryptPasswordTextBox?.Text ?? "") : DecryptPasswordInput.Password;
                        if (string.IsNullOrWhiteSpace(pwd))
                        {
                            CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña de desencriptación antes de procesar el archivo.", "Contraseña Requerida", this);
                            return;
                        }
                        await ProcessDecryptFile(filePath, pwd);
                    }
                    else
                    {
                        // Archivo no corresponde a la sección actual
                        if (isEncFile && _currentSection == "Encrypt")
                        {
                            CustomMessageBox.ShowWarning("Los archivos .enc deben ser arrastrados en la sección 'Desencriptar'.", "Archivo Incorrecto", this);
                        }
                        else if (!isEncFile && _currentSection == "Decrypt")
                        {
                            CustomMessageBox.ShowWarning("Para desencriptar, debe arrastrar archivos con extensión .enc.", "Archivo Incorrecto", this);
                        }
                    }
                }
            }
        }

        private void SetDropZoneHighlight(bool highlight)
        {
            Grid? activeDropZone = null;
            
            // Determinar qué zona de drop está activa
            if (_currentSection == "Encrypt" && EncryptDropZone != null)
            {
                activeDropZone = EncryptDropZone;
            }
            else if (_currentSection == "Decrypt" && DecryptDropZone != null)
            {
                activeDropZone = DecryptDropZone;
            }

            if (activeDropZone != null)
            {
                var rectangle = activeDropZone.Children.OfType<System.Windows.Shapes.Rectangle>().FirstOrDefault();
                if (rectangle != null)
                {
                    if (highlight)
                    {
                        activeDropZone.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0x3B, 0x82, 0xF6));
                        rectangle.StrokeThickness = 3;
                    }
                    else
                    {
                        activeDropZone.Background = System.Windows.Media.Brushes.Transparent;
                        rectangle.StrokeThickness = 2;
                    }
                }
            }
        }

        private async Task ProcessEncryptFile(string inputPath, string password)
        {
            await ProcessEncryptFileWithRecovery(inputPath, password);
        }

        private async Task ProcessDecryptFile(string inputPath, string password)
        {
            var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;

            await ExecuteFileOperation(async () =>
            {
                var output = await EncryptionService.DecryptFileWithPasswordOrRecoveryAsync(inputPath, password, null, dir, GetProgressReporter(), _cts.Token);
                
                // Marcar como desencriptado en el historial
                await HistoryService.MarkAsDecryptedAsync(inputPath, output);
                
                CustomMessageBox.ShowSuccess($"El archivo se ha desencriptado correctamente usando contraseña.\n\nUbicación: {output}", "Desencriptación Completada", this);
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
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowWarning("Operación Cancelada", "La operación fue cancelada por el usuario.");
                CustomMessageBox.ShowWarning("La operación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (CryptographicException)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowError("Contraseña Incorrecta", "La contraseña proporcionada no es válida.");
                CustomMessageBox.ShowPasswordError(this);
            }
            catch (Exception ex)
            {
                // No limpiar notificación de progreso (ya que no la mostramos)
                NotificationService.ShowError("Error en Operación", $"Ocurrió un error: {ex.Message}");
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
            return new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
                // Solo actualizar la barra de progreso local, sin notificaciones
            });
        }

        #endregion

        #region Sidebar Navigation

        private void EncryptNav_Click(object sender, RoutedEventArgs e)
        {
            if (EncryptNavButton == null || DecryptNavButton == null || HistoryNavButton == null ||
                EncryptSection == null || DecryptSection == null) return;

            _currentSection = "Encrypt";
            
            // Update button styles
            EncryptNavButton.Style = (Style)FindResource("SidebarButtonActive");
            DecryptNavButton.Style = (Style)FindResource("SidebarButton");
            HistoryNavButton.Style = (Style)FindResource("SidebarButton");
            
            // Show/Hide sections
            EncryptSection.Visibility = Visibility.Visible;
            DecryptSection.Visibility = Visibility.Collapsed;
        }

        private void DecryptNav_Click(object sender, RoutedEventArgs e)
        {
            if (EncryptNavButton == null || DecryptNavButton == null || HistoryNavButton == null ||
                EncryptSection == null || DecryptSection == null) return;

            _currentSection = "Decrypt";
            
            // Update button styles
            EncryptNavButton.Style = (Style)FindResource("SidebarButton");
            DecryptNavButton.Style = (Style)FindResource("SidebarButtonActive");
            HistoryNavButton.Style = (Style)FindResource("SidebarButton");
            
            // Show/Hide sections
            EncryptSection.Visibility = Visibility.Collapsed;
            DecryptSection.Visibility = Visibility.Visible;
        }

        private void HistoryNav_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historyWindow = new HistoryWindow
                {
                    Owner = this
                };
                historyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error abriendo historial: {ex.Message}", "Error", this);
            }
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

        private void EncryptPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateEncryptPasswordStrength();
        }

        private void ShowEncryptPassword_Click(object sender, RoutedEventArgs e)
        {
            if (EncryptPasswordInput == null || ShowEncryptPasswordButton == null)
                return;

            if (!_isEncryptPasswordVisible)
            {
                // Crear TextBox para mostrar la contraseña
                if (_encryptPasswordTextBox == null)
                {
                    _encryptPasswordTextBox = new TextBox
                    {
                        FontFamily = new FontFamily("Consolas"),
                        ToolTip = EncryptPasswordInput.ToolTip
                    };
                    
                    // Aplicar el estilo correcto para TextBox
                    try
                    {
                        var textBoxStyle = (Style)FindResource("ModernTextBox");
                        _encryptPasswordTextBox.Style = textBoxStyle;
                    }
                    catch (Exception)
                    {
                        // Si el estilo no se encuentra, aplicar propiedades manualmente
                        _encryptPasswordTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3A3A3A"));
                        try
                        {
                            _encryptPasswordTextBox.Foreground = (SolidColorBrush)FindResource("OnSurfaceBrush");
                        }
                        catch
                        {
                            _encryptPasswordTextBox.Foreground = new SolidColorBrush(Colors.White);
                        }
                        _encryptPasswordTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4A4A4A"));
                        _encryptPasswordTextBox.BorderThickness = new Thickness(1);
                        _encryptPasswordTextBox.Padding = new Thickness(12, 10, 12, 10);
                        _encryptPasswordTextBox.FontSize = 14;
                    }
                    
                    Grid.SetColumn(_encryptPasswordTextBox, 0);
                    
                    // Agregar evento para actualizar fortaleza cuando se edite
                    _encryptPasswordTextBox.TextChanged += (s, args) => UpdateEncryptPasswordStrength();
                }

                _encryptPasswordTextBox.Text = EncryptPasswordInput.Password;
                var parent = (Grid)EncryptPasswordInput.Parent;
                if (parent != null)
                {
                    parent.Children.Remove(EncryptPasswordInput);
                    parent.Children.Add(_encryptPasswordTextBox);
                }
                
                ShowEncryptPasswordButton.Content = "🙈";
                _isEncryptPasswordVisible = true;
            }
            else
            {
                // Volver al PasswordBox
                if (_encryptPasswordTextBox != null)
                {
                    EncryptPasswordInput.Password = _encryptPasswordTextBox.Text ?? "";
                    var parent = (Grid)_encryptPasswordTextBox.Parent;
                    if (parent != null)
                    {
                        parent.Children.Remove(_encryptPasswordTextBox);
                        parent.Children.Add(EncryptPasswordInput);
                    }
                }
                
                ShowEncryptPasswordButton.Content = "👁️";
                _isEncryptPasswordVisible = false;
                UpdateEncryptPasswordStrength(); // Actualizar después de cambiar de vuelta
            }
        }

        private void ShowDecryptPassword_Click(object sender, RoutedEventArgs e)
        {
            if (DecryptPasswordInput == null || ShowDecryptPasswordButton == null)
                return;

            if (!_isDecryptPasswordVisible)
            {
                // Crear TextBox para mostrar la contraseña
                if (_decryptPasswordTextBox == null)
                {
                    _decryptPasswordTextBox = new TextBox
                    {
                        FontFamily = new FontFamily("Consolas"),
                        ToolTip = DecryptPasswordInput.ToolTip
                    };
                    
                    // Aplicar el estilo correcto para TextBox
                    try
                    {
                        var textBoxStyle = (Style)FindResource("ModernTextBox");
                        _decryptPasswordTextBox.Style = textBoxStyle;
                    }
                    catch (Exception)
                    {
                        // Si el estilo no se encuentra, aplicar propiedades manualmente
                        _decryptPasswordTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3A3A3A"));
                        try
                        {
                            _decryptPasswordTextBox.Foreground = (SolidColorBrush)FindResource("OnSurfaceBrush");
                        }
                        catch
                        {
                            _decryptPasswordTextBox.Foreground = new SolidColorBrush(Colors.White);
                        }
                        _decryptPasswordTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4A4A4A"));
                        _decryptPasswordTextBox.BorderThickness = new Thickness(1);
                        _decryptPasswordTextBox.Padding = new Thickness(12, 10, 12, 10);
                        _decryptPasswordTextBox.FontSize = 14;
                    }
                    
                    Grid.SetColumn(_decryptPasswordTextBox, 0);
                }

                _decryptPasswordTextBox.Text = DecryptPasswordInput.Password;
                var parent = (Grid)DecryptPasswordInput.Parent;
                if (parent != null)
                {
                    parent.Children.Remove(DecryptPasswordInput);
                    parent.Children.Add(_decryptPasswordTextBox);
                }
                
                ShowDecryptPasswordButton.Content = "🙈";
                _isDecryptPasswordVisible = true;
            }
            else
            {
                // Volver al PasswordBox
                if (_decryptPasswordTextBox != null)
                {
                    DecryptPasswordInput.Password = _decryptPasswordTextBox.Text ?? "";
                    var parent = (Grid)_decryptPasswordTextBox.Parent;
                    if (parent != null)
                    {
                        parent.Children.Remove(_decryptPasswordTextBox);
                        parent.Children.Add(DecryptPasswordInput);
                    }
                }
                
                ShowDecryptPasswordButton.Content = "👁️";
                _isDecryptPasswordVisible = false;
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
            if (GeneratedPasswordPreview == null || EncryptPasswordInput == null)
                return;

            if (!string.IsNullOrEmpty(GeneratedPasswordPreview.Text) && 
                GeneratedPasswordPreview.Text != "Haz clic en 'Generar' para crear una contraseña")
            {
                if (_isEncryptPasswordVisible && _encryptPasswordTextBox != null)
                {
                    _encryptPasswordTextBox.Text = GeneratedPasswordPreview.Text;
                }
                else
                {
                    EncryptPasswordInput.Password = GeneratedPasswordPreview.Text;
                }
                
                UpdateEncryptPasswordStrength();
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

        private void UpdateEncryptPasswordStrength()
        {
            // Verificar que los controles estén inicializados
            if (EncryptPasswordInput == null || EncryptStrengthBar == null || EncryptStrengthText == null)
                return;

            try
            {
                var password = _isEncryptPasswordVisible ? (_encryptPasswordTextBox?.Text ?? "") : EncryptPasswordInput.Password;
                var strength = PasswordHelper.EvaluatePasswordStrength(password);

                EncryptStrengthBar.Value = strength.Score;
                EncryptStrengthText.Text = strength.Level;
                
                // Actualizar color del texto
                var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(strength.Color));
                EncryptStrengthText.Foreground = colorBrush;
                
                // Actualizar color de la barra de progreso
                EncryptStrengthBar.Foreground = colorBrush;
            }
            catch (Exception)
            {
                // Si hay algún error, establecer valores predeterminados
                if (EncryptStrengthBar != null) EncryptStrengthBar.Value = 0;
                if (EncryptStrengthText != null) EncryptStrengthText.Text = "N/A";
            }
        }

        #endregion

        #region Recovery Phrase Interface

        private void DecryptionMethod_Changed(object sender, RoutedEventArgs e)
        {
            if (PasswordDecryptPanel == null || RecoveryPhraseDecryptPanel == null) return;

            bool usePassword = UsePasswordRadio?.IsChecked == true;
            
            PasswordDecryptPanel.Visibility = usePassword ? Visibility.Visible : Visibility.Collapsed;
            RecoveryPhraseDecryptPanel.Visibility = usePassword ? Visibility.Collapsed : Visibility.Visible;
        }

        private void DecryptRecoveryPhrase_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DecryptRecoveryPhraseInput == null || RecoveryPhraseValidation == null) return;

            var text = DecryptRecoveryPhraseInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                RecoveryPhraseValidation.Text = "Ingrese las 12 palabras separadas por espacios";
                RecoveryPhraseValidation.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF59E0B"));
                return;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (words.Length < 12)
            {
                RecoveryPhraseValidation.Text = $"Faltan {12 - words.Length} palabras";
                RecoveryPhraseValidation.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEF4444"));
            }
            else if (words.Length > 12)
            {
                RecoveryPhraseValidation.Text = $"Demasiadas palabras ({words.Length}/12)";
                RecoveryPhraseValidation.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEF4444"));
            }
            else
            {
                // Exactamente 12 palabras, validar si son correctas
                if (RecoveryPhraseHelper.ValidateRecoveryPhrase(text))
                {
                    RecoveryPhraseValidation.Text = "✅ Frase de recuperación válida";
                    RecoveryPhraseValidation.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF10B981"));
                }
                else
                {
                    RecoveryPhraseValidation.Text = "❌ Una o más palabras no son válidas";
                    RecoveryPhraseValidation.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEF4444"));
                }
            }
        }

        private async Task ProcessEncryptFileWithRecovery(string inputPath, string password)
        {
            var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
            var hashName = HashHelper.ComputeHash(Path.GetFileName(inputPath));
            var output = Path.Combine(dir, hashName + ".enc");

            await ExecuteFileOperation(async () =>
            {
                var result = await EncryptionService.EncryptFileWithRecoveryAsync(inputPath, output, password, GetProgressReporter(), _cts.Token);
                
                // Agregar al historial
                var fileInfo = new FileInfo(inputPath);
                await HistoryService.AddEncryptionEntryAsync(inputPath, output, fileInfo.Length);
                
                // Mostrar ventana de frase de recuperación
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var recoveryWindow = new RecoveryPhraseWindow(result.RecoveryPhrase)
                    {
                        Owner = this
                    };
                    recoveryWindow.ShowDialog();
                });
            });
        }

        #endregion
    }
}
