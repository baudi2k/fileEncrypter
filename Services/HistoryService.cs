using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FileEncrypter.Helpers;
using FileEncrypter.Models;

namespace FileEncrypter.Services
{
    public static class HistoryService
    {
        private static readonly string HistoryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileEncrypter",
            "History"
        );

        private static readonly string HistoryFileName = "encryption_history.json";
        private static string HistoryFilePath => Path.Combine(HistoryDirectory, HistoryFileName);

        /// <summary>
        /// Agrega una nueva entrada al historial de encriptación
        /// </summary>
        public static async Task<HistoryOperationResult> AddEncryptionEntryAsync(
            string originalFilePath,
            string encryptedFilePath,
            long originalFileSize)
        {
            try
            {
                var machineId = MachineIdentifierHelper.GetMachineIdentifier();
                var history = await LoadHistoryAsync();

                var entry = new EncryptionHistoryEntry
                {
                    OriginalFileName = Path.GetFileName(originalFilePath),
                    OriginalFilePath = originalFilePath,
                    EncryptedFilePath = encryptedFilePath,
                    EncryptionDate = DateTime.Now,
                    OriginalFileSize = originalFileSize,
                    MachineIdentifier = machineId
                };

                history.Entries.Add(entry);
                history.LastUpdated = DateTime.Now;

                await SaveHistoryAsync(history);
                return HistoryOperationResult.CreateSuccess(entry);
            }
            catch (Exception ex)
            {
                return HistoryOperationResult.CreateError($"Error agregando entrada al historial: {ex.Message}");
            }
        }

        /// <summary>
        /// Marca un archivo como desencriptado en el historial
        /// </summary>
        public static async Task<HistoryOperationResult> MarkAsDecryptedAsync(
            string encryptedFilePath,
            string decryptedFilePath)
        {
            try
            {
                var history = await LoadHistoryAsync();
                var entry = history.Entries.FirstOrDefault(e => 
                    string.Equals(e.EncryptedFilePath, encryptedFilePath, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    return HistoryOperationResult.CreateError("No se encontró la entrada en el historial");
                }

                entry.IsDecrypted = true;
                entry.DecryptionDate = DateTime.Now;
                entry.DecryptedFilePath = decryptedFilePath;

                history.LastUpdated = DateTime.Now;
                await SaveHistoryAsync(history);

                return HistoryOperationResult.CreateSuccess(entry);
            }
            catch (Exception ex)
            {
                return HistoryOperationResult.CreateError($"Error actualizando historial: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene todas las entradas del historial para esta máquina
        /// </summary>
        public static async Task<List<EncryptionHistoryEntry>> GetHistoryEntriesAsync()
        {
            try
            {
                var machineId = MachineIdentifierHelper.GetMachineIdentifier();
                var history = await LoadHistoryAsync();

                // Filtrar solo las entradas de esta máquina
                return history.Entries
                    .Where(e => e.MachineIdentifier == machineId)
                    .OrderByDescending(e => e.EncryptionDate)
                    .ToList();
            }
            catch
            {
                return new List<EncryptionHistoryEntry>();
            }
        }

        /// <summary>
        /// Elimina una entrada del historial
        /// </summary>
        public static async Task<HistoryOperationResult> RemoveEntryAsync(string entryId)
        {
            try
            {
                var history = await LoadHistoryAsync();
                var entry = history.Entries.FirstOrDefault(e => e.Id == entryId);

                if (entry == null)
                {
                    return HistoryOperationResult.CreateError("No se encontró la entrada en el historial");
                }

                history.Entries.Remove(entry);
                history.LastUpdated = DateTime.Now;

                await SaveHistoryAsync(history);
                return HistoryOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                return HistoryOperationResult.CreateError($"Error eliminando entrada: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpia todo el historial de esta máquina
        /// </summary>
        public static async Task<HistoryOperationResult> ClearHistoryAsync()
        {
            try
            {
                var machineId = MachineIdentifierHelper.GetMachineIdentifier();
                var history = await LoadHistoryAsync();

                // Remover solo las entradas de esta máquina
                history.Entries.RemoveAll(e => e.MachineIdentifier == machineId);
                history.LastUpdated = DateTime.Now;

                await SaveHistoryAsync(history);
                return HistoryOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                return HistoryOperationResult.CreateError($"Error limpiando historial: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene estadísticas del historial
        /// </summary>
        public static async Task<(int TotalEncrypted, int TotalDecrypted, long TotalSize)> GetStatisticsAsync()
        {
            try
            {
                var entries = await GetHistoryEntriesAsync();
                
                var totalEncrypted = entries.Count;
                var totalDecrypted = entries.Count(e => e.IsDecrypted);
                var totalSize = entries.Sum(e => e.OriginalFileSize);

                return (totalEncrypted, totalDecrypted, totalSize);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        private static async Task<EncryptionHistory> LoadHistoryAsync()
        {
            try
            {
                if (!File.Exists(HistoryFilePath))
                {
                    return new EncryptionHistory
                    {
                        MachineIdentifier = MachineIdentifierHelper.GetMachineIdentifier()
                    };
                }

                var json = await File.ReadAllTextAsync(HistoryFilePath);
                var history = JsonSerializer.Deserialize<EncryptionHistory>(json);
                
                return history ?? new EncryptionHistory
                {
                    MachineIdentifier = MachineIdentifierHelper.GetMachineIdentifier()
                };
            }
            catch
            {
                return new EncryptionHistory
                {
                    MachineIdentifier = MachineIdentifierHelper.GetMachineIdentifier()
                };
            }
        }

        private static async Task SaveHistoryAsync(EncryptionHistory history)
        {
            Directory.CreateDirectory(HistoryDirectory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(history, options);
            await File.WriteAllTextAsync(HistoryFilePath, json);
        }
    }
} 