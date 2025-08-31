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
        /// Encripta datos usando un certificado PKI
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
    }
}