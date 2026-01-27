namespace TGharker.Identity.Web.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string hashedPassword, string providedPassword);
}

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}
