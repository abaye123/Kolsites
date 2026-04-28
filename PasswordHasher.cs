using System;
using System.Security.Cryptography;

namespace Kolsites
{
    /// <summary>
    /// Hashing של סיסמת הגדרות הקיוסק באמצעות PBKDF2-HMAC-SHA256 + מלח אקראי.
    /// פורמט מאוחסן: "iterations$saltBase64$hashBase64" - מאפשר שינוי iterations בעתיד תוך שמירה על תאימות.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(HashSize);
            return $"{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(password)) return false;

            var parts = stored.Split('$');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out int iterations) || iterations <= 0) return false;

            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
                var actual = pbkdf2.GetBytes(expected.Length);
                // השוואה בזמן קבוע למניעת timing attacks
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>בודק אם המחרוזת המאוחסנת היא ב-format של hash תקני (ולא טקסט גלוי).</summary>
        public static bool IsHashed(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            var parts = stored.Split('$');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out _)) return false;
            try
            {
                _ = Convert.FromBase64String(parts[1]);
                _ = Convert.FromBase64String(parts[2]);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
