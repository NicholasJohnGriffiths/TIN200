namespace TINWeb.Services
{
    public class StripeSettings
    {
        public bool UseTestMode { get; set; }
        public string PublishableKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string WebhookSecretTest { get; set; } = string.Empty;
    }
}