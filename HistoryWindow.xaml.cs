using FileEncrypter.Helpers;
using FileEncrypter.Models;
using FileEncrypter.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FileEncrypter
{
    public partial class HistoryWindow : Window
    {
        private ObservableCollection<HistoryEntryViewModel> _allEntries = new();
        private ObservableCollection<HistoryEntryViewModel> _filteredEntries = new();

        public HistoryWindow()
        {
            InitializeComponent();
            Loaded += HistoryWindow_Loaded;
        }

        private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mostrar ID de máquina
                var machineId = MachineIdentifierHelper.GetMachineIdentifier();
                if (MachineIdText != null)
                {
                    MachineIdText.Text = $"ID de Máquina: {machineId}";
                }

                // Cargar datos
                await LoadHistoryData();
                await UpdateStatistics();

                // Configurar DataGrid
                if (HistoryDataGrid != null)
                {
                    HistoryDataGrid.ItemsSource = _filteredEntries;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error inicializando ventana de historial: {ex.Message}", "Error", this);
            }
        }

        private async System.Threading.Tasks.Task LoadHistoryData()
        {
            try
            {
                var entries = await HistoryService.GetHistoryEntriesAsync();
                
                _allEntries.Clear();
                foreach (var entry in entries)
                {
                    _allEntries.Add(new HistoryEntryViewModel(entry));
                }

                ApplyFilters();
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error cargando historial: {ex.Message}", "Error", this);
            }
        }

        private async System.Threading.Tasks.Task UpdateStatistics()
        {
            try
            {
                var (totalEncrypted, totalDecrypted, totalSize) = await HistoryService.GetStatisticsAsync();
                
                if (TotalEncryptedText != null)
                    TotalEncryptedText.Text = totalEncrypted.ToString();
                    
                if (TotalDecryptedText != null)
                    TotalDecryptedText.Text = totalDecrypted.ToString();
                    
                if (TotalSizeText != null)
                    TotalSizeText.Text = FormatFileSize(totalSize);
                
                var successRate = totalEncrypted > 0 ? (double)totalDecrypted / totalEncrypted * 100 : 0;
                if (SuccessRateText != null)
                    SuccessRateText.Text = $"{successRate:F1}%";
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Error actualizando estadísticas: {ex.Message}", "Error", this);
            }
        }

        private void ApplyFilters()
        {
            _filteredEntries.Clear();

            var searchText = SearchTextBox?.Text?.ToLower() ?? "";
            var filterIndex = FilterComboBox?.SelectedIndex ?? 0;

            var filtered = _allEntries.AsEnumerable();

            // Aplicar filtro de búsqueda
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(e => 
                    e.OriginalFileName.ToLower().Contains(searchText) ||
                    e.OriginalFilePath.ToLower().Contains(searchText));
            }

            // Aplicar filtro por estado
            switch (filterIndex)
            {
                case 1: // Solo encriptados
                    filtered = filtered.Where(e => !e.IsDecrypted);
                    break;
                case 2: // Solo desencriptados
                    filtered = filtered.Where(e => e.IsDecrypted);
                    break;
            }

            foreach (var entry in filtered.OrderByDescending(e => e.EncryptionDate))
            {
                _filteredEntries.Add(entry);
            }

            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            var hasEntries = _filteredEntries.Count > 0;
            
            if (HistoryDataGrid != null)
                HistoryDataGrid.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
                
            if (EmptyStatePanel != null)
                EmptyStatePanel.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadHistoryData();
            await UpdateStatistics();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.ShowQuestion(
                "¿Estás seguro de que quieres eliminar todo el historial de encriptación?\n\nEsta acción no se puede deshacer.",
                "Confirmar Limpieza de Historial",
                this);

            if (result == MessageBoxResult.Yes)
            {
                var clearResult = await HistoryService.ClearHistoryAsync();
                if (clearResult.Success)
                {
                    await LoadHistoryData();
                    await UpdateStatistics();
                    CustomMessageBox.ShowSuccess("Historial limpiado exitosamente.", "Historial Limpiado", this);
                }
                else
                {
                    CustomMessageBox.ShowError($"Error limpiando historial: {clearResult.ErrorMessage}", "Error", this);
                }
            }
        }

        private void OpenEncryptedLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HistoryEntryViewModel entry)
            {
                try
                {
                    if (File.Exists(entry.EncryptedFilePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{entry.EncryptedFilePath}\"");
                    }
                    else
                    {
                        CustomMessageBox.ShowWarning("El archivo encriptado no existe en la ubicación especificada.", "Archivo No Encontrado", this);
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.ShowError($"Error abriendo ubicación: {ex.Message}", "Error", this);
                }
            }
        }

        private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HistoryEntryViewModel entry)
            {
                var result = CustomMessageBox.ShowQuestion(
                    $"¿Estás seguro de que quieres eliminar '{entry.OriginalFileName}' del historial?",
                    "Confirmar Eliminación",
                    this);

                if (result == MessageBoxResult.Yes)
                {
                    var deleteResult = await HistoryService.RemoveEntryAsync(entry.Id);
                    if (deleteResult.Success)
                    {
                        _allEntries.Remove(entry);
                        _filteredEntries.Remove(entry);
                        await UpdateStatistics();
                        UpdateEmptyState();
                    }
                    else
                    {
                        CustomMessageBox.ShowError($"Error eliminando entrada: {deleteResult.ErrorMessage}", "Error", this);
                    }
                }
            }
        }

        private void HistoryDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (HistoryDataGrid.SelectedItem is HistoryEntryViewModel entry)
            {
                ShowEntryDetails(entry);
            }
        }

        private void ShowEntryDetails(HistoryEntryViewModel entry)
        {
            var details = $"📁 Archivo: {entry.OriginalFileName}\n" +
                         $"📂 Ubicación Original: {entry.OriginalFilePath}\n" +
                         $"🔒 Archivo Encriptado: {entry.EncryptedFilePath}\n" +
                         $"📅 Fecha de Encriptación: {entry.EncryptionDate:dd/MM/yyyy HH:mm:ss}\n" +
                         $"💾 Tamaño: {entry.FormattedFileSize}\n" +
                         $"🔓 Estado: {entry.StatusText}\n";

            if (entry.IsDecrypted && entry.DecryptionDate.HasValue)
            {
                details += $"📅 Fecha de Desencriptación: {entry.DecryptionDate.Value:dd/MM/yyyy HH:mm:ss}\n";
                if (!string.IsNullOrEmpty(entry.DecryptedFilePath))
                {
                    details += $"📂 Ubicación Desencriptado: {entry.DecryptedFilePath}\n";
                }
            }

            CustomMessageBox.ShowInfo(details, "Detalles del Archivo", this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }

    public class HistoryEntryViewModel
    {
        private readonly EncryptionHistoryEntry _entry;

        public HistoryEntryViewModel(EncryptionHistoryEntry entry)
        {
            _entry = entry;
        }

        public string Id => _entry.Id;
        public string OriginalFileName => _entry.OriginalFileName;
        public string OriginalFilePath => _entry.OriginalFilePath;
        public string EncryptedFilePath => _entry.EncryptedFilePath;
        public DateTime EncryptionDate => _entry.EncryptionDate;
        public long OriginalFileSize => _entry.OriginalFileSize;
        public bool IsDecrypted => _entry.IsDecrypted;
        public DateTime? DecryptionDate => _entry.DecryptionDate;
        public string? DecryptedFilePath => _entry.DecryptedFilePath;

        public string FormattedFileSize
        {
            get
            {
                if (OriginalFileSize < 1024) return $"{OriginalFileSize} B";
                if (OriginalFileSize < 1024 * 1024) return $"{OriginalFileSize / 1024.0:F1} KB";
                if (OriginalFileSize < 1024 * 1024 * 1024) return $"{OriginalFileSize / (1024.0 * 1024.0):F1} MB";
                return $"{OriginalFileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        public string StatusText => IsDecrypted ? "✅ Desencriptado" : "🔒 Encriptado";
    }
} 