using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileEncrypter.Services;

namespace FileEncrypter
{
    public partial class CertificateSelectionWindow : Window
    {
        private CertificateService _certificateService;
        private List<CertificateInfo> _allCertificates;
        private ObservableCollection<CertificateInfo> _filteredCertificates;
        private CertificateInfo _selectedCertificate;

        public CertificateInfo SelectedCertificate => _selectedCertificate;

        public CertificateSelectionWindow()
        {
            InitializeComponent();
            _certificateService = new CertificateService();
            _filteredCertificates = new ObservableCollection<CertificateInfo>();
            CertificatesDataGrid.ItemsSource = _filteredCertificates;
            
            Loaded += async (s, e) => await LoadCertificatesAsync();
        }

        private async Task LoadCertificatesAsync()
        {
            try
            {
                ShowLoading(true);
                
                await Task.Run(() =>
                {
                    _allCertificates = _certificateService.GetAvailableCertificates();
                });

                FilterCertificates();
                
                if (_allCertificates?.Count == 0)
                {
                    ShowNoCertificatesMessage(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar certificados: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void FilterCertificates()
        {
            if (_allCertificates == null) return;

            var searchText = SearchTextBox.Text?.ToLower() ?? "";
            var showExpired = ShowExpiredCheckBox.IsChecked == true;

            var filtered = _allCertificates.Where(cert =>
            {
                // Filtro de búsqueda
                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchIn = $"{cert.FriendlyName} {cert.Subject} {cert.Issuer}".ToLower();
                    if (!searchIn.Contains(searchText))
                        return false;
                }

                // Filtro de expiración
                if (!showExpired && cert.ValidTo < DateTime.Now)
                    return false;

                return true;
            }).ToList();

            _filteredCertificates.Clear();
            foreach (var cert in filtered)
            {
                _filteredCertificates.Add(cert);
            }

            ShowNoCertificatesMessage(_filteredCertificates.Count == 0);
        }

        private void ShowLoading(bool show)
        {
            LoadingPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CertificatesDataGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowNoCertificatesMessage(bool show)
        {
            NoCertificatesPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CertificatesDataGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterCertificates();
        }

        private void ShowExpired_Changed(object sender, RoutedEventArgs e)
        {
            FilterCertificates();
        }

        private async void RefreshCertificates_Click(object sender, RoutedEventArgs e)
        {
            await LoadCertificatesAsync();
        }

        private void CertificatesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = CertificatesDataGrid.SelectedItem as CertificateInfo;
            ShowCertificateDetails(selected);
            SelectButton.IsEnabled = selected != null;
        }

        private void ShowCertificateDetails(CertificateInfo certificate)
        {
            if (certificate == null)
            {
                DetailsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            DetailsPanel.Visibility = Visibility.Visible;
            SubjectText.Text = certificate.Subject;
            IssuerText.Text = certificate.Issuer;
            ValidityText.Text = $"{certificate.ValidFrom:dd/MM/yyyy} - {certificate.ValidTo:dd/MM/yyyy}";
            ThumbprintText.Text = FormatThumbprint(certificate.Thumbprint);

            // Cambiar color si está expirado
            if (certificate.ValidTo < DateTime.Now)
            {
                ValidityText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Rojo
            }
            else
            {
                ValidityText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
            }
        }

        private string FormatThumbprint(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint)) return "";
            
            // Formatear como XX XX XX XX...
            return string.Join(" ", Enumerable.Range(0, thumbprint.Length / 2)
                .Select(i => thumbprint.Substring(i * 2, 2)));
        }

        private void CertificatesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CertificatesDataGrid.SelectedItem != null)
            {
                Select_Click(sender, e);
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            _selectedCertificate = CertificatesDataGrid.SelectedItem as CertificateInfo;
            if (_selectedCertificate != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ManageCertificates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Abrir el administrador de certificados de Windows
                Process.Start("certmgr.msc");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el administrador de certificados: {ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

}