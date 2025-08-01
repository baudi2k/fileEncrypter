using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileEncrypter.Helpers;

namespace FileEncrypter.Services
{
    public static class EncryptionService
    {
        private const int KeySize = 256;
        private const int SaltSize = 32;
        private const int BufferSize = 81920;

        public static async Task EncryptFileAsync(
            string inputPath,
            string outputPath,
            string password,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var result = await EncryptFileWithRecoveryAsync(inputPath, outputPath, password, progress, cancellationToken);
            // El método original no devuelve la frase de recuperación para mantener compatibilidad
        }

        public static async Task<EncryptionResult> EncryptFileWithRecoveryAsync(
            string inputPath,
            string outputPath,
            string password,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            // Generar frase de recuperación
            var recoveryResult = RecoveryPhraseHelper.GenerateRecoveryPhrase();
            if (!recoveryResult.Success)
            {
                throw new Exception($"Error generando frase de recuperación: {recoveryResult.ErrorMessage}");
            }

            byte[] salt = GenerateSalt();
            using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            
            // Escribir signature para identificar archivos con frase de recuperación
            var signature = Encoding.UTF8.GetBytes("FENC_v2");
            await fsOut.WriteAsync(signature, 0, signature.Length, cancellationToken);
            
            await fsOut.WriteAsync(salt, 0, salt.Length, cancellationToken);

            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.GenerateIV();
            await fsOut.WriteAsync(aes.IV, 0, aes.IV.Length, cancellationToken);

            // Encriptar la contraseña con una clave derivada de la frase de recuperación
            var recoveryKey = RecoveryPhraseHelper.DeriveKeyFromPhraseAsBase64(recoveryResult.Phrase);
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var encryptedPassword = EncryptData(passwordBytes, recoveryKey, salt);
            var passwordLength = BitConverter.GetBytes(encryptedPassword.Length);
            await fsOut.WriteAsync(passwordLength, 0, 4, cancellationToken);
            await fsOut.WriteAsync(encryptedPassword, 0, encryptedPassword.Length, cancellationToken);

            using var crypto = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using var writer = new BinaryWriter(crypto, Encoding.UTF8, leaveOpen: true);

            writer.Write(Path.GetFileName(inputPath));
            long totalBytes = new FileInfo(inputPath).Length;
            writer.Write(totalBytes);

            // Use fastest compression level to speed up encryption
            using var compressor = new BrotliStream(crypto, CompressionLevel.Fastest, leaveOpen: true);

            byte[] buffer = new byte[BufferSize];
            long readSoFar = 0;
            using var fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            int bytesRead;
            while ((bytesRead = await fsIn.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await compressor.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                readSoFar += bytesRead;
                progress.Report(readSoFar / (double)totalBytes * 100);
            }
            await compressor.FlushAsync(cancellationToken);
            await compressor.DisposeAsync();
            await crypto.FlushAsync(cancellationToken);

            return new EncryptionResult
            {
                Success = true,
                RecoveryPhrase = recoveryResult.Phrase,
                OutputPath = outputPath
            };
        }

        public static async Task<string> DecryptFileAsync(
            string inputPath,
            string password,
            string outputDirectory,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            return await DecryptFileWithPasswordOrRecoveryAsync(inputPath, password, null, outputDirectory, progress, cancellationToken);
        }

        public static async Task<string> DecryptFileWithRecoveryAsync(
            string inputPath,
            string recoveryPhrase,
            string outputDirectory,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            return await DecryptFileWithPasswordOrRecoveryAsync(inputPath, null, recoveryPhrase, outputDirectory, progress, cancellationToken);
        }

        public static async Task<string> DecryptFileWithPasswordOrRecoveryAsync(
            string inputPath,
            string? password,
            string? recoveryPhrase,
            string outputDirectory,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            string resultPath = string.Empty;
            using var fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            
            // Verificar si es un archivo con frase de recuperación
            var signatureBuffer = new byte[7];
            await fsIn.ReadAsync(signatureBuffer, 0, 7, cancellationToken);
            var signature = Encoding.UTF8.GetString(signatureBuffer);
            
            bool hasRecoveryPhrase = signature == "FENC_v2";
            if (!hasRecoveryPhrase)
            {
                // Archivo formato antiguo, volver al inicio
                fsIn.Seek(0, SeekOrigin.Begin);
                if (!string.IsNullOrEmpty(recoveryPhrase))
                {
                    throw new CryptographicException("Este archivo no tiene frase de recuperación. Use la contraseña original.");
                }
                resultPath = await DecryptLegacyFile(fsIn, password!, outputDirectory, progress, cancellationToken);
            }
            else
            {
                // Archivo con frase de recuperación
            byte[] salt = new byte[SaltSize];
            await fsIn.ReadAsync(salt, 0, salt.Length, cancellationToken);

            // Leer IV
            byte[] iv = new byte[16]; 
            await fsIn.ReadAsync(iv, 0, iv.Length, cancellationToken);

            // Leer contraseña encriptada con frase de recuperación
            var passwordLengthBytes = new byte[4];
            await fsIn.ReadAsync(passwordLengthBytes, 0, 4, cancellationToken);
            var passwordLength = BitConverter.ToInt32(passwordLengthBytes, 0);
            var encryptedPassword = new byte[passwordLength];
            await fsIn.ReadAsync(encryptedPassword, 0, passwordLength, cancellationToken);

            string actualPassword;
            if (!string.IsNullOrEmpty(password))
            {
                // Usar la contraseña directamente
                actualPassword = password;
            }
            else if (!string.IsNullOrEmpty(recoveryPhrase))
            {
                if (!RecoveryPhraseHelper.ValidateRecoveryPhrase(recoveryPhrase))
                {
                    throw new CryptographicException("Frase de recuperación inválida.");
                }
                
                // Desencriptar la contraseña usando la frase de recuperación
                try
                {
                    var recoveryKey = RecoveryPhraseHelper.DeriveKeyFromPhraseAsBase64(recoveryPhrase);
                    var passwordBytes = DecryptData(encryptedPassword, recoveryKey, salt);
                    actualPassword = Encoding.UTF8.GetString(passwordBytes);
                }
                catch (Exception)
                {
                    throw new CryptographicException("Frase de recuperación incorrecta.");
                }
            }
            else
            {
                throw new ArgumentException("Debe proporcionar una contraseña o frase de recuperación.");
            }

            using var aes = Aes.Create();
            aes.Key = DeriveKey(actualPassword, salt);
            aes.IV = iv;

            using var crypto = new CryptoStream(fsIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new BinaryReader(crypto, Encoding.UTF8, leaveOpen: true);

            string originalName;
            long totalBytes;
            string outputPath;

            try
            {
                originalName = reader.ReadString();
                totalBytes = reader.ReadInt64();

                // Validar que el nombre del archivo no contenga caracteres inválidos
                if (string.IsNullOrEmpty(originalName) || 
                    originalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    originalName.Contains('\0'))
                {
                    throw new CryptographicException("Contraseña o frase de recuperación incorrecta.");
                }

                outputPath = Path.Combine(outputDirectory, originalName);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("path"))
            {
                throw new CryptographicException("Contraseña o frase de recuperación incorrecta.", ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new CryptographicException("Contraseña o frase de recuperación incorrecta.", ex);
            }

            byte[] buffer = new byte[BufferSize];
            long writtenSoFar = 0;

            using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var decompressor = new BrotliStream(reader.BaseStream, CompressionMode.Decompress, leaveOpen: true);
            int bytesRead;
            while ((bytesRead = await decompressor.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fsOut.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                writtenSoFar += bytesRead;
                progress.Report(writtenSoFar / (double)totalBytes * 100);
            }

            resultPath = outputPath;
        }

            fsIn.Dispose();
            File.Delete(inputPath);
            return resultPath;
        }

        private static async Task<string> DecryptLegacyFile(
            FileStream fsIn,
            string password,
            string outputDirectory,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            byte[] salt = new byte[SaltSize];
            await fsIn.ReadAsync(salt, 0, salt.Length, cancellationToken);

            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            byte[] iv = new byte[aes.BlockSize / 8];
            await fsIn.ReadAsync(iv, 0, iv.Length, cancellationToken);
            aes.IV = iv;

            using var crypto = new CryptoStream(fsIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new BinaryReader(crypto, Encoding.UTF8, leaveOpen: true);

            string originalName;
            long totalBytes;
            string outputPath;

            try
            {
                originalName = reader.ReadString();
                totalBytes = reader.ReadInt64();

                if (string.IsNullOrEmpty(originalName) || 
                    originalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    originalName.Contains('\0'))
                {
                    throw new CryptographicException("Contraseña incorrecta o archivo corrupto.");
                }

                outputPath = Path.Combine(outputDirectory, originalName);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new CryptographicException("Contraseña incorrecta o archivo corrupto.", ex);
            }

            byte[] buffer = new byte[BufferSize];
            long writtenSoFar = 0;

            using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            int bytesRead;
            while ((bytesRead = await reader.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fsOut.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                writtenSoFar += bytesRead;
                progress.Report(writtenSoFar / (double)totalBytes * 100);
            }

            return outputPath;
        }

        private static byte[] GenerateSalt()
        {
            var salt = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        private static byte[] DeriveKey(string password, byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            return kdf.GetBytes(KeySize / 8);
        }

        private static byte[] EncryptData(byte[] data, string password, byte[] salt)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.GenerateIV();

            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);

            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();

            return ms.ToArray();
        }

        private static byte[] DecryptData(byte[] encryptedData, string password, byte[] salt)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);

            using var ms = new MemoryStream(encryptedData);
            var iv = new byte[aes.BlockSize / 8];
            ms.Read(iv, 0, iv.Length);
            aes.IV = iv;

            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var result = new MemoryStream();
            cs.CopyTo(result);

            return result.ToArray();
        }
    }

    public class EncryptionResult
    {
        public bool Success { get; set; }
        public string RecoveryPhrase { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }
}
