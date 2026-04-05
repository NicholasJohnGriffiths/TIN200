namespace TINWeb.Services
{
    public interface ISurveyLinkTokenService
    {
        string GenerateToken(int clientId, DateTimeOffset? expiresAtUtc = null);
        bool IsTokenValid(int clientId, string token);
        DateTimeOffset? GetTokenExpiryUtc(string token);
    }
}
