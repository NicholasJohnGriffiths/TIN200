using Azure;
using Azure.Communication.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.RegularExpressions;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class SurveyEmailService : ISurveyEmailService
    {
        private const string PlaceholderPromptText = "Please provide";

        private readonly ApplicationDbContext _context;
        private readonly AzureCommunicationEmailSettings _emailSettings;
        private readonly SurveyLinkSettings _surveyLinkSettings;

        public SurveyEmailService(
            ApplicationDbContext context,
            IOptions<AzureCommunicationEmailSettings> emailOptions,
            IOptions<SurveyLinkSettings> surveyLinkOptions)
        {
            _context = context;
            _emailSettings = emailOptions.Value;
            _surveyLinkSettings = surveyLinkOptions.Value;
        }

        public async Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId, int emailContentId, string? senderEmail = null, string? replyToEmail = null)
        {
            EnsureEmailConfigured();

            var recipientName = string.IsNullOrWhiteSpace(companyName) ? "there" : companyName.Trim();
            var senderDisplayName = GetSenderDisplayName();
            var supportEmail = "tin100@tinetwork.com";
            var defaultSubject = "TIN200 survey request: please review your company details";

            var unsubscribeToken = GenerateUnsubscribeToken(clientId);
            var baseUrl = _surveyLinkSettings.BaseUrl ?? string.Empty;
            baseUrl = baseUrl.Trim().TrimEnd('/');
            var unsubscribeUrl = $"{baseUrl}/Company/Unsubscribe?id={clientId}&token={Uri.EscapeDataString(unsubscribeToken)}";

            var configuredEmailOptions = await _context.AppConfig
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new { c.EmailHeaderImageId })
                .FirstOrDefaultAsync();

            var selectedContent = await GetEmailContentAsync(emailContentId);
            var templateFieldValues = await BuildTemplateFieldValuesAsync(clientId, companyName);

            var emailHeaderImageUrl = BuildEmailHeaderImageUrl(configuredEmailOptions?.EmailHeaderImageId);

            var subjectTemplate = string.IsNullOrWhiteSpace(selectedContent.Subject)
                ? defaultSubject
                : selectedContent.Subject.Trim();
            var subject = ApplyBracketPlaceholders(subjectTemplate, templateFieldValues, encodeForHtml: false);

            var (plainTextBody, htmlBody) = BuildSurveyEmailBodies(
                selectedContent.Template,
                templateFieldValues,
                emailHeaderImageUrl,
                recipientName,
                surveyUrl,
                unsubscribeUrl,
                supportEmail,
                senderDisplayName);

            await SendEmailAsync(new[] { recipientEmail }, subject, plainTextBody, htmlBody, senderEmail, replyToEmail);
        }

        public async Task SendSurveyReminderLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId, int emailContentId, string? senderEmail = null, string? replyToEmail = null)
        {
            EnsureEmailConfigured();

            var recipientName = string.IsNullOrWhiteSpace(companyName) ? "there" : companyName.Trim();
            var senderDisplayName = GetSenderDisplayName();
            var supportEmail = "tin100@tinetwork.com";
            var currentSurveyYear = await _context.Survey
                .AsNoTracking()
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.FinancialYear)
                .FirstOrDefaultAsync() ?? DateTime.UtcNow.Year;

            var unsubscribeToken = GenerateUnsubscribeToken(clientId);
            var baseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var unsubscribeUrl = $"{baseUrl}/Company/Unsubscribe?id={clientId}&token={Uri.EscapeDataString(unsubscribeToken)}";

            var configuredEmailOptions = await _context.AppConfig
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new { c.EmailHeaderImageId })
                .FirstOrDefaultAsync();

            var selectedContent = await GetEmailContentAsync(emailContentId);
            var templateFieldValues = await BuildTemplateFieldValuesAsync(clientId, companyName);

            var subjectTemplate = string.IsNullOrWhiteSpace(selectedContent.Subject)
                ? $"Reminder: Complete Your {currentSurveyYear} TIN Survey"
                : selectedContent.Subject.Trim();
            var subject = ApplyBracketPlaceholders(subjectTemplate, templateFieldValues, encodeForHtml: false);

            var emailHeaderImageUrl = BuildEmailHeaderImageUrl(configuredEmailOptions?.EmailHeaderImageId);

            var (plainTextBody, htmlBody) = BuildSurveyEmailBodies(
                selectedContent.Template,
                templateFieldValues,
                emailHeaderImageUrl,
                recipientName,
                surveyUrl,
                unsubscribeUrl,
                supportEmail,
                senderDisplayName);

            await SendEmailAsync(new[] { recipientEmail }, subject, plainTextBody, htmlBody, senderEmail, replyToEmail);
        }

        public async Task<SurveyEmailPreviewResult> BuildSurveyEmailPreviewAsync(int emailContentId, string surveyUrl, string? companyName, int clientId)
        {
            var recipientName = string.IsNullOrWhiteSpace(companyName) ? "there" : companyName.Trim();
            var senderDisplayName = GetSenderDisplayName();
            var supportEmail = "tin100@tinetwork.com";

            var unsubscribeToken = GenerateUnsubscribeToken(clientId);
            var baseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var unsubscribeUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? string.Empty
                : $"{baseUrl}/Company/Unsubscribe?id={clientId}&token={Uri.EscapeDataString(unsubscribeToken)}";

            var configuredEmailOptions = await _context.AppConfig
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new { c.EmailHeaderImageId })
                .FirstOrDefaultAsync();

            var selectedContent = await GetEmailContentAsync(emailContentId);
            var templateFieldValues = await BuildTemplateFieldValuesAsync(clientId, companyName);
            var emailHeaderImageUrl = BuildEmailHeaderImageUrl(configuredEmailOptions?.EmailHeaderImageId);

            var (plainTextBody, htmlBody) = BuildSurveyEmailBodies(
                selectedContent.Template,
                templateFieldValues,
                emailHeaderImageUrl,
                recipientName,
                surveyUrl,
                unsubscribeUrl,
                supportEmail,
                senderDisplayName);

            return new SurveyEmailPreviewResult
            {
                Subject = ApplyBracketPlaceholders(selectedContent.Subject?.Trim() ?? string.Empty, templateFieldValues, encodeForHtml: false),
                PlainTextBody = plainTextBody,
                HtmlBody = htmlBody
            };
        }

        public async Task SendBounceNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            string bouncedRecipientEmail,
            string status,
            string? reason,
            string? messageId,
            string? eventId)
        {
            EnsureEmailConfigured();

            var safeCompanyName = string.IsNullOrWhiteSpace(companyName) ? "Unknown company" : companyName.Trim();
            var safeReason = string.IsNullOrWhiteSpace(reason)
                ? "No additional delivery details were provided."
                : reason.Trim();

            var subject = $"TIN200 survey email bounced - {safeCompanyName}";

            var plainTextBody = $@"A TIN200 survey email has bounced back.

Company: {safeCompanyName}
Survey year: {surveyYear}
Recipient: {bouncedRecipientEmail}
Status: {status}
Reason: {safeReason}
{(string.IsNullOrWhiteSpace(messageId) ? string.Empty : $"Message ID: {messageId}\r\n")}{(string.IsNullOrWhiteSpace(eventId) ? string.Empty : $"Event ID: {eventId}\r\n")}
Please review the contact email for this company before resending the survey.";

            var htmlBody = $@"<p>A <strong>TIN200</strong> survey email has bounced back.</p>
<ul>
    <li><strong>Company:</strong> {WebUtility.HtmlEncode(safeCompanyName)}</li>
    <li><strong>Survey year:</strong> {surveyYear}</li>
    <li><strong>Recipient:</strong> {WebUtility.HtmlEncode(bouncedRecipientEmail)}</li>
    <li><strong>Status:</strong> {WebUtility.HtmlEncode(status)}</li>
    <li><strong>Reason:</strong> {WebUtility.HtmlEncode(safeReason)}</li>
    {(string.IsNullOrWhiteSpace(messageId) ? string.Empty : $"<li><strong>Message ID:</strong> {WebUtility.HtmlEncode(messageId)}</li>")}
    {(string.IsNullOrWhiteSpace(eventId) ? string.Empty : $"<li><strong>Event ID:</strong> {WebUtility.HtmlEncode(eventId)}</li>")}
</ul>
<p>Please review the contact email for this company before resending the survey.</p>";

            await SendEmailAsync(new[] { adminEmail }, subject, plainTextBody, htmlBody);
        }

        public async Task SendSurveySubmittedNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime submittedAt,
            string? submitterEmail)
        {
            EnsureEmailConfigured();

            var safeCompanyName = string.IsNullOrWhiteSpace(companyName) ? "Unknown company" : companyName.Trim();
            var safeSubmitterEmail = string.IsNullOrWhiteSpace(submitterEmail)
                ? "Not available"
                : submitterEmail.Trim();

            var subject = $"TIN200 survey submitted - {safeCompanyName}";

            var plainTextBody = $@"A TIN200 survey has been submitted.

Company: {safeCompanyName}
Survey year: {surveyYear}
Submitted at: {submittedAt:yyyy-MM-dd HH:mm:ss}
Submitter email: {safeSubmitterEmail}

Please review the submitted survey and follow up if needed.";

            var htmlBody = $@"<p>A <strong>TIN200</strong> survey has been submitted.</p>
<ul>
    <li><strong>Company:</strong> {WebUtility.HtmlEncode(safeCompanyName)}</li>
    <li><strong>Survey year:</strong> {surveyYear}</li>
    <li><strong>Submitted at:</strong> {WebUtility.HtmlEncode(submittedAt.ToString("yyyy-MM-dd HH:mm:ss"))}</li>
    <li><strong>Submitter email:</strong> {WebUtility.HtmlEncode(safeSubmitterEmail)}</li>
</ul>
<p>Please review the submitted survey and follow up if needed.</p>";

            await SendEmailAsync(new[] { adminEmail }, subject, plainTextBody, htmlBody);
        }

        public async Task SendSurveySavedNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime savedAt,
            string? submitterEmail)
        {
            EnsureEmailConfigured();

            var safeCompanyName = string.IsNullOrWhiteSpace(companyName) ? "Unknown company" : companyName.Trim();
            var safeSubmitterEmail = string.IsNullOrWhiteSpace(submitterEmail)
                ? "Not available"
                : submitterEmail.Trim();

            var subject = $"TIN200 survey saved - {safeCompanyName}";

            var plainTextBody = $@"A TIN200 survey has been saved for later.

Company: {safeCompanyName}
Survey year: {surveyYear}
Saved at: {savedAt:yyyy-MM-dd HH:mm:ss}
Submitter email: {safeSubmitterEmail}

The survey has not been submitted yet.";

            var htmlBody = $@"<p>A <strong>TIN200</strong> survey has been saved for later.</p>
<ul>
    <li><strong>Company:</strong> {WebUtility.HtmlEncode(safeCompanyName)}</li>
    <li><strong>Survey year:</strong> {surveyYear}</li>
    <li><strong>Saved at:</strong> {WebUtility.HtmlEncode(savedAt.ToString("yyyy-MM-dd HH:mm:ss"))}</li>
    <li><strong>Submitter email:</strong> {WebUtility.HtmlEncode(safeSubmitterEmail)}</li>
</ul>
<p>The survey has not been submitted yet.</p>";

            await SendEmailAsync(new[] { adminEmail }, subject, plainTextBody, htmlBody);
        }

        private (string PlainTextBody, string HtmlBody) BuildSurveyEmailBodies(
            string? configuredTemplate,
            IReadOnlyDictionary<string, string> templateFieldValues,
            string? emailHeaderImageUrl,
            string recipientName,
            string surveyUrl,
            string unsubscribeUrl,
            string supportEmail,
            string senderDisplayName)
        {
            var unsubscribePlainText = $@"To unsubscribe from future TIN200 surveys:
{unsubscribeUrl}";

            var unsubscribeHtml = $@"<p><small><a href=""{WebUtility.HtmlEncode(unsubscribeUrl)}"" style=""color: #999; font-size: 12px;"">Unsubscribe from future surveys</a></small></p>";

            var footerPlainText = $@"If you did not expect this email, you can safely ignore it.

Need help? Contact {supportEmail}.

{unsubscribePlainText}

Regards,
{senderDisplayName}";

            var footerHtml = $@"<p>If you did not expect this email, you can safely ignore it.</p>
<p>Need help? Contact <a href=""mailto:{WebUtility.HtmlEncode(supportEmail)}"">{WebUtility.HtmlEncode(supportEmail)}</a>.</p>
{unsubscribeHtml}
<p>Regards,<br/>{WebUtility.HtmlEncode(senderDisplayName)}</p>";

            var emailHeaderHtml = BuildEmailHeaderImageHtml(emailHeaderImageUrl);

            if (!string.IsNullOrWhiteSpace(configuredTemplate))
            {
                var htmlTemplate = ApplySurveyTemplate(configuredTemplate, templateFieldValues, recipientName, surveyUrl, encodeForHtml: true);
                var plainTextTemplate = ConvertHtmlToPlainText(ApplySurveyTemplate(configuredTemplate, templateFieldValues, recipientName, surveyUrl, encodeForHtml: false));

                var plainTextBody = string.IsNullOrWhiteSpace(plainTextTemplate)
                    ? unsubscribePlainText
                    : $"{plainTextTemplate}\r\n\r\n{unsubscribePlainText}";

                var htmlBody = string.IsNullOrWhiteSpace(htmlTemplate)
                    ? $"{emailHeaderHtml}{unsubscribeHtml}"
                    : $"{emailHeaderHtml}{htmlTemplate}\n{unsubscribeHtml}";

                return (plainTextBody, htmlBody);
            }

            var fallbackPlainTextBody = $@"Hello {recipientName},

You have been invited to review and update your company details for TIN200.

Open your secure survey link
{surveyUrl}

{footerPlainText}";

            var fallbackHtmlBody = $@"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>
<p>You have been invited to review and update your company details for <strong>TIN200</strong>.</p>
<p><a href=""{WebUtility.HtmlEncode(surveyUrl)}"">Open your secure survey link</a></p>
{footerHtml}";

            fallbackHtmlBody = $"{emailHeaderHtml}{fallbackHtmlBody}";

            return (fallbackPlainTextBody, fallbackHtmlBody);
        }

        private async Task<Dictionary<string, string>> BuildTemplateFieldValuesAsync(int clientId, string? fallbackCompanyName)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var companyInfo = await _context.Tin200
                    .AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => new
                    {
                        c.CompanyName,
                        c.CeoFirstName,
                        c.CeoLastName,
                        c.Website,
                        c.Phone,
                        c.AddStreet,
                        c.AddSuburb,
                        c.AddCity,
                        c.AddPostcode
                    })
                    .FirstOrDefaultAsync();

            var companyName = (companyInfo?.CompanyName)
                ?? fallbackCompanyName
                ?? string.Empty;

            values[NormalizeTemplateKey("CompanyName")] = companyName;
            values[NormalizeTemplateKey("Company Name")] = companyName;

            var fallbackCeoFirstName = companyInfo?.CeoFirstName?.Trim() ?? string.Empty;
            var fallbackCeoLastName = companyInfo?.CeoLastName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fallbackCeoFirstName))
            {
                values[NormalizeTemplateKey("CEO First Name")] = fallbackCeoFirstName;
                values[NormalizeTemplateKey("CeoFirstName")] = fallbackCeoFirstName;
            }

            if (!string.IsNullOrWhiteSpace(fallbackCeoLastName))
            {
                values[NormalizeTemplateKey("CEO Last Name")] = fallbackCeoLastName;
                values[NormalizeTemplateKey("CeoLastName")] = fallbackCeoLastName;
            }

            var companySurveyHistory = await (
                from cs in _context.CompanySurvey.AsNoTracking()
                join s in _context.Survey.AsNoTracking() on cs.SurveyId equals s.Id
                where cs.CompanyId == clientId
                orderby s.CurrentSurvey descending, s.FinancialYear descending, cs.Id descending
                select new
                {
                    CompanySurveyId = (int?)cs.Id,
                    s.FinancialYear,
                    cs.Estimate
                }
            ).ToListAsync();

            var companySurveyInfo = companySurveyHistory.FirstOrDefault();
            var previousYearSurveyInfo = companySurveyInfo == null
                ? null
                : companySurveyHistory
                    .FirstOrDefault(x => x.FinancialYear < companySurveyInfo.FinancialYear);

            var isEstimated = companySurveyInfo?.Estimate == true;
            var isEstimatedYearMinus1 = previousYearSurveyInfo?.Estimate == true;
            values["Estimated"] = isEstimated ? "Estimated" : "Not Estimated";
            values[NormalizeTemplateKey("Estimated Year-1")] = previousYearSurveyInfo == null
                ? string.Empty
                : (isEstimatedYearMinus1 ? "Estimated" : "Not Estimated");

            if (companySurveyInfo?.CompanySurveyId.HasValue == true)
            {
                var answers = await (
                    from a in _context.Answer.AsNoTracking()
                    join q in _context.Question.AsNoTracking() on a.QuestionId equals q.Id
                    where a.CompanySurveyId == companySurveyInfo.CompanySurveyId.Value && q.Title != null
                    orderby a.Id descending
                    select new
                    {
                        QuestionTitle = q.Title!,
                        a.AnswerText,
                        a.AnswerCurrency,
                        a.AnswerNumber
                    }
                ).ToListAsync();

                foreach (var answer in answers)
                {
                    var normalizedQuestionTitle = NormalizeTemplateKey(answer.QuestionTitle);
                    if (values.ContainsKey(normalizedQuestionTitle))
                    {
                        continue;
                    }

                    var resolvedValue = ResolveAnswerValue(answer.QuestionTitle, answer.AnswerText, answer.AnswerCurrency, answer.AnswerNumber);
                    if (string.IsNullOrWhiteSpace(resolvedValue))
                    {
                        continue;
                    }

                    values[normalizedQuestionTitle] = resolvedValue;
                }
            }

            EnsurePromptFallbackValue(values, "Business Decision");
            EnsurePromptFallbackValue(values, "Key Products");
            EnsureDollarPrefix(values, "Total Revenue Last Financial Year");
            EnsureDollarPrefix(values, "Total Revenue Year-1");

            var webAddress = companyInfo?.Website?.Trim();
            var companyPhone = companyInfo?.Phone?.Trim();
            var physicalAddress = BuildPhysicalAddress(
                companyInfo?.AddStreet,
                companyInfo?.AddSuburb,
                companyInfo?.AddCity,
                companyInfo?.AddPostcode);

            values[NormalizeTemplateKey("Web Address")] = string.IsNullOrWhiteSpace(webAddress) ? "TBC" : webAddress;
            values[NormalizeTemplateKey("WebAddress")] = string.IsNullOrWhiteSpace(webAddress) ? "TBC" : webAddress;
            values[NormalizeTemplateKey("Company Phone")] = string.IsNullOrWhiteSpace(companyPhone) ? "TBC" : companyPhone;
            values[NormalizeTemplateKey("CompanyPhone")] = string.IsNullOrWhiteSpace(companyPhone) ? "TBC" : companyPhone;
            values[NormalizeTemplateKey("Physical Address")] = string.IsNullOrWhiteSpace(physicalAddress) ? "TBC" : physicalAddress;
            values[NormalizeTemplateKey("PhysicalAddress")] = string.IsNullOrWhiteSpace(physicalAddress) ? "TBC" : physicalAddress;

            return values;
        }

        private static string ResolveAnswerValue(string? questionTitle, string? answerText, decimal? answerCurrency, double? answerNumber)
        {
            if (!string.IsNullOrWhiteSpace(answerText))
            {
                return answerText.Trim();
            }

            var normalizedQuestionTitle = NormalizeTemplateKey(questionTitle);
            var useWholeNumberFormat = ShouldFormatAsWholeNumber(normalizedQuestionTitle);

            if (answerCurrency.HasValue)
            {
                return useWholeNumberFormat
                    ? Math.Round(answerCurrency.Value, 0, MidpointRounding.AwayFromZero).ToString("N0")
                    : answerCurrency.Value.ToString("N2");
            }

            if (answerNumber.HasValue)
            {
                return useWholeNumberFormat
                    ? Math.Round((decimal)answerNumber.Value, 0, MidpointRounding.AwayFromZero).ToString("N0")
                    : answerNumber.Value.ToString("0.##");
            }

            return string.Empty;
        }

        private static bool ShouldFormatAsWholeNumber(string? normalizedQuestionTitle)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuestionTitle))
            {
                return false;
            }

            return normalizedQuestionTitle.Contains("revenue", StringComparison.OrdinalIgnoreCase)
                || normalizedQuestionTitle.Contains("employment", StringComparison.OrdinalIgnoreCase)
                || normalizedQuestionTitle.Contains("staffemployed", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPhysicalAddress(string? street, string? suburb, string? city, string? postcode)
        {
            var parts = new[] { street, suburb, city, postcode }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToList();

            return parts.Count == 0
                ? string.Empty
                : string.Join(", ", parts);
        }

        private string? BuildEmailHeaderImageUrl(int? emailHeaderImageId)
        {
            if (!emailHeaderImageId.HasValue)
            {
                return null;
            }

            var baseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            return $"{baseUrl}/api/config/email-header-image/{emailHeaderImageId.Value}";
        }

        private static string BuildEmailHeaderImageHtml(string? emailHeaderImageUrl)
        {
            if (string.IsNullOrWhiteSpace(emailHeaderImageUrl))
            {
                return string.Empty;
            }

            return $@"<div style=""margin: 0 0 16px 0; text-align: left;"">
<img src=""{WebUtility.HtmlEncode(emailHeaderImageUrl)}"" alt=""TIN200"" style=""max-width: 100%; height: auto; display: block;"" />
</div>
";
        }

        private static string ApplySurveyTemplate(string template, IReadOnlyDictionary<string, string> templateFieldValues, string recipientName, string surveyUrl, bool encodeForHtml)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var companyReplacement = encodeForHtml
                ? WebUtility.HtmlEncode(recipientName)
                : recipientName;

            var surveyLinkReplacement = encodeForHtml
                ? $@"<a id=""surveylink"" href=""{WebUtility.HtmlEncode(surveyUrl)}"">{WebUtility.HtmlEncode(surveyUrl)}</a>"
                : surveyUrl;

            var result = template
                .Replace("(Company Name)", companyReplacement, StringComparison.OrdinalIgnoreCase)
                .Replace("(Name)", companyReplacement, StringComparison.OrdinalIgnoreCase)
                .Replace("(Survey link)", surveyLinkReplacement, StringComparison.OrdinalIgnoreCase);

            result = ApplyBracketPlaceholders(result, templateFieldValues, encodeForHtml);
            result = ApplyParenthesisPlaceholders(result, templateFieldValues, encodeForHtml);

            if (encodeForHtml)
            {
                result = Regex.Replace(
                    result,
                    @"<a\b(?=[^>]*\bid\s*=\s*['""]surveylink['""])([^>]*)>(.*?)</a>",
                    match =>
                    {
                        var attributes = Regex.Replace(
                            match.Groups[1].Value,
                            @"\s+href\s*=\s*(['""]).*?\1",
                            string.Empty,
                            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

                        var attributePrefix = string.IsNullOrWhiteSpace(attributes)
                            ? string.Empty
                            : $" {attributes}";

                        var linkText = match.Groups[2].Value;
                        return $@"<a{attributePrefix} href=""{WebUtility.HtmlEncode(surveyUrl)}"">{linkText}</a>";
                    },
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            else
            {
                result = Regex.Replace(
                    result,
                    @"<a\b(?=[^>]*\bid\s*=\s*['""]surveylink['""])([^>]*)>(.*?)</a>",
                    surveyUrl,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            return result;
        }

        private static string ApplyBracketPlaceholders(string content, IReadOnlyDictionary<string, string> values, bool encodeForHtml)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            return Regex.Replace(
                content,
                @"\[(?<name>[^\[\]]+)\]",
                match =>
                {
                    var key = NormalizeTemplateKey(match.Groups["name"].Value);
                    if (string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out var value))
                    {
                        return match.Value;
                    }

                    return FormatPlaceholderValue(value, encodeForHtml);
                },
                RegexOptions.None,
                TimeSpan.FromMilliseconds(500));
        }

        private static string ApplyParenthesisPlaceholders(string content, IReadOnlyDictionary<string, string> values, bool encodeForHtml)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            return Regex.Replace(
                content,
                @"\((?<name>[^\(\)\r\n]{1,200})\)",
                match =>
                {
                    var key = NormalizeTemplateKey(match.Groups["name"].Value);
                    if (string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out var value))
                    {
                        return match.Value;
                    }

                    return FormatPlaceholderValue(value, encodeForHtml);
                },
                RegexOptions.None,
                TimeSpan.FromMilliseconds(500));
        }

        private static string FormatPlaceholderValue(string value, bool encodeForHtml)
        {
            if (string.Equals(value, PlaceholderPromptText, StringComparison.OrdinalIgnoreCase))
            {
                return encodeForHtml ? "<em>Please provide</em>" : PlaceholderPromptText;
            }

            return encodeForHtml ? WebUtility.HtmlEncode(value) : value;
        }

        private static void EnsurePromptFallbackValue(IDictionary<string, string> values, string key)
        {
            var normalizedKey = NormalizeTemplateKey(key);
            if (!values.TryGetValue(normalizedKey, out var existingValue)
                || string.IsNullOrWhiteSpace(existingValue))
            {
                values[normalizedKey] = PlaceholderPromptText;
            }
        }

        private static void EnsureDollarPrefix(IDictionary<string, string> values, string key)
        {
            var normalizedKey = NormalizeTemplateKey(key);
            if (!values.TryGetValue(normalizedKey, out var existingValue)
                || string.IsNullOrWhiteSpace(existingValue))
            {
                return;
            }

            var trimmed = existingValue.Trim();
            if (trimmed.StartsWith("$", StringComparison.Ordinal))
            {
                return;
            }

            values[normalizedKey] = $"${trimmed}";
        }

        private static string NormalizeTemplateKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return Regex.Replace(raw.Trim(), @"\s+", " ");
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var text = Regex.Replace(html, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<\s*/p\s*>", "\n\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = WebUtility.HtmlDecode(text);

            return Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
        }

        private async Task<TINWeb.Models.EmailContent> GetEmailContentAsync(int emailContentId)
        {
            var selectedContent = await _context.EmailContent
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == emailContentId && x.Active);

            if (selectedContent == null)
            {
                throw new InvalidOperationException("Selected email content was not found or is inactive.");
            }

            return selectedContent;
        }

        private void EnsureEmailConfigured()
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.ConnectionString)
                || string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
            {
                throw new InvalidOperationException("Azure Communication Email settings are not configured. Please configure AzureCommunicationEmail in appsettings or environment settings.");
            }
        }

        private async Task SendEmailAsync(IEnumerable<string> recipientEmails, string subject, string plainTextBody, string htmlBody, string? senderEmailOverride = null, string? replyToEmailOverride = null)
        {
            var recipients = ParseRecipientEmails(recipientEmails).ToList();
            if (recipients.Count == 0)
            {
                throw new InvalidOperationException("No valid recipient email addresses were provided.");
            }

            var emailClient = new EmailClient(_emailSettings.ConnectionString);
            var senderEmail = ResolveSenderEmailAddress(senderEmailOverride);
            var replyToAddress = ResolveReplyToAddress(replyToEmailOverride, senderEmail);

            var emailMessage = new EmailMessage(
                senderAddress: BuildSenderAddress(senderEmail, _emailSettings.FromName),
                content: new Azure.Communication.Email.EmailContent(subject)
                {
                    PlainText = plainTextBody,
                    Html = htmlBody
                },
                recipients: new EmailRecipients(recipients.Select(email => new EmailAddress(email)).ToList()));

            if (!string.IsNullOrWhiteSpace(replyToAddress))
            {
                emailMessage.ReplyTo.Add(new EmailAddress(replyToAddress));
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await emailClient.SendAsync(WaitUntil.Started, emailMessage, cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                throw new InvalidOperationException(
                    "Azure Communication Email send did not receive an acceptance response within 30 seconds. The request was cancelled to avoid the survey send page hanging.",
                    ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 401)
            {
                throw new InvalidOperationException(
                    "Azure Communication Email authorization failed (401). Verify AzureCommunicationEmail__ConnectionString is from the correct Communication Services resource and AzureCommunicationEmail__FromEmail is a sender address from a verified/connected domain for that resource.",
                    ex);
            }
            catch (RequestFailedException ex)
            {
                throw new InvalidOperationException(
                    $"Azure Communication Email send failed. Status: {ex.Status}, Code: {ex.ErrorCode}, Message: {ex.Message}",
                    ex);
            }
        }

        private string GetSenderDisplayName()
        {
            return string.IsNullOrWhiteSpace(_emailSettings.FromName)
                ? "TIN Survey"
                : _emailSettings.FromName.Trim();
        }

        private static string BuildSenderAddress(string fromEmail, string? fromName)
        {
            // Azure Communication Email expects senderAddress to be only the email address.
            // The configured FromName is still used throughout the survey email content and should
            // also match the sender identity configured in Azure for the mailbox.
            // return "tin@tin100.com"; // Temporary override previously used.
            return fromEmail.Trim();
        }

        private string ResolveSenderEmailAddress(string? senderEmailOverride)
        {
            if (!string.IsNullOrWhiteSpace(senderEmailOverride))
            {
                return senderEmailOverride.Trim();
            }

            return _emailSettings.FromEmail.Trim();
        }

        private static string ResolveReplyToAddress(string? replyToEmailOverride, string senderEmail)
        {
            if (!string.IsNullOrWhiteSpace(replyToEmailOverride))
            {
                return replyToEmailOverride.Trim();
            }

            return string.IsNullOrWhiteSpace(senderEmail)
                ? string.Empty
                : senderEmail.Trim();
        }

        private static IEnumerable<string> ParseRecipientEmails(IEnumerable<string> recipientEmails)
        {
            return recipientEmails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .SelectMany(email => email.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(email => email.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private string GenerateUnsubscribeToken(int clientId)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("unsubscribe-token-key")))
            {
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{DateTime.UtcNow:yyyyMMdd}"));
                return Convert.ToBase64String(hash);
            }
        }
    }
}
