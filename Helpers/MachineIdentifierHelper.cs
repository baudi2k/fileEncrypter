using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace FileEncrypter.Helpers
{
    public static class MachineIdentifierHelper
    {
        private static string? _cachedIdentifier;

        /// <summary>
        /// Obtiene un identificador único para esta máquina basado en información del hardware
        /// </summary>
        public static string GetMachineIdentifier()
        {
            if (_cachedIdentifier != null)
                return _cachedIdentifier;

            try
            {
                var identifierParts = new StringBuilder();

                // Obtener ID del procesador
                var cpuId = GetCpuId();
                if (!string.IsNullOrEmpty(cpuId))
                    identifierParts.Append(cpuId);

                // Obtener ID de la placa madre
                var motherboardId = GetMotherboardId();
                if (!string.IsNullOrEmpty(motherboardId))
                    identifierParts.Append(motherboardId);

                // Obtener ID del primer disco duro
                var diskId = GetDiskId();
                if (!string.IsNullOrEmpty(diskId))
                    identifierParts.Append(diskId);

                // Si no se pudo obtener información del hardware, usar nombre de máquina como fallback
                if (identifierParts.Length == 0)
                {
                    identifierParts.Append(Environment.MachineName);
                    identifierParts.Append(Environment.UserName);
                }

                // Crear hash SHA256 del identificador combinado
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identifierParts.ToString()));
                    _cachedIdentifier = Convert.ToBase64String(hash)[..16]; // Usar solo los primeros 16 caracteres
                }

                return _cachedIdentifier;
            }
            catch (Exception)
            {
                // Fallback en caso de error
                _cachedIdentifier = Environment.MachineName + "_" + Environment.UserName;
                return _cachedIdentifier;
            }
        }

        private static string GetCpuId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var processorId = obj["ProcessorId"]?.ToString();
                    if (!string.IsNullOrEmpty(processorId))
                        return processorId;
                }
            }
            catch
            {
                // Ignorar errores
            }

            return string.Empty;
        }

        private static string GetMotherboardId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var serialNumber = obj["SerialNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "To be filled by O.E.M.")
                        return serialNumber;
                }
            }
            catch
            {
                // Ignorar errores
            }

            return string.Empty;
        }

        private static string GetDiskId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var serialNumber = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serialNumber))
                        return serialNumber;
                }
            }
            catch
            {
                // Ignorar errores
            }

            return string.Empty;
        }
    }
} 