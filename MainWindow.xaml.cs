using FileEncrypter.Helpers;
using FileEncrypter.Services;
using FileEncrypter.Models;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        private CertificateService _certificateService;
        private CertificateInfo? _selectedEncryptCertificate;
        private CertificateInfo? _selectedDecryptCertificate;

        public MainWindow()
        {
            InitializeComponent();
            _certificateService = new CertificateService();
            // Inicializar después de que todos los controles estén listos
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateEncryptPasswordStrength();
            // Inicializar estados de los paneles
            EncryptionMethod_Changed(null, null);
        }

        private async void EncryptFile_Click(object sender, RoutedEventArgs e)
        {
            // Determinar método de encriptación
            bool usePassword = UsePasswordEncryptRadio?.IsChecked == true;
            bool useCertificate = UseCertificateEncryptRadio?.IsChecked == true;
            
            if (usePassword)
            {
                var pwd = _isEncryptPasswordVisible ? (_encryptPasswordTextBox?.Text ?? "") : EncryptPasswordInput.Password;
                if (string.IsNullOrWhiteSpace(pwd))
                {
                    CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña antes de continuar.", "Contraseña Requerida", this);
                    return;
                }
                await EncryptFileWithPassword(pwd);
            }
            else if (useCertificate)
            {
                if (_selectedEncryptCertificate == null)
                {
                    CustomMessageBox.ShowWarning("Por favor, seleccione un certificado PKI antes de continuar.", "Certificado Requerido", this);
                    return;
                }
                await EncryptFileWithCertificate(_selectedEncryptCertificate);
            }
            else
            {
                CustomMessageBox.ShowWarning("Por favor, seleccione un método de encriptación.", "Método Requerido", this);
            }
        }
        
        private async Task EncryptFileWithPassword(string password)
        {
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

            IProgress<double> progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
                // Solo actualizar la barra de progreso local, sin notificaciones
            });
            
            // No mostrar notificación de progreso inicial
            
            try
            {
                var result = await EncryptionService.EncryptFileWithRecoveryAsync(input, output, password, progress, _cts.Token);
                
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

                if (DeleteOriginalCheckBox?.IsChecked == true)
                {
                    try
                    {
                        File.Delete(input);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.ShowError($"Error eliminando archivo original: {ex.Message}", "Error", this);
                    }
                }
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
            bool useRecoveryPhrase = UseRecoveryPhraseRadio?.IsChecked == true;
            bool useCertificate = UseCertificateDecryptRadio?.IsChecked == true;
            
            string? password = null;
            string? recoveryPhrase = null;
            CertificateInfo? certificate = null;

            if (usePassword)
            {
                password = _isDecryptPasswordVisible ? (_decryptPasswordTextBox?.Text ?? "") : DecryptPasswordInput.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña antes de continuar.", "Contraseña Requerida", this);
                    return;
                }
            }
            else if (useRecoveryPhrase)
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
            else if (useCertificate)
            {
                if (_selectedDecryptCertificate == null)
                {
                    CustomMessageBox.ShowWarning("Por favor, seleccione un certificado PKI antes de continuar.", "Certificado Requerido", this);
                    return;
                }
                
                if (!_selectedDecryptCertificate.HasPrivateKey)
                {
                    CustomMessageBox.ShowWarning("El certificado seleccionado no tiene clave privada. Se requiere la clave privada para desencriptar.", "Certificado Sin Clave Privada", this);
                    return;
                }
                
                certificate = _selectedDecryptCertificate;
            }

            // Filtrar archivos según el método de desencriptación seleccionado
            string filter = useCertificate ? 
                "Archivos encriptados (*.enc;*.pki)|*.enc;*.pki|Archivos .enc|*.enc|Archivos .pki|*.pki" : 
                "Archivos encriptados (*.enc)|*.enc";
            
            var dlg = new OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() != true) return;

            var input = dlg.FileName;
            var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;

            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressSection.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            IProgress<double> progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
                // Solo actualizar la barra de progreso local, sin notificaciones
            });
            
            // No mostrar notificación de progreso inicial
            
            try
            {
                string output;
                
                if (useCertificate && certificate != null)
                {
                    output = await DecryptFileWithCertificateAsync(input, certificate, dir, progress, _cts.Token);
                }
                else
                {
                    output = await EncryptionService.DecryptFileWithPasswordOrRecoveryAsync(
                        input, password, recoveryPhrase, dir, progress, _cts.Token);
                }
                
                // Marcar como desencriptado en el historial
                await HistoryService.MarkAsDecryptedAsync(input, output);
                
                // No limpiar notificación de progreso (ya que no la mostramos)
                
                // Mostrar notificación de éxito
                NotificationService.ShowDecryptionSuccess(Path.GetFileName(output), output);
                
                var method = usePassword ? "contraseña" : 
                           useRecoveryPhrase ? "frase de recuperación" : 
                           "certificado PKI";
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
                    var fileExtension = Path.GetExtension(filePath).ToLower();
                    bool isEncryptedFile = fileExtension == ".enc" || fileExtension == ".pki";

                    // Determinar qué contraseña usar según la sección actual
                    string pwd;
                    if (_currentSection == "Encrypt" && !isEncryptedFile)
                    {
                        pwd = _isEncryptPasswordVisible ? (_encryptPasswordTextBox?.Text ?? "") : EncryptPasswordInput.Password;
                        if (string.IsNullOrWhiteSpace(pwd))
                        {
                            CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña de encriptación antes de procesar el archivo.", "Contraseña Requerida", this);
                            return;
                        }
                        bool deleteOriginal = DeleteOriginalCheckBox?.IsChecked == true;
                        await ProcessEncryptFile(filePath, pwd, deleteOriginal);
                    }
                    else if (_currentSection == "Decrypt" && isEncryptedFile)
                    {
                        // Determinar si es archivo .pki para usar certificado
                        if (fileExtension == ".pki")
                        {
                            // Para archivos .pki, verificar si hay certificado seleccionado
                            bool useCertificate = UseCertificateDecryptRadio?.IsChecked == true;
                            if (!useCertificate)
                            {
                                CustomMessageBox.ShowWarning("Para desencriptar archivos .pki, debe seleccionar 'Usar Certificado PKI' en el método de desencriptación.", "Método Incorrecto", this);
                                return;
                            }
                            
                            if (_selectedDecryptCertificate == null)
                            {
                                CustomMessageBox.ShowWarning("Por favor, seleccione un certificado para desencriptar archivos .pki.", "Certificado Requerido", this);
                                return;
                            }
                            
                            await ProcessDecryptFileWithCertificateAsync(filePath, _selectedDecryptCertificate);
                        }
                        else
                        {
                            // Para archivos .enc usar contraseña
                            pwd = _isDecryptPasswordVisible ? (_decryptPasswordTextBox?.Text ?? "") : DecryptPasswordInput.Password;
                            if (string.IsNullOrWhiteSpace(pwd))
                            {
                                CustomMessageBox.ShowWarning("Por favor, ingrese una contraseña de desencriptación antes de procesar el archivo.", "Contraseña Requerida", this);
                                return;
                            }
                            await ProcessDecryptFileAsync(filePath, pwd);
                        }
                    }
                    else
                    {
                        // Archivo no corresponde a la sección actual
                        if (isEncryptedFile && _currentSection == "Encrypt")
                        {
                            CustomMessageBox.ShowWarning("Los archivos encriptados (.enc/.pki) deben ser arrastrados en la sección 'Desencriptar'.", "Archivo Incorrecto", this);
                        }
                        else if (!isEncryptedFile && _currentSection == "Decrypt")
                        {
                            CustomMessageBox.ShowWarning("Para desencriptar, debe arrastrar archivos encriptados (.enc o .pki).", "Archivo Incorrecto", this);
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

        private async Task ProcessEncryptFile(string inputPath, string password, bool deleteOriginal)
        {
            await ProcessEncryptFileWithRecovery(inputPath, password, deleteOriginal);
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

        private async Task ProcessEncryptFileWithRecovery(string inputPath, string password, bool deleteOriginal)
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

                if (deleteOriginal)
                {
                    try
                    {
                        File.Delete(inputPath);
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CustomMessageBox.ShowError($"Error eliminando archivo original: {ex.Message}", "Error", this);
                        });
                    }
                }
            });
        }

        #endregion
        
        #region PKI Certificate Methods
        
        private async Task EncryptFileWithCertificate(CertificateInfo certificateInfo)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() != true) return;

            var input = dlg.FileName;
            var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;
            var hashName = HashHelper.ComputeHash(Path.GetFileName(input));
            var output = Path.Combine(dir, hashName + ".pki");

            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressSection.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            IProgress<double> progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
            });
            
            try
            {
                // Usar el nuevo método de streaming para archivos grandes
                await _certificateService.EncryptFileWithCertificateAsync(input, output, certificateInfo.Certificate, progress, _cts.Token);
                
                // Agregar al historial
                var fileInfo = new FileInfo(input);
                await HistoryService.AddEncryptionEntryAsync(input, output, fileInfo.Length);
                
                NotificationService.ShowEncryptionSuccess(Path.GetFileName(input), output, "");
                CustomMessageBox.ShowSuccess($"El archivo se ha encriptado correctamente usando certificado PKI.\n\nUbicación: {output}", "Encriptación Completada", this);

                if (DeleteOriginalCheckBox?.IsChecked == true)
                {
                    try
                    {
                        File.Delete(input);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.ShowError($"Error eliminando archivo original: {ex.Message}", "Error", this);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                NotificationService.ShowWarning("Operación Cancelada", "La encriptación fue cancelada por el usuario.");
                CustomMessageBox.ShowWarning("La operación de encriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (Exception ex)
            {
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
        
        private async Task<string> DecryptFileWithCertificateAsync(string inputPath, CertificateInfo certificateInfo, string outputDir, IProgress<double> progress, CancellationToken cancellationToken)
        {
            try
            {
                // Primero intentar con el nuevo formato de streaming
                return await _certificateService.DecryptFileWithCertificateAsync(inputPath, outputDir, certificateInfo.Certificate, progress, cancellationToken);
            }
            catch (InvalidDataException ex) when (ex.Message.Contains("versión anterior"))
            {
                // Si falla, intentar con el formato legacy
                return await DecryptLegacyPKIFileAsync(inputPath, certificateInfo, outputDir, progress, cancellationToken);
            }
        }

        private async Task<string> DecryptLegacyPKIFileAsync(string inputPath, CertificateInfo certificateInfo, string outputDir, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var fileData = await File.ReadAllBytesAsync(inputPath, cancellationToken);
            var pkiData = DeserializePKIData(fileData);
            
            // Verificar que el certificado coincida
            if (pkiData.CertificateThumbprint != certificateInfo.Thumbprint)
            {
                throw new InvalidOperationException("El certificado seleccionado no corresponde con el usado para encriptar este archivo.");
            }
            
            var decryptedData = _certificateService.DecryptWithCertificate(pkiData.EncryptedContent, certificateInfo.Certificate);
            
            var outputPath = Path.Combine(outputDir, pkiData.OriginalFileName);
            
            // Si el archivo ya existe, agregar un sufijo
            if (File.Exists(outputPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(pkiData.OriginalFileName);
                var extension = Path.GetExtension(pkiData.OriginalFileName);
                var counter = 1;
                
                do
                {
                    outputPath = Path.Combine(outputDir, $"{nameWithoutExt}_({counter}){extension}");
                    counter++;
                } while (File.Exists(outputPath));
            }
            
            await File.WriteAllBytesAsync(outputPath, decryptedData, cancellationToken);
            progress?.Report(100);
            
            return outputPath;
        }
        
        private void SelectCertificate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var certWindow = new CertificateSelectionWindow
                {
                    Owner = this
                };
                
                if (certWindow.ShowDialog() == true)
                {
                    _selectedEncryptCertificate = certWindow.SelectedCertificate;
                    if (_selectedEncryptCertificate != null)
                    {
                        SelectedCertificateTextBox.Text = _selectedEncryptCertificate.FriendlyName;
                        CertificateInfoText.Text = $"Válido hasta: {_selectedEncryptCertificate.ValidTo:dd/MM/yyyy}";
                        CertificateInfoText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error al seleccionar certificado: {ex.Message}", "Error", this);
            }
        }

        private async void GenerateCertificate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var certWindow = new CertificateGenerationWindow
                {
                    Owner = this
                };
                
                if (certWindow.ShowDialog() == true)
                {
                    // Actualizar la lista de certificados disponibles
                    // y seleccionar automáticamente el nuevo certificado si está instalado
                    try
                    {
                        // Esperar un momento para que el certificado se instale completamente
                        await System.Threading.Tasks.Task.Delay(500);
                        
                        var certificates = _certificateService.GetAvailableCertificates();
                        var latestCert = certificates
                            .Where(c => c.Certificate.NotBefore >= DateTime.Now.AddMinutes(-5) && c.HasPrivateKey)
                            .OrderByDescending(c => c.Certificate.NotBefore)
                            .FirstOrDefault();

                        if (latestCert != null)
                        {
                            _selectedEncryptCertificate = latestCert;
                            SelectedCertificateTextBox.Text = latestCert.FriendlyName;
                            CertificateInfoText.Text = $"✅ Certificado con clave privada - Válido hasta: {latestCert.ValidTo:dd/MM/yyyy}";
                            CertificateInfoText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
                            
                            CustomMessageBox.ShowSuccess("Certificado generado e instalado correctamente. Ya está seleccionado para encriptación.", "Certificado Listo", this);
                        }
                        else
                        {
                            CustomMessageBox.ShowInfo("Certificado generado exitosamente. Use el botón 'Seleccionar' para elegir el nuevo certificado.", "Información", this);
                        }
                    }
                    catch (Exception updateEx)
                    {
                        // Si no se puede actualizar automáticamente, mostrar mensaje informativo
                        CustomMessageBox.ShowInfo("Certificado generado exitosamente. Use el botón 'Seleccionar' para elegir el nuevo certificado.", "Información", this);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error al generar certificado: {ex.Message}", "Error", this);
            }
        }
        
        private void SelectDecryptCertificate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var certWindow = new CertificateSelectionWindow
                {
                    Owner = this
                };
                
                if (certWindow.ShowDialog() == true)
                {
                    _selectedDecryptCertificate = certWindow.SelectedCertificate;
                    if (_selectedDecryptCertificate != null)
                    {
                        DecryptCertificateTextBox.Text = _selectedDecryptCertificate.FriendlyName;
                        
                        if (!_selectedDecryptCertificate.HasPrivateKey)
                        {
                            CustomMessageBox.ShowWarning("El certificado seleccionado no tiene clave privada. Se requiere la clave privada para desencriptar.", "Advertencia", this);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error al seleccionar certificado: {ex.Message}", "Error", this);
            }
        }
        
        private void EncryptionMethod_Changed(object sender, RoutedEventArgs e)
        {
            if (PasswordEncryptPanel == null || CertificateEncryptPanel == null || SecurityTipsText == null) return;

            bool usePassword = UsePasswordEncryptRadio?.IsChecked == true;
            
            PasswordEncryptPanel.Visibility = usePassword ? Visibility.Visible : Visibility.Collapsed;
            CertificateEncryptPanel.Visibility = usePassword ? Visibility.Collapsed : Visibility.Visible;
            
            // Actualizar consejos de seguridad
            if (usePassword)
            {
                SecurityTipsText.Text = "• Mínimo 12 caracteres\n• Se genera frase de recuperación";
            }
            else
            {
                SecurityTipsText.Text = "• Certificado PKI para máxima seguridad\n• Requiere clave privada para desencriptar";
            }
        }
        
        private void DecryptionMethod_Changed(object sender, RoutedEventArgs e)
        {
            if (PasswordDecryptPanel == null || RecoveryPhraseDecryptPanel == null || CertificateDecryptPanel == null) return;

            bool usePassword = UsePasswordRadio?.IsChecked == true;
            bool useRecoveryPhrase = UseRecoveryPhraseRadio?.IsChecked == true;
            bool useCertificate = UseCertificateDecryptRadio?.IsChecked == true;
            
            PasswordDecryptPanel.Visibility = usePassword ? Visibility.Visible : Visibility.Collapsed;
            RecoveryPhraseDecryptPanel.Visibility = useRecoveryPhrase ? Visibility.Visible : Visibility.Collapsed;
            CertificateDecryptPanel.Visibility = useCertificate ? Visibility.Visible : Visibility.Collapsed;
        }
        
        #region PKI Data Serialization
        
        private class PKIFileData
        {
            public string OriginalFileName { get; set; }
            public string EncryptedContent { get; set; }
            public string CertificateThumbprint { get; set; }
            public DateTime EncryptedDate { get; set; }
        }
        
        private byte[] SerializePKIData(PKIFileData data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            
            // Agregar un encabezado para identificar archivos PKI
            var header = Encoding.UTF8.GetBytes("FILEENC_PKI_V1");
            var result = new byte[header.Length + jsonBytes.Length];
            
            Array.Copy(header, 0, result, 0, header.Length);
            Array.Copy(jsonBytes, 0, result, header.Length, jsonBytes.Length);
            
            return result;
        }
        
        private PKIFileData DeserializePKIData(byte[] data)
        {
            var header = Encoding.UTF8.GetBytes("FILEENC_PKI_V1");
            
            if (data.Length < header.Length)
                throw new InvalidDataException("El archivo no es un archivo PKI válido.");
            
            // Verificar encabezado
            for (int i = 0; i < header.Length; i++)
            {
                if (data[i] != header[i])
                    throw new InvalidDataException("El archivo no es un archivo PKI válido.");
            }
            
            var jsonBytes = new byte[data.Length - header.Length];
            Array.Copy(data, header.Length, jsonBytes, 0, jsonBytes.Length);
            
            var json = Encoding.UTF8.GetString(jsonBytes);
            return System.Text.Json.JsonSerializer.Deserialize<PKIFileData>(json) ?? 
                   throw new InvalidDataException("Error al deserializar datos PKI.");
        }
        
        #endregion
        
        #region Drag & Drop Helper Methods
        
        private async Task ProcessDecryptFileAsync(string filePath, string password)
        {
            var dir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
            
            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressSection.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            IProgress<double> progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
            });
            
            try
            {
                string output = await EncryptionService.DecryptFileAsync(filePath, password, dir, progress, _cts.Token);
                
                // Marcar como desencriptado en el historial
                await HistoryService.MarkAsDecryptedAsync(filePath, output);
                
                NotificationService.ShowDecryptionSuccess(Path.GetFileName(output), output);
                CustomMessageBox.ShowSuccess($"El archivo se ha desencriptado correctamente.\n\nUbicación: {output}", "Desencriptación Completada", this);
            }
            catch (OperationCanceledException)
            {
                NotificationService.ShowWarning("Operación Cancelada", "La desencriptación fue cancelada por el usuario.");
                CustomMessageBox.ShowWarning("La operación de desencriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (CryptographicException ex)
            {
                if (ex.Message.Contains("frase de recuperación"))
                {
                    NotificationService.ShowError("Frase de Recuperación Incorrecta", "La frase de recuperación ingresada no es válida.");
                    CustomMessageBox.ShowError("Frase de recuperación incorrecta o archivo corrupto.", "Error de Desencriptación", this);
                }
                else
                {
                    NotificationService.ShowError("Contraseña Incorrecta", "La contraseña ingresada no es válida para este archivo.");
                    CustomMessageBox.ShowPasswordError(this);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError("Error de Desencriptación", $"Error procesando {Path.GetFileName(filePath)}: {ex.Message}");
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

        private async Task ProcessDecryptFileWithCertificateAsync(string filePath, CertificateInfo certificateInfo)
        {
            var dir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
            
            _cts = new CancellationTokenSource();
            ProgressBar.Value = 0;
            ProgressSection.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;

            IProgress<double> progress = new Progress<double>(pct => 
            {
                ProgressBar.Value = pct;
            });
            
            try
            {
                string output = await DecryptFileWithCertificateAsync(filePath, certificateInfo, dir, progress, _cts.Token);
                
                // Marcar como desencriptado en el historial
                await HistoryService.MarkAsDecryptedAsync(filePath, output);
                
                NotificationService.ShowDecryptionSuccess(Path.GetFileName(output), output);
                CustomMessageBox.ShowSuccess($"El archivo se ha desencriptado correctamente usando certificado PKI.\n\nUbicación: {output}", "Desencriptación Completada", this);
            }
            catch (OperationCanceledException)
            {
                NotificationService.ShowWarning("Operación Cancelada", "La desencriptación fue cancelada por el usuario.");
                CustomMessageBox.ShowWarning("La operación de desencriptación fue cancelada por el usuario.", "Operación Cancelada", this);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError("Error de Desencriptación", $"Error procesando {Path.GetFileName(filePath)}: {ex.Message}");
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
        
        #endregion
        
        #endregion
    }
}
