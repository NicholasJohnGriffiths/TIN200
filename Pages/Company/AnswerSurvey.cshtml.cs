using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    [IgnoreAntiforgeryToken]
    [RequestFormLimits(ValueCountLimit = 20000)]
    public class AnswerSurveyModel : PageModel
    {
        private const string AccessCookiePrefix = "survey_access_";
        private readonly ApplicationDbContext _context;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;
        private readonly IImageStorageService _imageStorageService;
        private readonly TaskService _taskService;

        public AnswerSurveyModel(ApplicationDbContext context, ISurveyLinkTokenService surveyLinkTokenService, IImageStorageService imageStorageService, TaskService taskService)
        {
            _context = context;
            _surveyLinkTokenService = surveyLinkTokenService;
            _imageStorageService = imageStorageService;
            _taskService = taskService;
        }

        public Tin200 Company { get; set; } = new();
        public int FinancialYear { get; set; }

        [BindProperty]
        public int CompanyId { get; set; }

        [BindProperty]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        public string FormAction { get; set; } = string.Empty;

        [BindProperty]
        public List<AnswerEditRow> Rows { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int CurrentGroupIndex { get; set; }

        public string? SurveyHeaderImageUrl { get; set; }

        public bool Saved { get; set; }
        public bool Submitted { get; set; }
        public bool IsLocked { get; set; }
        public string SurveyPageTitle { get; set; } = "Survey Answers";
        public string SurveyPageDescription { get; set; } = "Please complete the survey answers for the current survey year.";
        public List<QuestionGroup> SurveyQuestionGroups { get; set; } = new();
        public HashSet<int> AvailableGroupImageIds { get; set; } = new();

        public string ResolveFinancialYearDisplayText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (FinancialYear <= 0)
            {
                return text;
            }

            var resolvedText = Regex.Replace(
                text,
                @"last\s*fin\w*\s*year",
                FinancialYear.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            resolvedText = Regex.Replace(
                resolvedText,
                @"current\s*financial\s*year\s*-\s*(\d+)|year\s*-\s*(\d+)",
                match =>
                {
                    var offsetGroup = match.Groups[1].Success ? match.Groups[1] : match.Groups[2];
                    if (!int.TryParse(offsetGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
                    {
                        return match.Value;
                    }

                    return (FinancialYear - offset).ToString(CultureInfo.InvariantCulture);
                },
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            resolvedText = Regex.Replace(
                resolvedText,
                @"current\s*financial\s*year",
                FinancialYear.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return resolvedText;
        }

        public bool HasPreviousYearValue(AnswerEditRow row)
        {
            if (!row.DisplayPreviousYear)
            {
                return false;
            }

            var answerType = (row.AnswerType ?? string.Empty).Trim();

            if (answerType.Equals("Number", StringComparison.OrdinalIgnoreCase))
            {
                return row.PreviousYearAnswerNumber.HasValue || !string.IsNullOrWhiteSpace(row.PreviousYearValue);
            }

            if (answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
            {
                return row.PreviousYearAnswerCurrency.HasValue || !string.IsNullOrWhiteSpace(row.PreviousYearValue);
            }

            if (answerType.Equals("Multichoice", StringComparison.OrdinalIgnoreCase)
                || answerType.Equals("MultiChoice", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(row.PreviousYearAnswerText)
                    || !string.IsNullOrWhiteSpace(row.PreviousYearValue);
            }

            return !string.IsNullOrWhiteSpace(row.PreviousYearAnswerText)
                || !string.IsNullOrWhiteSpace(row.PreviousYearValue);
        }

        public bool HasCurrentAnswer(AnswerEditRow row)
        {
            var answerType = (row.AnswerType ?? string.Empty).Trim();

            if (answerType.Equals("Number", StringComparison.OrdinalIgnoreCase))
            {
                return row.AnswerNumber.HasValue;
            }

            if (answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
            {
                return row.AnswerCurrency.HasValue;
            }

            if (answerType.Equals("Multichoice", StringComparison.OrdinalIgnoreCase)
                || answerType.Equals("MultiChoice", StringComparison.OrdinalIgnoreCase))
            {
                return row.SelectedChoices?.Count > 0;
            }

            return !string.IsNullOrWhiteSpace(row.AnswerText);
        }

        public bool ShouldHidePreviousYearCopy(string? text)
        {
            var offset = ResolveHistoricalQuestionOffset(text);
            return offset is 1 or 2;
        }

        private int? ResolveHistoricalQuestionOffset(string? text)
        {
            return ResolveHistoricalQuestionOffset(FinancialYear, text);
        }

        private static int? ResolveHistoricalQuestionOffset(int financialYear, params string?[] candidates)
        {
            if (financialYear <= 0)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                var normalized = NormalizeQuestionKey(candidate);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (normalized.Contains("lastfinancialyear", StringComparison.Ordinal)
                    || normalized.Contains("lastfinacialyear", StringComparison.Ordinal)
                    || normalized.Contains("lastfinacnailyear", StringComparison.Ordinal))
                {
                    return 0;
                }

                if (normalized.Contains("currentfinancialyear01", StringComparison.Ordinal)
                    || normalized.Contains("currentfinancialyear1", StringComparison.Ordinal)
                    || normalized.Contains("year01", StringComparison.Ordinal)
                    || normalized.Contains("year1", StringComparison.Ordinal))
                {
                    return 1;
                }

                if (normalized.Contains("currentfinancialyear02", StringComparison.Ordinal)
                    || normalized.Contains("currentfinancialyear2", StringComparison.Ordinal)
                    || normalized.Contains("year02", StringComparison.Ordinal)
                    || normalized.Contains("year2", StringComparison.Ordinal))
                {
                    return 2;
                }

                var matches = Regex.Matches(candidate, @"(?:19|20)\d{2}");
                foreach (Match match in matches)
                {
                    if (!int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
                    {
                        continue;
                    }

                    var offset = (financialYear - 1) - year;
                    if (offset >= 0 && offset <= 2)
                    {
                        return offset;
                    }
                }
            }

            return null;
        }

        private static string? BuildTemporalBaseKey(int financialYear, int? groupId, int? subgroupId, params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var normalized = NormalizeQuestionKey(candidate);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                var baseKey = normalized;
                var yearsToRemove = new[]
                {
                    (financialYear - 1).ToString(CultureInfo.InvariantCulture),
                    (financialYear - 2).ToString(CultureInfo.InvariantCulture),
                    (financialYear - 3).ToString(CultureInfo.InvariantCulture)
                };

                foreach (var year in yearsToRemove)
                {
                    baseKey = baseKey.Replace(year, string.Empty, StringComparison.Ordinal);
                }

                baseKey = baseKey
                    .Replace("lastfinancialyear", string.Empty, StringComparison.Ordinal)
                    .Replace("lastfinacialyear", string.Empty, StringComparison.Ordinal)
                    .Replace("lastfinacnailyear", string.Empty, StringComparison.Ordinal)
                    .Replace("currentfinancialyear01", string.Empty, StringComparison.Ordinal)
                    .Replace("currentfinancialyear1", string.Empty, StringComparison.Ordinal)
                    .Replace("currentfinancialyear02", string.Empty, StringComparison.Ordinal)
                    .Replace("currentfinancialyear2", string.Empty, StringComparison.Ordinal)
                    .Replace("year01", string.Empty, StringComparison.Ordinal)
                    .Replace("year1", string.Empty, StringComparison.Ordinal)
                    .Replace("year02", string.Empty, StringComparison.Ordinal)
                    .Replace("year2", string.Empty, StringComparison.Ordinal)
                    .Trim();

                if (!string.IsNullOrWhiteSpace(baseKey))
                {
                    return baseKey;
                }
            }

            if (subgroupId.HasValue)
            {
                return $"group-{groupId ?? 0}-subgroup-{subgroupId.Value}";
            }

            if (groupId.HasValue)
            {
                return $"group-{groupId.Value}";
            }

            return null;
        }

        private static string NormalizeQuestionKey(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return new string(text
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static bool HasAnswerValue(Answer? answer)
        {
            return answer != null
                && (!string.IsNullOrWhiteSpace(answer.AnswerText)
                    || answer.AnswerNumber.HasValue
                    || answer.AnswerCurrency.HasValue);
        }

        private static Answer? ResolvePriorYearSourceAnswer(
            int financialYear,
            string baseKey,
            int targetOffset,
            IReadOnlyDictionary<(int SurveyFinancialYear, string BaseKey, int Offset), Answer> priorAnswersByYearKey)
        {
            if (targetOffset == 0)
            {
                if (priorAnswersByYearKey.TryGetValue((financialYear - 1, baseKey, 0), out var lastFinancialYearSource)
                    && HasAnswerValue(lastFinancialYearSource))
                {
                    return lastFinancialYearSource;
                }

                return null;
            }

            for (var step = 1; step <= targetOffset; step++)
            {
                var sourceSurveyYear = financialYear - step;
                var sourceOffset = targetOffset - step;

                if (sourceOffset < 0)
                {
                    continue;
                }

                if (priorAnswersByYearKey.TryGetValue((sourceSurveyYear, baseKey, sourceOffset), out var source)
                    && HasAnswerValue(source))
                {
                    return source;
                }
            }

            return null;
        }

        public async Task<IActionResult> OnGetAsync(int id, string token, bool saved = false, bool submitted = false)
        {
            token = GetEffectiveToken(token);
            var hasSurveyAccessCookie = Request.Cookies.TryGetValue($"{AccessCookiePrefix}{id}", out var accessCookieValue)
                && string.Equals(accessCookieValue, "1", StringComparison.Ordinal);
            var hasValidToken = !string.IsNullOrWhiteSpace(token) && _surveyLinkTokenService.IsTokenValid(id, token);

            if (!hasValidToken && !hasSurveyAccessCookie)
            {
                return RedirectToPage("/Company/SurveyLinkInvalid", new { id, reason = "invalid-token" });
            }

            var company = await _context.Tin200.FirstOrDefaultAsync(c => c.Id == id);
            if (company == null)
            {
                return RedirectToPage("/Company/SurveyLinkInvalid", new { id, reason = "company-not-found" });
            }

            Company = company;
            CompanyId = company.Id;
            Token = hasValidToken ? token : string.Empty;
            Saved = saved;
            Submitted = submitted;

            Response.Cookies.Append(
                $"{AccessCookiePrefix}{company.Id}",
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(8)
                });

            var survey = await GetCurrentSurveyAsync();
            if (survey == null)
            {
                ModelState.AddModelError(string.Empty, "No current survey is configured.");
                return Page();
            }

            FinancialYear = survey.FinancialYear;
            SurveyPageTitle = string.IsNullOrWhiteSpace(survey.Title) ? "Survey Answers" : survey.Title.Trim();
            SurveyPageDescription = string.IsNullOrWhiteSpace(survey.Description) ? "Please complete the survey answers for the current survey year." : survey.Description.Trim();
            SurveyHeaderImageUrl = await BuildSurveyHeaderImageUrlAsync(company.Id, Token, survey);
            var companySurveyId = await EnsureCompanySurveyAsync(company.Id, survey.Id);
            var companySurvey = await _context.CompanySurvey.FirstOrDefaultAsync(cs => cs.Id == companySurveyId);
            IsLocked = (companySurvey?.Locked).GetValueOrDefault();

            Rows = await LoadAnswerRowsAsync(company.Id, companySurveyId, survey.FinancialYear);
            SurveyQuestionGroups = await LoadSurveyQuestionGroupsAsync();
            AvailableGroupImageIds = await GetAvailableGroupImageIdsAsync(Rows, SurveyQuestionGroups);
            return Page();
        }

        public async Task<IActionResult> OnGetGroupImageAsync(int id, int groupId, int imageId, string? token)
        {
            var effectiveToken = GetEffectiveToken(token);
            var hasSurveyAccessCookie = Request.Cookies.TryGetValue($"{AccessCookiePrefix}{id}", out var accessCookieValue)
                && string.Equals(accessCookieValue, "1", StringComparison.Ordinal);
            var hasValidToken = !string.IsNullOrWhiteSpace(effectiveToken) && _surveyLinkTokenService.IsTokenValid(id, effectiveToken);

            if (!hasValidToken && !hasSurveyAccessCookie)
            {
                return NotFound();
            }

            var group = await _context.QuestionGroup.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
            {
                return NotFound();
            }

            if (group.ImageId1 != imageId && group.ImageId2 != imageId && group.ImageId3 != imageId)
            {
                return NotFound();
            }

            var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == imageId);
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return NotFound();
            }

            var stream = await _imageStorageService.OpenReadAsync(image.FilePath);
            if (stream == null)
            {
                return NotFound();
            }

            return File(stream, GetContentTypeFromPath(image.FilePath));
        }

        public async Task<IActionResult> OnGetSurveyHeaderImageAsync(int id, string? token)
        {
            var effectiveToken = GetEffectiveToken(token);
            var hasSurveyAccessCookie = Request.Cookies.TryGetValue($"{AccessCookiePrefix}{id}", out var accessCookieValue)
                && string.Equals(accessCookieValue, "1", StringComparison.Ordinal);
            var hasValidToken = !string.IsNullOrWhiteSpace(effectiveToken) && _surveyLinkTokenService.IsTokenValid(id, effectiveToken);

            if (!hasValidToken && !hasSurveyAccessCookie)
            {
                return NotFound();
            }

            var survey = await GetCurrentSurveyAsync();
            if (survey?.HeaderImageId == null)
            {
                return NotFound();
            }

            var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == survey.HeaderImageId.Value);
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return NotFound();
            }

            var stream = await _imageStorageService.OpenReadAsync(image.FilePath);
            if (stream == null)
            {
                return NotFound();
            }

            var contentType = GetContentTypeFromPath(image.FilePath);
            return File(stream, contentType);
        }

        public async Task<IActionResult> OnPostAsync(int id, string? token)
        {
            var effectiveToken = GetEffectiveToken(string.IsNullOrWhiteSpace(Token) ? token : Token);
            var hasSurveyAccessCookie = Request.Cookies.TryGetValue($"{AccessCookiePrefix}{id}", out var accessCookieValue)
                && string.Equals(accessCookieValue, "1", StringComparison.Ordinal);
            var hasValidToken = !string.IsNullOrWhiteSpace(effectiveToken) && _surveyLinkTokenService.IsTokenValid(id, effectiveToken);
            var baseSurveyPath = $"{Request.Scheme}://{Request.Host}/Company/AnswerSurvey/{id}";
            var hasSameOriginPost = string.Equals(Request.Headers.Origin.ToString(), $"{Request.Scheme}://{Request.Host}", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Request.Headers.Referer.ToString(), baseSurveyPath, StringComparison.OrdinalIgnoreCase)
                || Request.Headers.Referer.ToString().StartsWith(baseSurveyPath + "?", StringComparison.OrdinalIgnoreCase)
                || Request.Headers.Referer.ToString().StartsWith(baseSurveyPath + "/", StringComparison.OrdinalIgnoreCase);

            if (!hasValidToken && !hasSurveyAccessCookie && !hasSameOriginPost)
            {
                return RedirectToPage("/Company/SurveyLinkInvalid", new { id, reason = "post-auth-failed" });
            }

            id = ResolveCompanyId(id, effectiveToken);

            Token = hasValidToken ? effectiveToken! : string.Empty;

            var company = await _context.Tin200.FirstOrDefaultAsync(c => c.Id == id);
            if (company == null)
            {
                return RedirectToPage("/Company/SurveyLinkInvalid", new { id, reason = "company-not-found" });
            }

            Company = company;
            CompanyId = company.Id;

            var survey = await GetCurrentSurveyAsync();
            if (survey == null)
            {
                ModelState.AddModelError(string.Empty, "No current survey is configured.");
                return Page();
            }

            FinancialYear = survey.FinancialYear;
            SurveyPageTitle = string.IsNullOrWhiteSpace(survey.Title) ? "Survey Answers" : survey.Title.Trim();
            SurveyPageDescription = string.IsNullOrWhiteSpace(survey.Description) ? "Please complete the survey answers for the current survey year." : survey.Description.Trim();
            SurveyHeaderImageUrl = await BuildSurveyHeaderImageUrlAsync(company.Id, Token, survey);
            var companySurveyId = await EnsureCompanySurveyAsync(company.Id, survey.Id);
            var companySurvey = await _context.CompanySurvey.FirstOrDefaultAsync(cs => cs.Id == companySurveyId);
            IsLocked = (companySurvey?.Locked).GetValueOrDefault();

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "This survey record is locked. Please contact the Technology Investment Network.");
                Rows = await LoadAnswerRowsAsync(company.Id, companySurveyId, survey.FinancialYear);
                SurveyQuestionGroups = await LoadSurveyQuestionGroupsAsync();
                AvailableGroupImageIds = await GetAvailableGroupImageIdsAsync(Rows, SurveyQuestionGroups);
                return Page();
            }

            var questionById = await _context.Question
                .Where(q => Rows.Select(r => r.QuestionId).Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            foreach (var row in Rows)
            {
                if (!questionById.TryGetValue(row.QuestionId, out var question))
                {
                    continue;
                }

                var answerType = question.AnswerType?.Trim();
                var allowedOptions = GetChoiceOptions(question);

                if (answerType != null && answerType.Equals("Number", StringComparison.OrdinalIgnoreCase))
                {
                    row.AnswerNumber = ScaleNumberForStorage(row.AnswerNumber, row.DecimalPoints);
                    row.AnswerText = null;
                    row.AnswerCurrency = null;
                }
                else if (answerType != null && answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
                {
                    row.AnswerCurrency = ScaleCurrencyForStorage(row.AnswerCurrency, row.DecimalPoints);
                    row.AnswerText = null;
                    row.AnswerNumber = null;
                }
                else if (answerType != null && (
                    answerType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                    || answerType.Equals("Radio", StringComparison.OrdinalIgnoreCase)))
                {
                    row.AnswerText = allowedOptions.Contains(row.AnswerText ?? string.Empty, StringComparer.Ordinal)
                        ? row.AnswerText
                        : null;
                    row.AnswerNumber = null;
                    row.AnswerCurrency = null;
                }
                else if (answerType != null && (answerType.Equals("Multichoice", StringComparison.OrdinalIgnoreCase) || answerType.Equals("MultiChoice", StringComparison.OrdinalIgnoreCase)))
                {
                    var selected = (row.SelectedChoices ?? new List<string>())
                        .Select(x => (x ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Where(x => allowedOptions.Contains(x, StringComparer.Ordinal))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    row.AnswerText = selected.Count == 0 ? null : string.Join("; ", selected);
                    row.AnswerNumber = null;
                    row.AnswerCurrency = null;
                }
            }

            if (await AddRankingDuplicateSelectionErrorsAsync(Rows))
            {
                Rows = await ReloadRowsWithPostedAnswersAsync(company.Id, companySurveyId, survey.FinancialYear, Rows);
                SurveyQuestionGroups = await LoadSurveyQuestionGroupsAsync();
                AvailableGroupImageIds = await GetAvailableGroupImageIdsAsync(Rows, SurveyQuestionGroups);
                return Page();
            }

            var latestAnswerByQuestionId = await _context.Answer
                .Where(a => a.CompanySurveyId == companySurveyId)
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.Id).First())
                .ToDictionaryAsync(a => a.QuestionId, a => a);

            foreach (var row in Rows)
            {
                if (latestAnswerByQuestionId.TryGetValue(row.QuestionId, out var existing))
                {
                    existing.AnswerText = row.AnswerText;
                    existing.AnswerNumber = row.AnswerNumber;
                    existing.AnswerCurrency = row.AnswerCurrency;
                }
                else
                {
                    _context.Answer.Add(new Answer
                    {
                        CompanySurveyId = companySurveyId,
                        QuestionId = row.QuestionId,
                        AnswerText = row.AnswerText,
                        AnswerNumber = row.AnswerNumber,
                        AnswerCurrency = row.AnswerCurrency
                    });
                }
            }

            var isSubmitAction = string.Equals(FormAction, "submit", StringComparison.OrdinalIgnoreCase);

            if (companySurvey != null)
            {
                companySurvey.Saved = true;
                companySurvey.SavedDate = DateTime.Now;
                if (isSubmitAction)
                {
                    companySurvey.Submitted = true;
                    companySurvey.SubmittedDate = DateTime.Now;
                }

                var noteText = isSubmitAction
                    ? "Survey receiver clicked Submit Final on the public survey page."
                    : "Survey receiver clicked Save for Later on the public survey page.";

                _context.CompanySurveyNotes.Add(new CompanySurveyNote
                {
                    CompanySurveyId = companySurvey.Id,
                    NoteDateTime = DateTime.Now,
                    User = "Survey Receiver",
                    Notes = noteText
                });
            }

            await _context.SaveChangesAsync();

            if (isSubmitAction)
            {
                await _taskService.CreateSurveySubmittedTaskAsync(
                    company.Id,
                    company.CompanyName ?? $"Company {company.Id}",
                    survey.FinancialYear,
                    "Survey Receiver");
            }

            Submitted = isSubmitAction;
            Saved = !Submitted;

            Rows = await LoadAnswerRowsAsync(company.Id, companySurveyId, survey.FinancialYear);
            SurveyQuestionGroups = await LoadSurveyQuestionGroupsAsync();
            AvailableGroupImageIds = await GetAvailableGroupImageIdsAsync(Rows, SurveyQuestionGroups);
            return Page();
        }

        private async Task<HashSet<int>> GetAvailableGroupImageIdsAsync(List<AnswerEditRow> rows, IEnumerable<QuestionGroup> groups)
        {
            var rowImageIds = rows
                .SelectMany(r => new[] { r.GroupImageId1, r.GroupImageId2, r.GroupImageId3 })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            var groupImageIds = groups
                .SelectMany(g => new[] { g.ImageId1, g.ImageId2, g.ImageId3 })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            var candidateIds = rowImageIds
                .Concat(groupImageIds)
                .Distinct()
                .ToList();

            if (candidateIds.Count == 0)
            {
                return new HashSet<int>();
            }

            var images = await _context.Image
                .Where(x => candidateIds.Contains(x.Id) && !string.IsNullOrWhiteSpace(x.FilePath))
                .Select(x => new { x.Id, x.FilePath })
                .ToListAsync();

            var availableIds = new HashSet<int>();
            foreach (var image in images)
            {
                if (image.FilePath != null && await _imageStorageService.ExistsAsync(image.FilePath))
                {
                    availableIds.Add(image.Id);
                }
            }

            return availableIds;
        }

        private async Task<List<QuestionGroup>> LoadSurveyQuestionGroupsAsync()
        {
            return await _context.QuestionGroup
                .OrderBy(g => g.OrderNumber ?? int.MaxValue)
                .ThenBy(g => g.Id)
                .ToListAsync();
        }

        private async Task<string?> BuildSurveyHeaderImageUrlAsync(int companyId, string token, Models.Survey survey)
        {
            if (!survey.HeaderImageId.HasValue)
            {
                return null;
            }

            var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == survey.HeaderImageId.Value);
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return null;
            }

            if (!await _imageStorageService.ExistsAsync(image.FilePath))
            {
                return null;
            }

            return Url.Page("./AnswerSurvey", "SurveyHeaderImage", new { id = companyId, token });
        }

        private static string GetContentTypeFromPath(string filePath)
        {
            var contentTypeProvider = new FileExtensionContentTypeProvider();
            var extension = Path.GetExtension(filePath);
            if (!contentTypeProvider.TryGetContentType($"file{extension}", out var contentType))
            {
                return "application/octet-stream";
            }

            return contentType;
        }

        private async Task<Models.Survey?> GetCurrentSurveyAsync()
        {
            return await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<int> EnsureCompanySurveyAsync(int companyId, int surveyId)
        {
            var companySurvey = await _context.CompanySurvey
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SurveyId == surveyId);

            if (companySurvey != null)
            {
                return companySurvey.Id;
            }

            companySurvey = new Models.CompanySurvey
            {
                CompanyId = companyId,
                SurveyId = surveyId,
                Saved = false,
                Submitted = false,
                Requested = false,
                    Locked = false,
                    Estimate = false,
                SavedDate = null,
                SubmittedDate = null,
                RequestedDate = null
            };

            _context.CompanySurvey.Add(companySurvey);
            await _context.SaveChangesAsync();
            return companySurvey.Id;
        }

        private async Task<List<AnswerEditRow>> LoadAnswerRowsAsync(int companyId, int companySurveyId, int currentFinancialYear)
        {
            var questions = await _context.Question
                .Where(q => q.Active != false)
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .ToListAsync();

            var questionIds = questions.Select(q => q.Id).ToList();

            var groupIds = questions
                .Where(q => q.GroupId.HasValue)
                .Select(q => q.GroupId!.Value)
                .Distinct()
                .ToList();

            var groupsById = await _context.QuestionGroup
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id);

            var subgroupAssignments = await (
                from subgroupQuestion in _context.QuestionSubgroupQuestion
                join subgroup in _context.QuestionSubgroup on subgroupQuestion.QuestionSubgroupId equals subgroup.Id
                where questionIds.Contains(subgroupQuestion.QuestionId)
                select new
                {
                    subgroupQuestion.QuestionId,
                    subgroup.Id,
                    subgroup.Title,
                    subgroup.NewHeader,
                    subgroup.QuestionRows,
                    subgroupQuestion.OrderNumber
                })
                .ToListAsync();

            var subgroupByQuestionId = subgroupAssignments
                .GroupBy(x => x.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.OrderNumber ?? int.MaxValue)
                        .ThenBy(x => x.Id)
                        .First());

            var subgroupIdByQuestionId = questions
                .ToDictionary(
                    q => q.Id,
                    q => subgroupByQuestionId.TryGetValue(q.Id, out var subgroup) ? (int?)subgroup.Id : null);

            var previousYearAnswersByQuestionId = await GetPreviousYearAnswersByQuestionIdAsync(
                companyId,
                currentFinancialYear,
                questions,
                subgroupIdByQuestionId);

            var answers = await _context.Answer
                .Where(a => a.CompanySurveyId == companySurveyId)
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            var latestAnswerByQuestionId = answers
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.First());

            var rows = questions
                .Select(question =>
                {
                    latestAnswerByQuestionId.TryGetValue(question.Id, out var answer);
                    previousYearAnswersByQuestionId.TryGetValue(question.Id, out var previousYearAnswer);
                    var displayPreviousYear = question.DisplayPreviousYear == true;
                    var effectivePreviousYearAnswer = displayPreviousYear ? previousYearAnswer : null;
                    groupsById.TryGetValue(question.GroupId ?? 0, out var group);
                    subgroupByQuestionId.TryGetValue(question.Id, out var subgroup);

                    return new AnswerEditRow
                    {
                        QuestionId = question.Id,
                        OrderNumber = question.OrderNumber,
                        GroupId = question.GroupId,
                        GroupTitle = group?.Title,
                        GroupDescription = group?.Description,
                        GroupImageId1 = group?.ImageId1,
                        GroupImageId2 = group?.ImageId2,
                        GroupImageId3 = group?.ImageId3,
                        GroupTableFormat = group?.TableFormat ?? false,
                        GroupNewPage = group?.NewPage ?? false,
                        GroupDisplayTitleDesc = group?.DisplayTitleDesc ?? false,
                        SubgroupId = subgroup?.Id,
                        SubgroupTitle = subgroup?.Title,
                        SubgroupNewHeader = subgroup?.NewHeader,
                        SubgroupQuestionRows = subgroup?.QuestionRows,
                        SubgroupOrderNumber = subgroup?.OrderNumber,
                        QuestionText = question.QuestionText,
                        AnswerType = question.AnswerType,
                        DisplayPreviousYear = displayPreviousYear,
                        DecimalPoints = question.DecimalPoints,
                        ChoiceOptions = GetChoiceOptions(question),
                        SelectedChoices = ParseMultiChoiceAnswer(answer?.AnswerText),
                        PreviousYearValue = FormatAnswerPreview(effectivePreviousYearAnswer, question.AnswerType, question.DecimalPoints),
                        PreviousYearAnswerText = effectivePreviousYearAnswer?.AnswerText,
                        PreviousYearAnswerNumber = ScaleNumberForDisplay(effectivePreviousYearAnswer?.AnswerNumber, question.DecimalPoints),
                        PreviousYearAnswerCurrency = ScaleCurrencyForDisplay(effectivePreviousYearAnswer?.AnswerCurrency, question.DecimalPoints),
                        AnswerText = answer?.AnswerText,
                        AnswerNumber = ScaleNumberForDisplay(answer?.AnswerNumber, question.DecimalPoints),
                        AnswerCurrency = ScaleCurrencyForDisplay(answer?.AnswerCurrency, question.DecimalPoints)
                    };
                })
                .ToList();

            NormalizeRowsForDisplay(rows);
            return rows;
        }

        private async Task<Dictionary<int, Answer>> GetPreviousYearAnswersByQuestionIdAsync(
            int companyId,
            int currentFinancialYear,
            IReadOnlyList<Question> currentQuestions,
            IReadOnlyDictionary<int, int?> subgroupIdByQuestionId)
        {
            if (currentFinancialYear <= 0 || currentQuestions.Count == 0)
            {
                return new Dictionary<int, Answer>();
            }

            var priorAnswerRows = await (
                from answer in _context.Answer
                join companySurvey in _context.CompanySurvey on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join question in _context.Question on answer.QuestionId equals question.Id
                where companySurvey.CompanyId == companyId && survey.FinancialYear < currentFinancialYear
                orderby survey.FinancialYear descending, answer.Id descending
                select new
                {
                    Answer = answer,
                    SurveyFinancialYear = survey.FinancialYear,
                    Question = question
                })
                .ToListAsync();

            if (priorAnswerRows.Count == 0)
            {
                return new Dictionary<int, Answer>();
            }

            var priorQuestionIds = priorAnswerRows
                .Select(x => x.Question.Id)
                .Distinct()
                .ToList();

            var priorSubgroupAssignments = await (
                from subgroupQuestion in _context.QuestionSubgroupQuestion
                join subgroup in _context.QuestionSubgroup on subgroupQuestion.QuestionSubgroupId equals subgroup.Id
                where priorQuestionIds.Contains(subgroupQuestion.QuestionId)
                select new
                {
                    subgroupQuestion.QuestionId,
                    subgroup.Id,
                    subgroupQuestion.OrderNumber
                })
                .ToListAsync();

            var priorSubgroupIdByQuestionId = priorSubgroupAssignments
                .GroupBy(x => x.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => (int?)g.OrderBy(x => x.OrderNumber ?? int.MaxValue)
                        .ThenBy(x => x.Id)
                        .Select(x => x.Id)
                        .FirstOrDefault());

            var descriptorByQuestionId = currentQuestions
                .Select(question => new TemporalAnswerQuestionDescriptor
                {
                    QuestionId = question.Id,
                    BaseKey = BuildTemporalBaseKey(
                        currentFinancialYear,
                        question.GroupId,
                        subgroupIdByQuestionId.TryGetValue(question.Id, out var subgroupId) ? subgroupId : null,
                        question.Title,
                        question.QuestionText,
                        question.ImportColumnName,
                        question.ImportColumnNameAlt),
                    Offset = ResolveHistoricalQuestionOffset(
                        currentFinancialYear,
                        question.Title,
                        question.QuestionText,
                        question.ImportColumnName,
                        question.ImportColumnNameAlt)
                })
                .ToDictionary(x => x.QuestionId);

            var priorAnswersByYearKey = priorAnswerRows
                .Select(x => new
                {
                    x.Answer,
                    x.SurveyFinancialYear,
                    BaseKey = BuildTemporalBaseKey(
                        currentFinancialYear,
                        x.Question.GroupId,
                        priorSubgroupIdByQuestionId.TryGetValue(x.Question.Id, out var subgroupId) ? subgroupId : null,
                        x.Question.Title,
                        x.Question.QuestionText,
                        x.Question.ImportColumnName,
                        x.Question.ImportColumnNameAlt),
                    Offset = ResolveHistoricalQuestionOffset(
                        currentFinancialYear,
                        x.Question.Title,
                        x.Question.QuestionText,
                        x.Question.ImportColumnName,
                        x.Question.ImportColumnNameAlt)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.BaseKey) && x.Offset.HasValue)
                .GroupBy(x => (x.SurveyFinancialYear, BaseKey: x.BaseKey!, Offset: x.Offset!.Value))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Answer.Id)
                        .Select(x => x.Answer)
                        .First());

            var latestPriorAnswerByQuestionId = priorAnswerRows
                .GroupBy(x => x.Answer.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.SurveyFinancialYear)
                        .ThenByDescending(x => x.Answer.Id)
                        .Select(x => x.Answer)
                        .First());

            var resolvedAnswers = new Dictionary<int, Answer>();

            foreach (var question in currentQuestions)
            {
                if (descriptorByQuestionId.TryGetValue(question.Id, out var descriptor)
                    && !string.IsNullOrWhiteSpace(descriptor.BaseKey)
                    && descriptor.Offset.HasValue)
                {
                    var temporalMatch = ResolvePriorYearSourceAnswer(
                        currentFinancialYear,
                        descriptor.BaseKey,
                        descriptor.Offset.Value,
                        priorAnswersByYearKey);

                    if (temporalMatch != null)
                    {
                        resolvedAnswers[question.Id] = temporalMatch;
                        continue;
                    }
                }

                if (latestPriorAnswerByQuestionId.TryGetValue(question.Id, out var latestPriorAnswer)
                    && HasAnswerValue(latestPriorAnswer))
                {
                    resolvedAnswers[question.Id] = latestPriorAnswer;
                }
            }

            return resolvedAnswers;
        }

        private static void NormalizeRowsForDisplay(List<AnswerEditRow> rows)
        {
            foreach (var row in rows)
            {
                var answerType = (row.AnswerType ?? string.Empty).Trim();

                if (answerType.Equals("Number", StringComparison.OrdinalIgnoreCase))
                {
                    row.AnswerText = null;
                    row.AnswerCurrency = null;
                    row.PreviousYearAnswerText = null;
                    row.PreviousYearAnswerCurrency = null;
                }
                else if (answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
                {
                    row.AnswerText = null;
                    row.AnswerNumber = null;
                    row.PreviousYearAnswerText = null;
                    row.PreviousYearAnswerNumber = null;
                }
                else if (answerType.Equals("Multichoice", StringComparison.OrdinalIgnoreCase)
                    || answerType.Equals("MultiChoice", StringComparison.OrdinalIgnoreCase)
                    || answerType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                    || answerType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
                    || answerType.Equals("Text", StringComparison.OrdinalIgnoreCase))
                {
                    row.AnswerNumber = null;
                    row.AnswerCurrency = null;
                    row.PreviousYearAnswerNumber = null;
                    row.PreviousYearAnswerCurrency = null;
                }
            }
        }

        private void ApplyVisibleHistoricalCopyValues(List<AnswerEditRow> rows, int financialYear)
        {
            if (financialYear <= 0 || rows.Count == 0)
            {
                return;
            }

            foreach (var row in rows)
            {
                row.PreviousYearValue = null;
                row.PreviousYearAnswerText = null;
                row.PreviousYearAnswerNumber = null;
                row.PreviousYearAnswerCurrency = null;
            }

            foreach (var targetRow in rows)
            {
                var answerType = (targetRow.AnswerType ?? string.Empty).Trim();
                if (!answerType.Equals("Number", StringComparison.OrdinalIgnoreCase)
                    && !answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (HasCurrentAnswer(targetRow))
                {
                    continue;
                }

                var targetOffset = ResolveHistoricalQuestionOffset(financialYear, targetRow.QuestionText);
                if (targetOffset is not 0 and not 1)
                {
                    continue;
                }

                var sourceOffset = targetOffset.Value + 1;

                var sourceRow = rows
                    .Where(candidate => !ReferenceEquals(candidate, targetRow))
                    .Where(candidate => candidate.GroupId == targetRow.GroupId)
                    .Where(candidate => candidate.SubgroupId == targetRow.SubgroupId)
                    .Where(candidate => ResolveHistoricalQuestionOffset(financialYear, candidate.QuestionText) == sourceOffset)
                    .Where(HasCurrentAnswer)
                    .OrderBy(candidate => candidate.OrderNumber ?? int.MaxValue)
                    .ThenBy(candidate => candidate.QuestionId)
                    .FirstOrDefault();

                if (sourceRow == null)
                {
                    continue;
                }

                targetRow.PreviousYearAnswerText = sourceRow.AnswerText;
                targetRow.PreviousYearAnswerNumber = sourceRow.AnswerNumber;
                targetRow.PreviousYearAnswerCurrency = sourceRow.AnswerCurrency;
                targetRow.PreviousYearValue = FormatAnswerPreview(sourceRow);
            }
        }

        private static string? BuildCurrentSurveyCopyKey(AnswerEditRow row, int financialYear)
        {
            if (row.SubgroupId.HasValue)
            {
                return $"group-{row.GroupId ?? 0}-subgroup-{row.SubgroupId.Value}";
            }

            var baseKey = BuildTemporalBaseKey(
                financialYear,
                row.GroupId,
                row.SubgroupId,
                row.QuestionText,
                row.SubgroupTitle,
                row.GroupTitle);

            if (!string.IsNullOrWhiteSpace(baseKey))
            {
                return $"group-{row.GroupId ?? 0}-{baseKey}";
            }

            return row.GroupId.HasValue
                ? $"group-{row.GroupId.Value}-question-{row.QuestionId}"
                : null;
        }

        private static string? FormatAnswerPreview(AnswerEditRow row)
        {
            var normalizedType = row.AnswerType?.Trim();

            if (normalizedType != null && normalizedType.Equals("Currency", StringComparison.OrdinalIgnoreCase) && row.AnswerCurrency.HasValue)
            {
                return row.AnswerCurrency.Value.ToString($"N{GetDisplayPrecision(row.DecimalPoints)}");
            }

            if (normalizedType != null && normalizedType.Equals("Number", StringComparison.OrdinalIgnoreCase) && row.AnswerNumber.HasValue)
            {
                return row.AnswerNumber.Value.ToString($"N{GetDisplayPrecision(row.DecimalPoints)}");
            }

            if (!string.IsNullOrWhiteSpace(row.AnswerText))
            {
                return row.AnswerText;
            }

            return null;
        }

        private static string? FormatAnswerPreview(Answer? answer, string? answerType, int? decimalPoints)
        {
            if (answer == null)
            {
                return null;
            }

            var normalizedType = answerType?.Trim();

            if (normalizedType != null && normalizedType.Equals("Currency", StringComparison.OrdinalIgnoreCase) && answer.AnswerCurrency.HasValue)
            {
                var displayValue = ScaleCurrencyForDisplay(answer.AnswerCurrency.Value, decimalPoints);
                return displayValue?.ToString($"N{GetDisplayPrecision(decimalPoints)}");
            }

            if (normalizedType != null && normalizedType.Equals("Number", StringComparison.OrdinalIgnoreCase) && answer.AnswerNumber.HasValue)
            {
                var displayValue = ScaleNumberForDisplay(answer.AnswerNumber.Value, decimalPoints);
                return displayValue?.ToString($"N{GetDisplayPrecision(decimalPoints)}");
            }

            if (!string.IsNullOrWhiteSpace(answer.AnswerText))
            {
                return answer.AnswerText;
            }

            if (answer.AnswerCurrency.HasValue)
            {
                var displayValue = ScaleCurrencyForDisplay(answer.AnswerCurrency.Value, decimalPoints);
                return displayValue?.ToString($"N{GetDisplayPrecision(decimalPoints)}");
            }

            if (answer.AnswerNumber.HasValue)
            {
                var displayValue = ScaleNumberForDisplay(answer.AnswerNumber.Value, decimalPoints);
                return displayValue?.ToString($"N{GetDisplayPrecision(decimalPoints)}");
            }

            return null;
        }

        private static double? ScaleNumberForDisplay(double? value, int? decimalPoints)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var scaleFactor = (double)GetScaleFactor(decimalPoints);
            var scaled = value.Value / scaleFactor;
            return Math.Round(scaled, GetDisplayPrecision(decimalPoints), MidpointRounding.AwayFromZero);
        }

        private static decimal? ScaleCurrencyForDisplay(decimal? value, int? decimalPoints)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var scaleFactor = GetScaleFactor(decimalPoints);
            var scaled = value.Value / scaleFactor;
            return Math.Round(scaled, GetDisplayPrecision(decimalPoints), MidpointRounding.AwayFromZero);
        }

        private static double? ScaleNumberForStorage(double? value, int? decimalPoints)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var scaleFactor = (double)GetScaleFactor(decimalPoints);
            return value.Value * scaleFactor;
        }

        private static decimal? ScaleCurrencyForStorage(decimal? value, int? decimalPoints)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var scaleFactor = GetScaleFactor(decimalPoints);
            return value.Value * scaleFactor;
        }

        private static decimal GetScaleFactor(int? decimalPoints)
        {
            if (!decimalPoints.HasValue || decimalPoints.Value >= 0)
            {
                return 1m;
            }

            var factor = 1m;
            for (var i = 0; i < -decimalPoints.Value; i++)
            {
                factor *= 10m;
            }

            return factor;
        }

        private static int GetDisplayPrecision(int? decimalPoints)
        {
            var precision = decimalPoints ?? 2;
            return Math.Max(0, Math.Min(precision, 6));
        }

        private static int GetInputPrecision(int? decimalPoints)
        {
            if (!decimalPoints.HasValue)
            {
                return 2;
            }

            return decimalPoints.Value < 0
                ? 0
                : Math.Max(0, Math.Min(decimalPoints.Value, 6));
        }

        public string FormatNumberInputValue(double? value, int? decimalPoints)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }

            return value.Value.ToString($"F{GetInputPrecision(decimalPoints)}", CultureInfo.InvariantCulture);
        }

        public string FormatCurrencyInputValue(decimal? value, int? decimalPoints)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }

            return value.Value.ToString($"F{GetInputPrecision(decimalPoints)}", CultureInfo.InvariantCulture);
        }

        public string GetNumericInputStep(int? decimalPoints)
        {
            var precision = GetInputPrecision(decimalPoints);
            if (precision <= 0)
            {
                return "1";
            }

            return "0." + new string('0', precision - 1) + "1";
        }

        private async Task<List<AnswerEditRow>> ReloadRowsWithPostedAnswersAsync(
            int companyId,
            int companySurveyId,
            int financialYear,
            IReadOnlyCollection<AnswerEditRow> postedRows)
        {
            var reloadedRows = await LoadAnswerRowsAsync(companyId, companySurveyId, financialYear);
            var postedByQuestionId = postedRows
                .GroupBy(x => x.QuestionId)
                .ToDictionary(g => g.Key, g => g.Last());

            foreach (var row in reloadedRows)
            {
                if (!postedByQuestionId.TryGetValue(row.QuestionId, out var posted))
                {
                    continue;
                }

                row.AnswerText = posted.AnswerText;
                row.AnswerNumber = posted.AnswerNumber;
                row.AnswerCurrency = posted.AnswerCurrency;
                row.SelectedChoices = (posted.SelectedChoices ?? new List<string>())
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            return reloadedRows;
        }

        private async Task<bool> AddRankingDuplicateSelectionErrorsAsync(IReadOnlyCollection<AnswerEditRow> rows)
        {
            var groupIds = rows
                .Where(x => x.GroupId.HasValue)
                .Select(x => x.GroupId!.Value)
                .Distinct()
                .ToList();

            var enforceUniqueGroupIds = groupIds.Count == 0
                ? new HashSet<int>()
                : (await _context.QuestionGroup
                    .Where(x => groupIds.Contains(x.Id) && x.EnforceUniqueSelection)
                    .Select(x => x.Id)
                    .ToListAsync())
                    .ToHashSet();

            var duplicateMessages = rows
                .Where(row => row.GroupId.HasValue && enforceUniqueGroupIds.Contains(row.GroupId.Value))
                .Where(IsRankingSingleChoiceRow)
                .Select(row => new
                {
                    SelectedValue = (row.AnswerText ?? string.Empty).Trim(),
                    GroupKey = BuildRankingGroupKey(row)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.SelectedValue) && !string.IsNullOrWhiteSpace(x.GroupKey))
                .GroupBy(x => x.GroupKey, StringComparer.Ordinal)
                .SelectMany(group => group
                    .GroupBy(x => x.SelectedValue, StringComparer.Ordinal)
                    .Where(choiceGroup => choiceGroup.Count() > 1)
                    .Select(choiceGroup => $"Ranking value '{choiceGroup.Key}' is selected more than once in the same ranking block. Each value can only be used once."))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var message in duplicateMessages)
            {
                ModelState.AddModelError(string.Empty, message);
            }

            return duplicateMessages.Count > 0;
        }

        private static bool IsRankingSingleChoiceRow(AnswerEditRow row)
        {
            var answerType = (row.AnswerType ?? string.Empty).Trim();
            if (!answerType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                && !answerType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryBuildNumericChoiceSignature(row.ChoiceOptions) != null;
        }

        private static string? BuildRankingGroupKey(AnswerEditRow row)
        {
            var signature = TryBuildNumericChoiceSignature(row.ChoiceOptions);
            if (string.IsNullOrWhiteSpace(signature))
            {
                return null;
            }

            var groupKey = row.GroupId.HasValue ? row.GroupId.Value.ToString(CultureInfo.InvariantCulture) : "nogroup";
            var subgroupKey = row.SubgroupId.HasValue ? row.SubgroupId.Value.ToString(CultureInfo.InvariantCulture) : "nosubgroup";
            return $"{groupKey}|{subgroupKey}|{signature}";
        }

        private static string? TryBuildNumericChoiceSignature(IReadOnlyCollection<string>? choiceOptions)
        {
            if (choiceOptions == null)
            {
                return null;
            }

            var numbers = choiceOptions
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? (int?)value
                    : null)
                .ToList();

            if (numbers.Count == 0 || numbers.Any(x => !x.HasValue))
            {
                return null;
            }

            var distinctSorted = numbers
                .Select(x => x!.Value)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (distinctSorted.Count < 3)
            {
                return null;
            }

            for (var index = 0; index < distinctSorted.Count; index++)
            {
                if (distinctSorted[index] != index + 1)
                {
                    return null;
                }
            }

            return string.Join(",", distinctSorted);
        }

        private static List<string> GetChoiceOptions(Question question)
        {
            return new[]
            {
                question.Multi1,
                question.Multi2,
                question.Multi3,
                question.Multi4,
                question.Multi5,
                question.Multi6,
                question.Multi7,
                question.Multi8,
                question.Multi9,
                question.Multi10
            }
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        }

        private static List<string> ParseMultiChoiceAnswer(string? answerText)
        {
            if (string.IsNullOrWhiteSpace(answerText))
            {
                return new List<string>();
            }

            return answerText
                .Split(new[] { ';', ',', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private string GetEffectiveToken(string? candidate)
        {
            var token = candidate;

            if (string.IsNullOrWhiteSpace(token))
            {
                token = Request.Query["token"].FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(token) && RouteData.Values.TryGetValue("token", out var routeToken))
            {
                token = routeToken?.ToString();
            }

            token ??= string.Empty;
            token = Uri.UnescapeDataString(token.Trim());
            token = token.Trim('"', '\'', '<', '>', '(', ')', '[', ']', '{', '}');
            token = token.TrimEnd('.', ',', ';', ':');

            return token;
        }

        private int ResolveCompanyId(int routeId, string? token)
        {
            if (routeId > 0)
            {
                return routeId;
            }

            if (CompanyId > 0)
            {
                return CompanyId;
            }

            var tokenId = TryGetClientIdFromToken(token);
            if (tokenId.HasValue && tokenId.Value > 0)
            {
                return tokenId.Value;
            }

            var referer = Request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                var segments = refererUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    if (segments[i].Equals("AnswerSurvey", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(segments[i + 1], out var parsedId)
                        && parsedId > 0)
                    {
                        return parsedId;
                    }
                }
            }

            return routeId;
        }

        private static int? TryGetClientIdFromToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var base64 = token.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2:
                        base64 += "==";
                        break;
                    case 3:
                        base64 += "=";
                        break;
                }

                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var parts = decoded.Split(':');
                if (parts.Length < 3)
                {
                    return null;
                }

                if (int.TryParse(parts[0], out var clientId) && clientId > 0)
                {
                    return clientId;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private sealed class TemporalAnswerQuestionDescriptor
        {
            public int QuestionId { get; set; }
            public string? BaseKey { get; set; }
            public int? Offset { get; set; }
        }

        public class AnswerEditRow
        {
            public int QuestionId { get; set; }
            public int? OrderNumber { get; set; }
            public int? GroupId { get; set; }
            public string? GroupTitle { get; set; }
            public string? GroupDescription { get; set; }
            public int? GroupImageId1 { get; set; }
            public int? GroupImageId2 { get; set; }
            public int? GroupImageId3 { get; set; }
            public bool GroupTableFormat { get; set; }
            public bool GroupNewPage { get; set; }
            public bool GroupDisplayTitleDesc { get; set; }
            public int? SubgroupId { get; set; }
            public string? SubgroupTitle { get; set; }
            public bool? SubgroupNewHeader { get; set; }
            public int? SubgroupQuestionRows { get; set; }
            public int? SubgroupOrderNumber { get; set; }
            public string? QuestionText { get; set; }
            public string? AnswerType { get; set; }
            public bool DisplayPreviousYear { get; set; }
            public int? DecimalPoints { get; set; }
            public List<string> ChoiceOptions { get; set; } = new();
            public List<string> SelectedChoices { get; set; } = new();
            public string? PreviousYearValue { get; set; }
            public string? PreviousYearAnswerText { get; set; }
            public double? PreviousYearAnswerNumber { get; set; }
            public decimal? PreviousYearAnswerCurrency { get; set; }
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }
    }
}
