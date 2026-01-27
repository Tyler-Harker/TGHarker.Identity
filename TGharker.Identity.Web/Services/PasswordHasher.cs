using System.Security.Cryptography;

namespace TGharker.Identity.Web.Services;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            HashSize);

        // Format: {iterations}.{salt}.{hash}
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public PasswordVerificationResult Verify(string hashedPassword, string providedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 3)
            return PasswordVerificationResult.Failed;

        if (!int.TryParse(parts[0], out var iterations))
            return PasswordVerificationResult.Failed;

        byte[] salt;
        byte[] storedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            storedHash = Convert.FromBase64String(parts[2]);
        }
        catch
        {
            return PasswordVerificationResult.Failed;
        }

        var computedHash = Rfc2898DeriveBytes.Pbkdf2(
            providedPassword,
            salt,
            iterations,
            Algorithm,
            HashSize);

        if (!CryptographicOperations.FixedTimeEquals(storedHash, computedHash))
            return PasswordVerificationResult.Failed;

        // Check if rehash is needed (iterations increased)
        if (iterations < Iterations)
            return PasswordVerificationResult.SuccessRehashNeeded;

        return PasswordVerificationResult.Success;
    }
}
