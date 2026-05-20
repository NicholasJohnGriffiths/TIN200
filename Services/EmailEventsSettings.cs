namespace TINWeb.Services
{
    public class EmailEventsSettings
    {
        public bool Enabled { get; set; }

        public string AzPath { get; set; } = "az";

        public string? CommandTemplate { get; set; }

        public int CommandTimeoutSeconds { get; set; } = 30;
    }
}