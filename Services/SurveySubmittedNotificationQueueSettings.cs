namespace TINWeb.Services
{
    public class SurveySubmittedNotificationQueueSettings
    {
        public bool Enabled { get; set; }

        public string ConnectionString { get; set; } = string.Empty;

        public string QueueName { get; set; } = "survey-submitted-admin-notifications";
    }
}