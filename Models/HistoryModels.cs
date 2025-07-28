using System;
using System.Collections.Generic;

namespace FileEncrypter.Models
{
    public class EncryptionHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OriginalFileName { get; set; } = string.Empty;
        public string OriginalFilePath { get; set; } = string.Empty;
        public string EncryptedFilePath { get; set; } = string.Empty;
        public DateTime EncryptionDate { get; set; }
        public long OriginalFileSize { get; set; }
        public string MachineIdentifier { get; set; } = string.Empty;
        public bool IsDecrypted { get; set; } = false;
        public DateTime? DecryptionDate { get; set; }
        public string? DecryptedFilePath { get; set; }
    }

    public class EncryptionHistory
    {
        public string MachineIdentifier { get; set; } = string.Empty;
        public List<EncryptionHistoryEntry> Entries { get; set; } = new List<EncryptionHistoryEntry>();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class HistoryOperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public EncryptionHistoryEntry? Entry { get; set; }

        public static HistoryOperationResult CreateSuccess(EncryptionHistoryEntry? entry = null)
        {
            return new HistoryOperationResult { Success = true, Entry = entry };
        }

        public static HistoryOperationResult CreateError(string errorMessage)
        {
            return new HistoryOperationResult { Success = false, ErrorMessage = errorMessage };
        }
    }
} 