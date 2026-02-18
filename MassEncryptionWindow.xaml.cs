using FileEncrypter.Helpers;
using FileEncrypter.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileEncrypter
{
    public partial class MassEncryptionWindow : Window
    {
        private bool _isPasswordVisible = false;
        private TextBox? _passwordTextBox;
        private CertificateInfo? _selectedCertificate;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isEncrypting = false;

        public MassEncryptionWindow()
        {
            InitializeComponent();
            Loaded += MassEncryptionWindow_Loaded;
        }

        private void MassEncryptionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
            EncryptionMethod_Changed(null, null);
            PasswordInput.PasswordChanged += PasswordInput_PasswordChanged;
        }

        private void SelectSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Seleccionar Carpeta de Origen",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                SourceFolderTextBox.Text = dialog.FolderName;
                StatsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void SelectOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Seleccionar Carpeta de Destino",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                OutputFolderTextBox.Text = dialog.FolderName;
            }
        }

        private void EncryptionMethod_Changed(object? sender, RoutedEventArgs e)
        {
            if (UsePasswordRadio.IsChecked == true)
            {
                PasswordPanel.Visibility = Visibility.Visible;
                CertificatePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PasswordPanel.Visibility = Visibility.Collapsed;
                CertificatePanel.Visibility = Visibility.Visible;
            }
        }

        private void ShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (_passwordTextBox == null)
            {
                _passwordTextBox = new TextBox
                {
                    Style = (Style)FindResource("ModernTextBox"),
                    Height = 48
                };
            }

            if (_isPasswordVisible)
            {
                // Ocultar contraseña
                PasswordInput.Visibility = Visibility.Visible;
                _passwordTextBox.Visibility = Visibility.Collapsed;
                ShowPasswordButton.Content = "👁️";
                _isPasswordVisible = false;
            }
            else
            {
                // Mostrar contraseña
                _passwordTextBox.Text = PasswordInput.Password;
                _passwordTextBox.TextChanged += (s, args) => PasswordInput.Password = _passwordTextBox.Text;
                
                PasswordInput.Visibility = Visibility.Collapsed;
                _passwordTextBox.Visibility = Visibility.Visible;
                ShowPasswordButton.Content = "🙈";
                _isPasswordVisible = true;
            }
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
        }

        private void UpdatePasswordStrength()
        {
            var password = PasswordInput.Password;
            var strength = PasswordHelper.CalculatePasswordStrength(password);
            
            PasswordStrengthBar.Value = strength.Score;
            
            if (strength.Score < 30)
            {
                PasswordStrengthText.Text = "Débil";
                PasswordStrengthText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else if (strength.Score < 70)
            {
                PasswordStrengthText.Text = "Media";
                PasswordStrengthText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
            else
            {
                PasswordStrengthText.Text = "Fuerte";
                PasswordStrengthText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
        }

        private void SelectCertificate_Click(object sender, RoutedEventArgs e)
        {
            var certificateWindow = new CertificateSelectionWindow();
            if (certificateWindow.ShowDialog() == true)
            {
                _selectedCertificate = certificateWindow.SelectedCertificate;
                if (_selectedCertificate != null)
                {
                    SelectedCertificateTextBox.Text = $"{_selectedCertificate.Subject} ({_selectedCertificate.Thumbprint[..8]}...)";
                }
            }
        }

        private void GenerateCertificate_Click(object sender, RoutedEventArgs e)
        {
            var certificateWindow = new CertificateGenerationWindow();
            if (certificateWindow.ShowDialog() == true)
            {
                _selectedCertificate = certificateWindow.GeneratedCertificate;
                if (_selectedCertificate != null)
                {
                    SelectedCertificateTextBox.Text = $"{_selectedCertificate.Subject} ({_selectedCertificate.Thumbprint[..8]}...)";
                }
            }
        }

        private void MaxFileSizeSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            var value = (int)MaxFileSizeSlider.Value;
            switch (value)
            {
                case 0:
                    MaxFileSizeLabel.Text = "Sin límite";
                    break;
                case 1:
                    MaxFileSizeLabel.Text = "10 MB";
                    break;
                case 2:
                    MaxFileSizeLabel.Text = "50 MB";
                    break;
                case 3:
                    MaxFileSizeLabel.Text = "100 MB";
                    break;
                case 4:
                    MaxFileSizeLabel.Text = "500 MB";
                    break;
                case 5:
                    MaxFileSizeLabel.Text = "1 GB";
                    break;
                case 6:
                    MaxFileSizeLabel.Text = "2 GB";
                    break;
                case 7:
                    MaxFileSizeLabel.Text = "5 GB";
                    break;
                case 8:
                    MaxFileSizeLabel.Text = "10 GB";
                    break;
                case 9:
                    MaxFileSizeLabel.Text = "50 GB";
                    break;
                case 10:
                    MaxFileSizeLabel.Text = "100 GB";
                    break;
            }
        }

        private async void AnalyzeFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SourceFolderTextBox.Text) || !Directory.Exists(SourceFolderTextBox.Text))
            {
                MessageBox.Show("Por favor selecciona una carpeta válida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AnalyzeButton.IsEnabled = false;
            AnalyzeButton.Content = "🔍 Analizando...";

            try
            {
                var config = CreateEncryptionConfig();
                var stats = await MassEncryptionService.GetFolderStatsAsync(config);

                if (!string.IsNullOrEmpty(stats.ErrorMessage))
                {
                    MessageBox.Show($"Error analizando carpeta: {stats.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Actualizar UI con estadísticas
                TotalFilesText.Text = stats.TotalFiles.ToString();
                TotalSizeText.Text = stats.FormattedSize;
                EstimatedTimeText.Text = stats.FormattedEstimatedTime;

                StatsGrid.Visibility = Visibility.Visible;

                // Mostrar tipos de archivo más comunes
                if (stats.FileTypes.Count > 0)
                {
                    var topTypes = stats.FileTypes.OrderByDescending(x => x.Value).Take(5);
                    var typesText = string.Join(", ", topTypes.Select(x => $"{x.Key} ({x.Value})"));
                    
                    // Podrías mostrar esto en un tooltip o label adicional
                    ToolTipService.SetToolTip(TotalFilesText, $"Tipos de archivo: {typesText}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error analizando carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AnalyzeButton.IsEnabled = true;
                AnalyzeButton.Content = "🔍 Analizar Carpeta";
            }
        }

        private async void StartMassEncryption_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

            _isEncrypting = true;
            StartButton.IsEnabled = false;
            StartButton.Content = "⏳ Encriptando...";
            CancelButton.IsEnabled = true;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var config = CreateEncryptionConfig();
                var progress = new Progress<MassEncryptionService.MassEncryptionProgress>(UpdateProgress);

                var result = await MassEncryptionService.EncryptFolderAsync(
                    config, 
                    progress, 
                    _cancellationTokenSource.Token);

                if (result.Success)
                {
                    var message = $"Encriptación completada exitosamente!\n\n" +
                                $"Archivos procesados: {result.EncryptedFiles}/{result.TotalFiles}\n" +
                                $"Archivos fallidos: {result.FailedFiles}\n" +
                                $"Tiempo total: {result.TotalTime.TotalMinutes:F1} minutos\n" +
                                $"Tamaño procesado: {FormatFileSize(result.TotalSizeProcessed)}";

                    if (result.FailedFiles > 0)
                    {
                        message += $"\n\nArchivos con errores:\n{string.Join("\n", result.FailedFilePaths.Take(5))}";
                        if (result.FailedFilePaths.Count > 5)
                            message += $"\n... y {result.FailedFilePaths.Count - 5} más";
                    }

                    MessageBox.Show(message, "Encriptación Completada", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Mostrar notificación
                    NotificationService.ShowEncryptionSuccess(
                        $"Carpeta completa", 
                        config.OutputFolder, 
                        config.RecoveryPhrase);
                }
                else
                {
                    MessageBox.Show($"Error en la encriptación: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Encriptación cancelada por el usuario.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error durante la encriptación: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isEncrypting = false;
                StartButton.IsEnabled = true;
                StartButton.Content = "🚀 Iniciar Encriptación Masiva";
                CancelButton.IsEnabled = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        private void UpdateProgress(MassEncryptionService.MassEncryptionProgress progress)
        {
            Dispatcher.Invoke(() =>
            {
                StartButton.Content = $"⏳ Encriptando... {progress.CurrentFile}/{progress.TotalFiles}";
                
                // Actualizar título de la ventana con progreso
                Title = $"Encriptación Masiva - {progress.Percentage:F0}% - {progress.CurrentFileName}";
            });
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isEncrypting)
            {
                _cancellationTokenSource?.Cancel();
            }
            else
            {
                Close();
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrEmpty(SourceFolderTextBox.Text) || !Directory.Exists(SourceFolderTextBox.Text))
            {
                MessageBox.Show("Por favor selecciona una carpeta de origen válida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrEmpty(OutputFolderTextBox.Text) || !Directory.Exists(OutputFolderTextBox.Text))
            {
                MessageBox.Show("Por favor selecciona una carpeta de destino válida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (UsePasswordRadio.IsChecked == true)
            {
                if (string.IsNullOrEmpty(PasswordInput.Password))
                {
                    MessageBox.Show("Por favor ingresa una contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var strength = PasswordHelper.CalculatePasswordStrength(PasswordInput.Password);
                if (strength.Score < 30)
                {
                    var result = MessageBox.Show(
                        "La contraseña es muy débil. ¿Deseas continuar de todos modos?",
                        "Contraseña Débil",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.No)
                        return false;
                }
            }
            else
            {
                if (_selectedCertificate == null)
                {
                    MessageBox.Show("Por favor selecciona un certificado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private MassEncryptionService.MassEncryptionConfig CreateEncryptionConfig()
        {
            var config = new MassEncryptionService.MassEncryptionConfig
            {
                SourceFolder = SourceFolderTextBox.Text,
                OutputFolder = OutputFolderTextBox.Text,
                DeleteOriginalFiles = DeleteOriginalCheckBox.IsChecked == true,
                IncludeSubfolders = IncludeSubfoldersCheckBox.IsChecked == true,
                PreserveFolderStructure = PreserveStructureCheckBox.IsChecked == true
            };

            // Configurar método de encriptación
            if (UsePasswordRadio.IsChecked == true)
            {
                config.Password = PasswordInput.Password;
                var recoveryResult = RecoveryPhraseHelper.GenerateRecoveryPhrase();
                config.RecoveryPhrase = recoveryResult.Success ? recoveryResult.Phrase : null;
            }
            else
            {
                config.Certificate = _selectedCertificate;
            }

            // Configurar filtros de archivos
            if (!string.IsNullOrEmpty(AllowedExtensionsTextBox.Text) && 
                AllowedExtensionsTextBox.Text != "Dejar vacío para permitir todas")
            {
                config.AllowedExtensions = AllowedExtensionsTextBox.Text
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().ToLowerInvariant())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(ExcludedExtensionsTextBox.Text) && 
                ExcludedExtensionsTextBox.Text != "Una extensión por línea (ej: .tmp, .log)")
            {
                config.ExcludedExtensions = ExcludedExtensionsTextBox.Text
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().ToLowerInvariant())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            // Configurar tamaño máximo
            var maxSizeValue = (int)MaxFileSizeSlider.Value;
            config.MaxFileSizeBytes = maxSizeValue switch
            {
                0 => 0,
                1 => 10 * 1024 * 1024,
                2 => 50 * 1024 * 1024,
                3 => 100 * 1024 * 1024,
                4 => 500 * 1024 * 1024,
                5 => 1024 * 1024 * 1024,
                6 => 2L * 1024 * 1024 * 1024,
                7 => 5L * 1024 * 1024 * 1024,
                8 => 10L * 1024 * 1024 * 1024,
                9 => 50L * 1024 * 1024 * 1024,
                10 => 100L * 1024 * 1024 * 1024,
                _ => 0
            };

            return config;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }
    }
}
