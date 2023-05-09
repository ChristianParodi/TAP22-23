using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace Parodi {
    public static class Utils {
        /// <summary>
        /// This functions computes an Hash by using
        /// pbkdf2 algorithm and SHA512 for hashing
        /// </summary>
        /// <param name="password">the plaintext to hash</param>
        /// <returns>the hashed password</returns>
        public static string HashPassword(string password) {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 350_000, HashAlgorithmName.SHA512, 16);
            var hexSalt = Convert.ToHexString(salt);
            var hexHash = Convert.ToHexString(hash);
            return $"{hexHash}{hexSalt}";
        }

        /// <summary>
        /// Checks if the given hash and the given passwords matches
        /// </summary>
        /// <param name="hashPass"></param>
        /// <param name="password"></param>
        /// <returns>true if the passwords are the same, false otherwise</returns>
        public static bool VerifyHashPassword(string hashPass, string password) {
            var hashBytes = Convert.FromHexString(hashPass);
            var hash = hashBytes[..16]; // Firsts 16 bytes
            var salt = hashBytes[16..]; // Last 16 bytes
            var newHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 350_000,
                HashAlgorithmName.SHA512, 16);
            return newHash.SequenceEqual(hash);
        }
    }
}

