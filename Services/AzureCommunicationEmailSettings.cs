namespace TINWeb.Services
{
    public class AzureCommunicationEmailSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "TIN200 Survey";
    }
}