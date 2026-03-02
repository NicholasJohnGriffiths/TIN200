namespace TINWorkspaceTemp.Services
{
    public interface ISurveyLinkTokenService
    {
        string GenerateToken(int clientId);
        bool IsTokenValid(int clientId, string token);
    }
}
