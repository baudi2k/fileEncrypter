using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FileEncrypter.Helpers
{
    public static class PasswordHelper
    {
        private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        private const string NumberChars = "0123456789";
        private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        public static string GeneratePassword(int length, bool includeUppercase, bool includeLowercase, 
            bool includeNumbers, bool includeSpecial)
        {
            if (length < 4 || length > 32)
                throw new ArgumentException("La longitud debe estar entre 4 y 32 caracteres");

            var characterSet = new StringBuilder();
            var requiredChars = new StringBuilder();

            if (includeUppercase)
            {
                characterSet.Append(UppercaseChars);
                requiredChars.Append(GetRandomChar(UppercaseChars));
            }
            if (includeLowercase)
            {
                characterSet.Append(LowercaseChars);
                requiredChars.Append(GetRandomChar(LowercaseChars));
            }
            if (includeNumbers)
            {
                characterSet.Append(NumberChars);
                requiredChars.Append(GetRandomChar(NumberChars));
            }
            if (includeSpecial)
            {
                characterSet.Append(SpecialChars);
                requiredChars.Append(GetRandomChar(SpecialChars));
            }

            if (characterSet.Length == 0)
                throw new ArgumentException("Debe seleccionar al menos un tipo de caracter");

            var password = new StringBuilder(requiredChars.ToString());
            var remainingLength = length - requiredChars.Length;

            for (int i = 0; i < remainingLength; i++)
            {
                password.Append(GetRandomChar(characterSet.ToString()));
            }

            // Mezclar la contraseña
            return ShuffleString(password.ToString());
        }

        private static char GetRandomChar(string chars)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var randomIndex = Math.Abs(BitConverter.ToInt32(bytes, 0)) % chars.Length;
                return chars[randomIndex];
            }
        }

        private static string ShuffleString(string input)
        {
            var array = input.ToCharArray();
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = array.Length - 1; i > 0; i--)
                {
                    var bytes = new byte[4];
                    rng.GetBytes(bytes);
                    var j = Math.Abs(BitConverter.ToInt32(bytes, 0)) % (i + 1);
                    (array[i], array[j]) = (array[j], array[i]);
                }
            }
            return new string(array);
        }

        public static PasswordStrength EvaluatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return new PasswordStrength { Score = 0, Level = "Muy Débil", Color = "#FFEF4444" };

            int score = 0;
            var feedback = new StringBuilder();

            // Longitud
            if (password.Length >= 8) score += 20;
            else if (password.Length >= 6) score += 10;
            else feedback.AppendLine("• Use al menos 8 caracteres");

            if (password.Length >= 12) score += 10;
            if (password.Length >= 16) score += 10;

            // Mayúsculas
            if (Regex.IsMatch(password, @"[A-Z]")) score += 15;
            else feedback.AppendLine("• Incluya letras mayúsculas");

            // Minúsculas
            if (Regex.IsMatch(password, @"[a-z]")) score += 15;
            else feedback.AppendLine("• Incluya letras minúsculas");

            // Números
            if (Regex.IsMatch(password, @"[0-9]")) score += 15;
            else feedback.AppendLine("• Incluya números");

            // Caracteres especiales
            if (Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{}|;:,.<>?]")) score += 15;
            else feedback.AppendLine("• Incluya caracteres especiales");

            // Variedad de caracteres
            var uniqueChars = password.Distinct().Count();
            if (uniqueChars >= password.Length * 0.7) score += 10;

            // Penalizar patrones comunes
            if (Regex.IsMatch(password, @"123|abc|qwe|password|admin", RegexOptions.IgnoreCase))
                score -= 20;

            // Determinar nivel y color
            string level;
            string color;
            if (score >= 80)
            {
                level = "Muy Fuerte";
                color = "#FF10B981"; // Verde
            }
            else if (score >= 60)
            {
                level = "Fuerte";
                color = "#FF10B981"; // Verde
            }
            else if (score >= 40)
            {
                level = "Media";
                color = "#FFF59E0B"; // Amarillo
            }
            else if (score >= 20)
            {
                level = "Débil";
                color = "#FFEF4444"; // Rojo
            }
            else
            {
                level = "Muy Débil";
                color = "#FFEF4444"; // Rojo
            }

            return new PasswordStrength
            {
                Score = Math.Max(0, Math.Min(100, score)),
                Level = level,
                Color = color,
                Feedback = feedback.ToString().Trim()
            };
        }
    }

    public class PasswordStrength
    {
        public int Score { get; set; }
        public string Level { get; set; } = "";
        public string Color { get; set; } = "";
        public string Feedback { get; set; } = "";
    }
} 