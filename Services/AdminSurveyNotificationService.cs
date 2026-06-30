using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;

namespace TINWeb.Services
{
    public class AdminSurveyNotificationService
    {
        private readonly ISurveyEmailService _surveyEmailService;
        private readonly SurveySubmittedNotificationQueueSettings _queueSettings;
        private readonly ILogger<AdminSurveyNotificationService> _logger;

        public AdminSurveyNotificationService(
            ISurveyEmailService surveyEmailService,
            IOptions<SurveySubmittedNotificationQueueSettings> queueOptions,
            ILogger<AdminSurveyNotificationService> logger)
        {
            _surveyEmailService = surveyEmailService;
            _queueSettings = queueOptions.Value;
            _logger = logger;
        }

        public async Task NotifySurveySubmittedAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime submittedAt,
            string? submitterEmail)
        {
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return;
            }

            if (await TryQueueNotificationAsync(adminEmail, companyName, surveyYear, submittedAt, submitterEmail))
            {
                return;
            }

            await _surveyEmailService.SendSurveySubmittedNotificationAsync(
                adminEmail,
                companyName,
                surveyYear,
                submittedAt,
                submitterEmail);
        }

        public async Task NotifySurveySavedAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime savedAt,
            string? submitterEmail)
        {
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return;
            }

            await _surveyEmailService.SendSurveySavedNotificationAsync(
                adminEmail,
                companyName,
                surveyYear,
                savedAt,
                submitterEmail);
        }

        private async Task<bool> TryQueueNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime submittedAt,
            string? submitterEmail)
        {
            if (!_queueSettings.Enabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_queueSettings.ConnectionString)
                || string.IsNullOrWhiteSpace(_queueSettings.QueueName))
            {
                _logger.LogWarning("Survey submitted notification queue is enabled but not fully configured. Falling back to direct send.");
                return false;
            }

            try
            {
                var queueClient = new QueueClient(
                    _queueSettings.ConnectionString,
                    _queueSettings.QueueName,
                    new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

                await queueClient.CreateIfNotExistsAsync();

                var message = new SurveySubmittedNotificationMessage
                {
                    AdminEmail = adminEmail.Trim(),
                    CompanyName = string.IsNullOrWhiteSpace(companyName) ? "Unknown company" : companyName.Trim(),
                    SurveyYear = surveyYear,
                    SubmittedAtUtc = submittedAt.Kind == DateTimeKind.Utc ? submittedAt : submittedAt.ToUniversalTime(),
                    SubmitterEmail = string.IsNullOrWhiteSpace(submitterEmail) ? null : submitterEmail.Trim()
                };

                var payload = JsonSerializer.Serialize(message);
                await queueClient.SendMessageAsync(payload);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed queuing survey submitted notification. Falling back to direct send.");
                return false;
            }
        }
    }
}