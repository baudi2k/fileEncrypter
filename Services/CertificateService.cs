using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text;

namespace FileEncrypter.Services
{
    public class CertificateInfo
    {
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public string Thumbprint { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public bool HasPrivateKey { get; set; }
        public string FriendlyName { get; set; }
        public X509Certificate2 Certificate { get; set; }
    }

    public class CertificateService
    {
        /// <summary>
        /// Obtiene todos los certificados disponibles para encriptación del almacén de certificados
        /// </summary>
        /// <returns>Lista de certificados con capacidades de encriptación</returns>
        public List<CertificateInfo> GetAvailableCertificates()
        {
            var certificates = new List<CertificateInfo>();

            try
            {
                // Buscar en el almacén personal del usuario actual
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    
                    foreach (X509Certificate2 cert in store.Certificates)
                    {
                        // Verificar que el certificado sea válido y tenga capacidades de encriptación
                        if (IsCertificateValidForEncryption(cert))
                        {
                            certificates.Add(new CertificateInfo
                            {
                                Subject = cert.Subject,
                                Issuer = cert.Issuer,
                                Thumbprint = cert.Thumbprint,
                                ValidFrom = cert.NotBefore,
                                ValidTo = cert.NotAfter,
                                HasPrivateKey = cert.HasPrivateKey,
                                FriendlyName = !string.IsNullOrEmpty(cert.FriendlyName) ? cert.FriendlyName : GetCommonNameFromSubject(cert.Subject),
                                Certificate = cert
                            });
                        }
                    }
                }

                // Buscar también en el almacén de la máquina local
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);
                    
