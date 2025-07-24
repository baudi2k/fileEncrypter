using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            byte[] salt = GenerateSalt();
            using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await fsOut.WriteAsync(salt, 0, salt.Length, cancellationToken);

            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.GenerateIV();
            await fsOut.WriteAsync(aes.IV, 0, aes.IV.Length, cancellationToken);

            using var crypto = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using var writer = new BinaryWriter(crypto, Encoding.UTF8, leaveOpen: true);

            writer.Write(Path.GetFileName(inputPath));
            long totalBytes = new FileInfo(inputPath).Length;
            writer.Write(totalBytes);

            byte[] buffer = new byte[BufferSize];
            long readSoFar = 0;
            using var fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            int bytesRead;
            while ((bytesRead = await fsIn.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await crypto.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                readSoFar += bytesRead;
                progress.Report(readSoFar / (double)totalBytes * 100);
            }
            await crypto.FlushAsync(cancellationToken);
        }

        public static async Task<string> DecryptFileAsync(
            string inputPath,
            string password,
            string outputDirectory,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            using var fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            byte[] salt = new byte[SaltSize];
            await fsIn.ReadAsync(salt, 0, salt.Length, cancellationToken);

            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            byte[] iv = new byte[aes.BlockSize / 8];
            await fsIn.ReadAsync(iv, 0, iv.Length, cancellationToken);
            aes.IV = iv;

            using var crypto = new CryptoStream(fsIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new BinaryReader(crypto, Encoding.UTF8, leaveOpen: true);

            string originalName = reader.ReadString();
            long totalBytes = reader.ReadInt64();

            string outputPath = Path.Combine(outputDirectory, originalName);
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
    }
}
