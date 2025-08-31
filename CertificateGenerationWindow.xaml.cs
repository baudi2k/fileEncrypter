using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FileEncrypter.Services;
using Microsoft.Win32;

namespace FileEncrypter
{
    public partial class CertificateGenerationWindow : Window
    {
        private readonly CertificateService _certificateService;

        public CertificateGenerationWindow()
        {
            InitializeComponent();
            _certificateService = new CertificateService();
        }

        private void ExportToPfxCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PfxExportPanel.Visibility = Visibility.Visible;
        }

        private void ExportToPfxCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PfxExportPanel.Visibility = Visibility.Collapsed;
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar entradas
                if (string.IsNullOrWhiteSpace(CertificateNameTextBox.Text))
                {
                    CustomMessageBox.ShowError("Por favor ingrese un nombre para el certificado.", "Error de Validación", this);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SubjectNameTextBox.Text))
                {
                    CustomMessageBox.ShowError("Por favor ingrese el nombre del sujeto.", "Error de Validación", this);
                    return;
                }

                // Validar contraseñas PFX si está habilitado
                if (ExportToPfxCheckBox.IsChecked == true)
                {
                    if (string.IsNullOrEmpty(PfxPasswordBox.Password))
                    {
                        CustomMessageBox.ShowError("Por favor ingrese una contraseña para el archivo PFX.", "Error de Validación", this);
                        return;
                    }

                    if (PfxPasswordBox.Password != PfxPasswordConfirmBox.Password)
                    {
                        CustomMessageBox.ShowError("Las contraseñas no coinciden.", "Error de Validación", this);
                        return;
                    }

                    if (PfxPasswordBox.Password.Length < 6)
                    {
                        CustomMessageBox.ShowError("La contraseña debe tener al menos 6 caracteres.", "Error de Validación", this);
                        return;
                    }
                }

                // Obtener parámetros
                var certificateName = CertificateNameTextBox.Text.Trim();
                var subjectName = SubjectNameTextBox.Text.Trim();
                var keySize = GetSelectedKeySize();
                var validityYears = GetSelectedValidityYears();
                var installInStore = InstallInStoreCheckBox.IsChecked == true;
                var exportToPfx = ExportToPfxCheckBox.IsChecked == true;

                // Mostrar progreso
                ShowProgress("Generando certificado...");

                // Generar certificado en un hilo de fondo
                var certificate = await System.Threading.Tasks.Task.Run(() =>
                {
                    var cert = _certificateService.GenerateSelfSignedCertificate(
                        subjectName, 
                        keySize, 
                        validityYears, 
                        installInStore);

                    // Establecer nombre amigable personalizado
                    cert.FriendlyName = certificateName;
                    
                    // Reinstalar con el nombre amigable actualizado si está instalado
                    if (installInStore)
                    {
                        _certificateService.InstallCertificate(cert);
                    }

                    return cert;
                });

                // Exportar a PFX si está habilitado
                string pfxPath = null;
                if (exportToPfx)
                {
                    UpdateProgress("Exportando certificado a archivo PFX...");

                    var saveDialog = new SaveFileDialog
                    {
                        Title = "Guardar certificado PFX",
                        Filter = "Archivos PFX (*.pfx)|*.pfx|Todos los archivos (*.*)|*.*",
                        FileName = $"{certificateName.Replace(" ", "_")}.pfx"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        pfxPath = saveDialog.FileName;
                        await System.Threading.Tasks.Task.Run(() =>
                        {
                            _certificateService.ExportCertificateToPfx(certificate, pfxPath, PfxPasswordBox.Password);
                        });
                    }
                }

                HideProgress();

                // Mostrar resultado
                var message = $"✅ Certificado generado exitosamente\n\n";
                message += $"📋 Nombre: {certificateName}\n";
                message += $"🔑 Sujeto: {subjectName}\n";
                message += $"🔢 Tamaño de clave: {keySize} bits\n";
                message += $"📅 Válido por: {validityYears} años\n";
                message += $"🆔 Thumbprint: {certificate.Thumbprint}\n\n";

                if (installInStore)
                {
                    message += "📦 El certificado ha sido instalado en el almacén de certificados del usuario.\n";
                }

                if (exportToPfx && !string.IsNullOrEmpty(pfxPath))
                {
                    message += $"💾 Exportado a: {pfxPath}\n";
                }

                message += "\n⚠️ Guarde la información del certificado en un lugar seguro.";

                CustomMessageBox.ShowSuccess(message, "Certificado Generado", this);
                
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                HideProgress();
                CustomMessageBox.ShowError($"Error al generar el certificado:\n\n{ex.Message}", "Error", this);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private int GetSelectedKeySize()
        {
            return KeySizeComboBox.SelectedIndex switch
            {
                0 => 2048,
                1 => 3072,
                2 => 4096,
                _ => 2048
            };
        }

        private int GetSelectedValidityYears()
        {
            return ValidityYearsComboBox.SelectedIndex switch
            {
                0 => 1,
                1 => 2,
                2 => 5,
                3 => 10,
                4 => 20,
                _ => 5
            };
        }

        private void ShowProgress(string message)
        {
            ProgressText.Text = message;
            ProgressSection.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
            
            // Deshabilitar controles
            CertificateNameTextBox.IsEnabled = false;
            SubjectNameTextBox.IsEnabled = false;
            KeySizeComboBox.IsEnabled = false;
            ValidityYearsComboBox.IsEnabled = false;
            InstallInStoreCheckBox.IsEnabled = false;
            ExportToPfxCheckBox.IsEnabled = false;
            PfxPasswordBox.IsEnabled = false;
            PfxPasswordConfirmBox.IsEnabled = false;
        }

        private void UpdateProgress(string message)
        {
            ProgressText.Text = message;
        }

        private void HideProgress()
        {
            ProgressSection.Visibility = Visibility.Collapsed;
            ProgressBar.IsIndeterminate = false;
            
            // Rehabilitar controles
            CertificateNameTextBox.IsEnabled = true;
            SubjectNameTextBox.IsEnabled = true;
            KeySizeComboBox.IsEnabled = true;
            ValidityYearsComboBox.IsEnabled = true;
            InstallInStoreCheckBox.IsEnabled = true;
            ExportToPfxCheckBox.IsEnabled = true;
            PfxPasswordBox.IsEnabled = true;
            PfxPasswordConfirmBox.IsEnabled = true;
        }
    }
}