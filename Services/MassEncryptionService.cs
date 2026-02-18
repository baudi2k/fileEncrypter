using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileEncrypter.Helpers;
using FileEncrypter.Models;

namespace FileEncrypter.Services
{
    /// <summary>
    /// Servicio para manejar encriptación masiva de carpetas
    /// </summary>
    public static class MassEncryptionService
    {
        /// <summary>
        /// Resultado de una operación de encriptación masiva
        /// </summary>
        public class MassEncryptionResult
        {
            public bool Success { get; set; }
            public int TotalFiles { get; set; }
            public int EncryptedFiles { get; set; }
            public int FailedFiles { get; set; }
            public List<string> FailedFilePaths { get; set; } = new List<string>();
            public List<string> EncryptedFilePaths { get; set; } = new List<string>();
            public string? ErrorMessage { get; set; }
            public TimeSpan TotalTime { get; set; }
            public long TotalSizeProcessed { get; set; }
        }

        /// <summary>
        /// Progreso de encriptación masiva
        /// </summary>
        public class MassEncryptionProgress
        {
            public int CurrentFile { get; set; }
            public int TotalFiles { get; set; }
            public string CurrentFileName { get; set; } = string.Empty;
            public double Percentage { get; set; }
            public bool IsCancelled { get; set; }
        }

        /// <summary>
        /// Configuración para encriptación masiva
        /// </summary>
        public class MassEncryptionConfig
        {
            public string SourceFolder { get; set; } = string.Empty;
            public string OutputFolder { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? RecoveryPhrase { get; set; }
            public CertificateInfo? Certificate { get; set; }
            public bool DeleteOriginalFiles { get; set; }
            public bool IncludeSubfolders { get; set; } = true;
            public List<string> AllowedExtensions { get; set; } = new List<string>();
            public List<string> ExcludedExtensions { get; set; } = new List<string>();
            public long MaxFileSizeBytes { get; set; } = 0; // 0 = sin límite
            public bool PreserveFolderStructure { get; set; } = true;
        }