                    foreach (X509Certificate2 cert in store.Certificates)
                    {
                        if (IsCertificateValidForEncryption(cert) && !certificates.Any(c => c.Thumbprint == cert.Thumbprint))
                        {
                            certificates.Add(new CertificateInfo
                            {
                                Subject = cert.Subject,
                                Issuer = cert.Issuer,
                                Thumbprint = cert.Thumbprint,
                                ValidFrom = cert.NotBefore,
                                ValidTo = cert.NotAfter,
                                HasPrivateKey = cert.HasPrivateKey,
                                FriendlyName = !string.IsNullOrEmpty(cert.FriendlyName) ? cert.FriendlyName : GetCommonNameFromSubject(cert.Subject),
                                Certificate = cert
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener certificados: {ex.Message}", ex);
            }

            return certificates.OrderBy(c => c.FriendlyName).ToList();
        }

        /// <summary>
        /// Verifica si un certificado es válido para encriptación
        /// </summary>
        private bool IsCertificateValidForEncryption(X509Certificate2 certificate)
        {
            try
            {
                // Verificar que el certificado no haya expirado
                if (DateTime.Now < certificate.NotBefore || DateTime.Now > certificate.NotAfter)
                    return false;

                // Verificar que tenga clave pública RSA
                if (!(certificate.PublicKey.Key is RSA))
                    return false;

                // Verificar uso de claves (Key Usage)
                foreach (X509Extension extension in certificate.Extensions)
                {
                    if (extension is X509KeyUsageExtension keyUsage)
                    {
                        // Debe tener capacidad de encriptación de datos o intercambio de claves
                        if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DataEncipherment) ||
                            keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment))
                        {
                            return true;
                        }
                    }
                }

                // Si no tiene extensión de uso de claves, asumir que es válido
                return !certificate.Extensions.OfType<X509KeyUsageExtension>().Any();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extrae el nombre común (CN) del subject del certificado
        /// </summary>
        private string GetCommonNameFromSubject(string subject)
        {
            try
            {
                var parts = subject.Split(',');
                var cnPart = parts.FirstOrDefault(p => p.Trim().StartsWith("CN="));
                return cnPart?.Substring(cnPart.IndexOf('=') + 1).Trim() ?? "Certificado sin nombre";
            }
            catch
            {
                return "Certificado sin nombre";
            }
        }

        /// <summary>
        /// Encripta un archivo usando un certificado PKI con streaming para archivos grandes
        /// </summary>
        /// <param name="inputPath">Ruta del archivo a encriptar</param>
        /// <param name="outputPath">Ruta del archivo encriptado</param>
        /// <param name="certificate">Certificado para encriptación</param>
        /// <param name="progress">Reporte de progreso</param>
        /// <param name="cancellationToken">Token de cancelación</param>
        public async Task EncryptFileWithCertificateAsync(
            string inputPath, 
            string outputPath, 
            X509Certificate2 certificate, 
            IProgress<double> progress = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                using (var rsa = certificate.GetRSAPublicKey())
                {
                    if (rsa == null)
                        throw new Exception("No se pudo obtener la clave pública RSA del certificado");

                    // Generar clave y IV AES
                    using (var aes = Aes.Create())
                    {
                        aes.GenerateKey();
                        aes.GenerateIV();

                        // Encriptar clave AES con RSA
                        var encryptedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
                        var encryptedIV = rsa.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA256);

                        // Crear estructura de archivo PKI
                        var fileInfo = new FileInfo(inputPath);
                        var pkiFileHeader = new PKIFileHeader
                        {
                            OriginalFileName = Path.GetFileName(inputPath),
                            OriginalFileSize = fileInfo.Length,
                            CertificateThumbprint = certificate.Thumbprint,
                            EncryptedDate = DateTime.UtcNow,
                            EncryptedKey = encryptedKey,
                            EncryptedIV = encryptedIV
                        };

                        // Escribir archivo encriptado
                        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                        {
                            // Escribir header
                            await WriteFileHeaderAsync(outputStream, pkiFileHeader, cancellationToken);

                            // Encriptar y escribir contenido del archivo por bloques
                            using (var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
                            using (var encryptor = aes.CreateEncryptor())
                            using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write))
                            {
                                const int bufferSize = 81920; // 80KB buffer
                                var buffer = new byte[bufferSize];
                                long totalBytes = fileInfo.Length;
                                long processedBytes = 0;
                                
                                int bytesRead;
                                while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    await cryptoStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                    
                                    processedBytes += bytesRead;
                                    progress?.Report((double)processedBytes / totalBytes * 100);
                                }
                                
                                cryptoStream.FlushFinalBlock();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al encriptar archivo con certificado: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Desencripta un archivo usando un certificado PKI
        /// </summary>
        /// <param name="inputPath">Ruta del archivo encriptado</param>
        /// <param name="outputPath">Ruta del archivo desencriptado</param>
        /// <param name="certificate">Certificado para desencriptación</param>
        /// <param name="progress">Reporte de progreso</param>
        /// <param name="cancellationToken">Token de cancelación</param>
        public async Task<string> DecryptFileWithCertificateAsync(
            string inputPath, 
            string outputDirectory, 
            X509Certificate2 certificate, 
            IProgress<double> progress = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!certificate.HasPrivateKey)
                    throw new Exception("El certificado no tiene clave privada para desencriptar");

                using (var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
                {
                    // Leer y validar header
                    var header = await ReadFileHeaderAsync(inputStream, cancellationToken);
                    
                    // Verificar que sea el certificado correcto
                    if (header.CertificateThumbprint != certificate.Thumbprint)
                        throw new Exception("El certificado no corresponde con el usado para encriptar este archivo");

                    using (var rsa = certificate.GetRSAPrivateKey())
                    {
                        if (rsa == null)
                            throw new Exception("No se pudo obtener la clave privada RSA del certificado");

                        // Desencriptar clave AES
                        var aesKey = rsa.Decrypt(header.EncryptedKey, RSAEncryptionPadding.OaepSHA256);
                        var aesIV = rsa.Decrypt(header.EncryptedIV, RSAEncryptionPadding.OaepSHA256);

                        // Crear archivo de salida
                        var outputPath = Path.Combine(outputDirectory, header.OriginalFileName);
                        
                        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                        using (var aes = Aes.Create())
                        {
                            aes.Key = aesKey;
                            aes.IV = aesIV;
                            
                            using (var decryptor = aes.CreateDecryptor())
                            using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                            {
                                const int bufferSize = 81920; // 80KB buffer
                                var buffer = new byte[bufferSize];
                                long totalBytes = header.OriginalFileSize;
                                long processedBytes = 0;
                                
                                int bytesRead;
                                while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                    
                                    processedBytes += bytesRead;
                                    progress?.Report((double)processedBytes / totalBytes * 100);
                                }
                            }
                        }

                        return outputPath;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al desencriptar archivo con certificado: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Encripta datos usando un certificado PKI (método legacy para datos pequeños)
        /// </summary>
        /// <param name="data">Datos a encriptar</param>
        /// <param name="certificate">Certificado para encriptación</param>
        /// <returns>Datos encriptados en Base64</returns>
        public string EncryptWithCertificate(byte[] data, X509Certificate2 certificate)
        {
            try
            {
                using (var rsa = certificate.GetRSAPublicKey())
                {
                    if (rsa == null)
                        throw new Exception("No se pudo obtener la clave pública RSA del certificado");

                    // RSA tiene limitación de tamaño, así que usamos híbrido:
                    // 1. Generamos una clave AES aleatoria
                    // 2. Encriptamos los datos con AES
                    // 3. Encriptamos la clave AES con RSA
                    
                    using (var aes = Aes.Create())
                    {
                        aes.GenerateKey();
                        aes.GenerateIV();

                        // Encriptar datos con AES
                        byte[] encryptedData;
                        using (var encryptor = aes.CreateEncryptor())
                        using (var msEncrypt = new MemoryStream())
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(data, 0, data.Length);
                            csEncrypt.FlushFinalBlock();
                            encryptedData = msEncrypt.ToArray();
                        }

                        // Encriptar clave AES con RSA
                        var encryptedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
                        var encryptedIV = rsa.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA256);

                        // Combinar todo
                        var result = new CertificateEncryptedData
                        {
                            EncryptedKey = encryptedKey,
                            EncryptedIV = encryptedIV,
                            EncryptedData = encryptedData,
                            CertificateThumbprint = certificate.Thumbprint
                        };

                        return Convert.ToBase64String(SerializeEncryptedData(result));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al encriptar con certificado: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Desencripta datos usando un certificado PKI
        /// </summary>
        /// <param name="encryptedDataBase64">Datos encriptados en Base64</param>
        /// <param name="certificate">Certificado para desencriptación (debe tener clave privada)</param>
        /// <returns>Datos desencriptados</returns>
        public byte[] DecryptWithCertificate(string encryptedDataBase64, X509Certificate2 certificate)
        {
            try
            {
                if (!certificate.HasPrivateKey)
                    throw new Exception("El certificado no tiene clave privada para desencriptar");

                var serializedData = Convert.FromBase64String(encryptedDataBase64);
                var encryptedDataObj = DeserializeEncryptedData(serializedData);

                // Verificar que sea el certificado correcto
                if (encryptedDataObj.CertificateThumbprint != certificate.Thumbprint)
                    throw new Exception("El certificado no corresponde con el usado para encriptar");

                using (var rsa = certificate.GetRSAPrivateKey())
                {
                    if (rsa == null)
                        throw new Exception("No se pudo obtener la clave privada RSA del certificado");

                    // Desencriptar clave AES
                    var aesKey = rsa.Decrypt(encryptedDataObj.EncryptedKey, RSAEncryptionPadding.OaepSHA256);
                    var aesIV = rsa.Decrypt(encryptedDataObj.EncryptedIV, RSAEncryptionPadding.OaepSHA256);

                    // Desencriptar datos con AES
                    using (var aes = Aes.Create())
                    {
                        aes.Key = aesKey;
                        aes.IV = aesIV;

                        using (var decryptor = aes.CreateDecryptor())
                        using (var msDecrypt = new MemoryStream(encryptedDataObj.EncryptedData))
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        using (var msPlain = new MemoryStream())
                        {
                            csDecrypt.CopyTo(msPlain);
                            return msPlain.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al desencriptar con certificado: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Busca un certificado por su thumbprint
        /// </summary>
        public X509Certificate2 FindCertificateByThumbprint(string thumbprint)
        {
            var stores = new[]
            {
                new X509Store(StoreName.My, StoreLocation.CurrentUser),
                new X509Store(StoreName.My, StoreLocation.LocalMachine)
            };

            foreach (var store in stores)
            {
                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                    if (found.Count > 0)
                    {
                        return found[0];
                    }
                }
                finally
                {
                    store.Close();
                }
            }

            return null;
        }

        #region Serialización de datos encriptados

        private class CertificateEncryptedData
        {
            public byte[] EncryptedKey { get; set; }
            public byte[] EncryptedIV { get; set; }
            public byte[] EncryptedData { get; set; }
            public string CertificateThumbprint { get; set; }
        }

        private class PKIFileHeader
        {
            public string OriginalFileName { get; set; }
            public long OriginalFileSize { get; set; }
            public string CertificateThumbprint { get; set; }
            public DateTime EncryptedDate { get; set; }
            public byte[] EncryptedKey { get; set; }
            public byte[] EncryptedIV { get; set; }
        }

        private byte[] SerializeEncryptedData(CertificateEncryptedData data)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                writer.Write(data.CertificateThumbprint ?? "");
                writer.Write(data.EncryptedKey.Length);
                writer.Write(data.EncryptedKey);
                writer.Write(data.EncryptedIV.Length);
                writer.Write(data.EncryptedIV);
                writer.Write(data.EncryptedData.Length);
                writer.Write(data.EncryptedData);
                return ms.ToArray();
            }
        }

        private CertificateEncryptedData DeserializeEncryptedData(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms, Encoding.UTF8))
            {
                var thumbprint = reader.ReadString();
                
                var keyLength = reader.ReadInt32();
                var encryptedKey = reader.ReadBytes(keyLength);
                
                var ivLength = reader.ReadInt32();
                var encryptedIV = reader.ReadBytes(ivLength);
                
                var dataLength = reader.ReadInt32();
                var encryptedData = reader.ReadBytes(dataLength);

                return new CertificateEncryptedData
                {
                    CertificateThumbprint = thumbprint,
                    EncryptedKey = encryptedKey,
                    EncryptedIV = encryptedIV,
                    EncryptedData = encryptedData
                };
            }
        }

        #endregion

        #region Generación de Certificados

        /// <summary>
        /// Genera un nuevo certificado auto-firmado
        /// </summary>
        /// <param name="subjectName">Nombre del sujeto (ej: "CN=Mi Certificado")</param>
        /// <param name="keySize">Tamaño de la clave RSA en bits (2048, 3072, 4096)</param>
        /// <param name="validityYears">Años de validez del certificado</param>
        /// <param name="installInStore">Si debe instalarse automáticamente en el almacén de certificados</param>
        /// <returns>El certificado generado</returns>
        public X509Certificate2 GenerateSelfSignedCertificate(
            string subjectName, 
            int keySize = 2048, 
            int validityYears = 5, 
            bool installInStore = true)
        {
            try
            {
                // Validar parámetros
                if (string.IsNullOrWhiteSpace(subjectName))
                    throw new ArgumentException("El nombre del sujeto no puede estar vacío", nameof(subjectName));

                if (keySize != 2048 && keySize != 3072 && keySize != 4096)
                    throw new ArgumentException("El tamaño de clave debe ser 2048, 3072 o 4096 bits", nameof(keySize));

                if (validityYears <= 0 || validityYears > 30)
                    throw new ArgumentException("La validez debe estar entre 1 y 30 años", nameof(validityYears));

                // Crear solicitud de certificado
                using (var rsa = RSA.Create(keySize))
                {
                    var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                    // Agregar extensiones básicas
                    request.CertificateExtensions.Add(
                        new X509BasicConstraintsExtension(false, false, 0, false));

                    request.CertificateExtensions.Add(
                        new X509KeyUsageExtension(
                            X509KeyUsageFlags.DataEncipherment | 
                            X509KeyUsageFlags.KeyEncipherment | 
                            X509KeyUsageFlags.DigitalSignature, 
                            false));

                    request.CertificateExtensions.Add(
                        new X509EnhancedKeyUsageExtension(
                            new OidCollection { 
                                new Oid("1.3.6.1.5.5.7.3.1"), // Server Authentication
                                new Oid("1.3.6.1.5.5.7.3.2"), // Client Authentication
                                new Oid("1.3.6.1.4.1.311.10.3.4") // Encrypting File System
                            }, 
                            false));

                    // Generar el certificado auto-firmado
                    var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
                    var notAfter = notBefore.AddYears(validityYears);
                    
                    // Crear certificado con clave privada exportable y persistente
                    var certificate = request.CreateSelfSigned(notBefore, notAfter);
                    
                    // Asegurar que el certificado tenga clave privada exportable
                    var certificateWithPrivateKey = new X509Certificate2(certificate.Export(X509ContentType.Pfx), 
                        "", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                    
                    // Establecer nombre amigable
                    certificateWithPrivateKey.FriendlyName = $"Certificado de Encriptación - {DateTime.Now:yyyy-MM-dd}";

                    // Instalar en el almacén si se solicita
                    if (installInStore)
                    {
                        InstallCertificate(certificateWithPrivateKey);
                    }

                    return certificateWithPrivateKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar certificado: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Instala un certificado en el almacén personal del usuario actual
        /// </summary>
        /// <param name="certificate">Certificado a instalar</param>
        public void InstallCertificate(X509Certificate2 certificate)
        {
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    
                    // Verificar si el certificado ya existe para evitar duplicados
                    var existing = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                    if (existing.Count > 0)
                    {
                        // Remover el existente primero
                        store.Remove(existing[0]);
                    }
                    
                    // Asegurar que el certificado tenga las flags correctas para persistir la clave privada
                    if (certificate.HasPrivateKey)
                    {
                        var pfxData = certificate.Export(X509ContentType.Pfx);
                        var certWithFlags = new X509Certificate2(pfxData, "", 
                            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
                        certWithFlags.FriendlyName = certificate.FriendlyName;
                        
                        store.Add(certWithFlags);
                    }
                    else
                    {
                        store.Add(certificate);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al instalar certificado en el almacén: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Exporta un certificado a un archivo PFX protegido con contraseña
        /// </summary>
        /// <param name="certificate">Certificado a exportar</param>
        /// <param name="filePath">Ruta donde guardar el archivo</param>
        /// <param name="password">Contraseña para proteger el archivo</param>
        public void ExportCertificateToPfx(X509Certificate2 certificate, string filePath, string password)
        {
            try
            {
                if (!certificate.HasPrivateKey)
                    throw new InvalidOperationException("El certificado debe tener clave privada para exportar a PFX");

                var pfxData = certificate.Export(X509ContentType.Pfx, password);
                File.WriteAllBytes(filePath, pfxData);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al exportar certificado: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Importa un certificado desde un archivo PFX
        /// </summary>
        /// <param name="filePath">Ruta del archivo PFX</param>
        /// <param name="password">Contraseña del archivo</param>
        /// <param name="installInStore">Si debe instalarse en el almacén</param>
        /// <returns>El certificado importado</returns>
        public X509Certificate2 ImportCertificateFromPfx(string filePath, string password, bool installInStore = false)
        {
            try
            {
                var certificate = new X509Certificate2(filePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                
                if (installInStore)
                {
                    InstallCertificate(certificate);
                }

                return certificate;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al importar certificado: {ex.Message}", ex);
            }
        }

        private async Task WriteFileHeaderAsync(Stream outputStream, PKIFileHeader header, CancellationToken cancellationToken)
        {
            // Escribir signature
            var signature = Encoding.UTF8.GetBytes("FILEENC_PKI_V2");
            await outputStream.WriteAsync(signature, 0, signature.Length, cancellationToken);

            // Serializar header usando BinaryWriter para mejor control
            using (var headerStream = new MemoryStream())
            using (var writer = new BinaryWriter(headerStream, Encoding.UTF8))
            {
                writer.Write(header.OriginalFileName ?? "");
                writer.Write(header.OriginalFileSize);
                writer.Write(header.CertificateThumbprint ?? "");
                writer.Write(header.EncryptedDate.ToBinary());
                writer.Write(header.EncryptedKey.Length);
                writer.Write(header.EncryptedKey);
                writer.Write(header.EncryptedIV.Length);
                writer.Write(header.EncryptedIV);

                var headerData = headerStream.ToArray();
                var headerLength = BitConverter.GetBytes(headerData.Length);
                await outputStream.WriteAsync(headerLength, 0, 4, cancellationToken);
                await outputStream.WriteAsync(headerData, 0, headerData.Length, cancellationToken);
            }
        }

        private async Task<PKIFileHeader> ReadFileHeaderAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            // Leer y verificar signature
            var signatureBuffer = new byte[14];
            await inputStream.ReadAsync(signatureBuffer, 0, 14, cancellationToken);
            var signature = Encoding.UTF8.GetString(signatureBuffer);
            
            if (signature != "FILEENC_PKI_V2")
                throw new InvalidDataException("El archivo no es un archivo PKI válido o es de una versión anterior");

            // Leer longitud del header
            var headerLengthBuffer = new byte[4];
            await inputStream.ReadAsync(headerLengthBuffer, 0, 4, cancellationToken);
            var headerLength = BitConverter.ToInt32(headerLengthBuffer, 0);

            // Leer header
            var headerData = new byte[headerLength];
            await inputStream.ReadAsync(headerData, 0, headerLength, cancellationToken);

            // Deserializar header
            using (var headerStream = new MemoryStream(headerData))
            using (var reader = new BinaryReader(headerStream, Encoding.UTF8))
            {
                return new PKIFileHeader
                {
                    OriginalFileName = reader.ReadString(),
                    OriginalFileSize = reader.ReadInt64(),
                    CertificateThumbprint = reader.ReadString(),
                    EncryptedDate = DateTime.FromBinary(reader.ReadInt64()),
                    EncryptedKey = reader.ReadBytes(reader.ReadInt32()),
                    EncryptedIV = reader.ReadBytes(reader.ReadInt32())
                };
            }
        }

        #endregion
    }
}