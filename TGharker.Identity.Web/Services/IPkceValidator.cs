namespace TGharker.Identity.Web.Services;

public interface IPkceValidator
{
    bool Validate(string codeVerifier, string codeChallenge, string? codeChallengeMethod);
    string GenerateCodeChallenge(string codeVerifier, string method = "S256");
}