        /// <summary>
        /// Encripta todos los archivos de una carpeta de forma masiva
        /// </summary>
        public static async Task<MassEncryptionResult> EncryptFolderAsync(
            MassEncryptionConfig config,
            IProgress<MassEncryptionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new MassEncryptionResult();
            var startTime = DateTime.Now;

            try
            {
                // Validar configuración
                if (!Directory.Exists(config.SourceFolder))
                {
                    result.ErrorMessage = "La carpeta de origen no existe";
                    return result;
                }

                if (!Directory.Exists(config.OutputFolder))
                {
                    Directory.CreateDirectory(config.OutputFolder);
                }

                // Obtener lista de archivos
                var files = GetFilesToEncrypt(config);
                result.TotalFiles = files.Count;

                if (files.Count == 0)
                {
                    result.ErrorMessage = "No se encontraron archivos para encriptar";
                    return result;
                }

                // Procesar archivos
                for (int i = 0; i < files.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.ErrorMessage = "Operación cancelada por el usuario";
                        break;
                    }

                    var filePath = files[i];
                    var fileName = Path.GetFileName(filePath);

                    // Actualizar progreso
                    progress?.Report(new MassEncryptionProgress
                    {
                        CurrentFile = i + 1,
                        TotalFiles = files.Count,
                        CurrentFileName = fileName,
                        Percentage = (double)(i + 1) / files.Count * 100,
                        IsCancelled = false
                    });

                    try
                    {
                        // Determinar ruta de salida
                        var relativePath = Path.GetRelativePath(config.SourceFolder, filePath);
                        var outputPath = config.PreserveFolderStructure
                            ? Path.Combine(config.OutputFolder, relativePath + ".enc")
                            : Path.Combine(config.OutputFolder, Path.GetFileName(filePath) + ".enc");

                        // Crear directorio de salida si es necesario
                        var outputDir = Path.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        // Encriptar archivo
                        var fileInfo = new FileInfo(filePath);
                        var encryptionResult = await EncryptSingleFileAsync(filePath, outputPath, config);

                        if (encryptionResult.Success)
                        {
                            result.EncryptedFiles++;
                            result.EncryptedFilePaths.Add(outputPath);
                            result.TotalSizeProcessed += fileInfo.Length;

                            // Eliminar archivo original si se solicita
                            if (config.DeleteOriginalFiles)
                            {
                                File.Delete(filePath);
                            }
                        }
                        else
                        {
                            result.FailedFiles++;
                            result.FailedFilePaths.Add(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedFiles++;
                        result.FailedFilePaths.Add(filePath);
                        System.Diagnostics.Debug.WriteLine($"Error encriptando {filePath}: {ex.Message}");
                    }
                }

                result.Success = result.EncryptedFiles > 0;
                result.TotalTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Obtiene la lista de archivos a encriptar según la configuración
        /// </summary>
        private static List<string> GetFilesToEncrypt(MassEncryptionConfig config)
        {
            var files = new List<string>();
            var searchOption = config.IncludeSubfolders 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;

            try
            {
                var allFiles = Directory.GetFiles(config.SourceFolder, "*", searchOption);

                foreach (var file in allFiles)
                {
                    // Verificar tamaño máximo
                    if (config.MaxFileSizeBytes > 0)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Length > config.MaxFileSizeBytes)
                        {
                            continue;
                        }
                    }

                    // Verificar extensiones permitidas
                    if (config.AllowedExtensions.Count > 0)
                    {
                        var extension = Path.GetExtension(file).ToLowerInvariant();
                        if (!config.AllowedExtensions.Contains(extension))
                        {
                            continue;
                        }
                    }

                    // Verificar extensiones excluidas
                    if (config.ExcludedExtensions.Count > 0)
                    {
                        var extension = Path.GetExtension(file).ToLowerInvariant();
                        if (config.ExcludedExtensions.Contains(extension))
                        {
                            continue;
                        }
                    }

                    // Excluir archivos ya encriptados
                    if (file.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    files.Add(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo archivos: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// Encripta un archivo individual
        /// </summary>
        private static async Task<EncryptionResult> EncryptSingleFileAsync(
            string inputPath, 
            string outputPath, 
            MassEncryptionConfig config)
        {
            try
            {
                if (config.Certificate != null)
                {
                    return await EncryptionService.EncryptFileWithCertificateAsync(
                        inputPath, outputPath, config.Certificate);
                }
                else
                {
                    return await EncryptionService.EncryptFileWithPasswordAsync(
                        inputPath, outputPath, config.Password, config.RecoveryPhrase);
                }
            }
            catch (Exception ex)
            {
                return EncryptionResult.CreateError($"Error encriptando archivo: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene estadísticas de una carpeta antes de encriptar
        /// </summary>
        public static async Task<FolderEncryptionStats> GetFolderStatsAsync(MassEncryptionConfig config)
        {
            var stats = new FolderEncryptionStats();

            try
            {
                var files = GetFilesToEncrypt(config);
                
                stats.TotalFiles = files.Count;
                stats.TotalSize = 0;
                stats.FileTypes = new Dictionary<string, int>();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    stats.TotalSize += fileInfo.Length;

                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension))
                    {
                        extension = "sin extensión";
                    }

                    if (stats.FileTypes.ContainsKey(extension))
                    {
                        stats.FileTypes[extension]++;
                    }
                    else
                    {
                        stats.FileTypes[extension] = 1;
                    }
                }

                stats.EstimatedTime = EstimateEncryptionTime(stats.TotalSize, stats.TotalFiles);
            }
            catch (Exception ex)
            {
                stats.ErrorMessage = ex.Message;
            }

            return stats;
        }

        /// <summary>
        /// Estima el tiempo de encriptación basado en el tamaño y cantidad de archivos
        /// </summary>
        private static TimeSpan EstimateEncryptionTime(long totalSizeBytes, int fileCount)
        {
            // Estimación basada en ~50MB/s para archivos pequeños y ~100MB/s para archivos grandes
            var baseSpeed = totalSizeBytes / (50 * 1024 * 1024); // 50MB/s base
            var overheadTime = fileCount * 0.1; // 0.1 segundos por archivo de overhead
            
            return TimeSpan.FromSeconds(Math.Max(1, baseSpeed + overheadTime));
        }
    }

    /// <summary>
    /// Estadísticas de una carpeta para encriptación
    /// </summary>
    public class FolderEncryptionStats
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public Dictionary<string, int> FileTypes { get; set; } = new Dictionary<string, int>();
        public TimeSpan EstimatedTime { get; set; }
        public string? ErrorMessage { get; set; }

        public string FormattedSize => FormatFileSize(TotalSize);
        public string FormattedEstimatedTime => EstimatedTime.TotalMinutes < 1 
            ? $"{EstimatedTime.TotalSeconds:F0} segundos"
            : $"{EstimatedTime.TotalMinutes:F1} minutos";

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
    }
}
