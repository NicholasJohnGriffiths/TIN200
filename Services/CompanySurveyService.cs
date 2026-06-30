using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class CompanySurveyService
    {
        private readonly ApplicationDbContext _context;
        private readonly SurveyLinkSettings _surveyLinkSettings;

        public CompanySurveyService(
            ApplicationDbContext context,
            IOptions<SurveyLinkSettings> surveyLinkSettings)
        {
            _context = context;
            _surveyLinkSettings = surveyLinkSettings.Value;
        }

        public async Task<List<CompanySurvey>> GetAllAsync()
        {
            var records = await _context.CompanySurvey
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            foreach (var record in records)
            {
                record.SurveyLink = NormalizeSurveyLink(record.SurveyLink);
            }

            return records;
        }

        public async Task<List<CompanySurveyListRow>> GetListRowsAsync()
        {
            return await GetListRowsAsync(null);
        }

        public async Task<List<CompanySurveyListRow>> GetListRowsAsync(int? financialYear)
        {
            var query =
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id into companyJoin
                from company in companyJoin.DefaultIfEmpty()
                select new CompanySurveyListRow
                {
                    Id = companySurvey.Id,
                    CompanyId = companySurvey.CompanyId,
                    CompanyName = company.CompanyName,
                    LastTIN200Year = company.LastTIN200Year,
                    ContactFirstName = company.ContactFirstName,
                    ContactLastName = company.ContactLastName,
                    ContactEmail = company.ContactEmail,
                    ExternalId = company.ExternalId,
                    TinStatus = company.TinStatus,
                    IsTestCompany = company.TinStatus == (int)TinStatus.TinTest,
                    FinancialYear = survey.FinancialYear,
                    Saved = companySurvey.Saved,
                    Submitted = companySurvey.Submitted,
                    Requested = companySurvey.Requested,
                    Locked = companySurvey.Locked ?? false,
                    Estimate = companySurvey.Estimate ?? false,
                    SurveyEmailSent = companySurvey.SurveyEmailSent ?? false,
                    SurveyEmailSentLastDate = companySurvey.SurveyEmailSentLastDate,
                    SurveyReminderEmailSent = companySurvey.SurveyReminderEmailSent ?? false,
                    SurveyReminderEmailSentLastDate = companySurvey.SurveyReminderEmailSentLastDate,
                    SavedDate = companySurvey.SavedDate,
                    SubmittedDate = companySurvey.SubmittedDate,
                    RequestedDate = companySurvey.RequestedDate,
                    Unsubscribed = companySurvey.Unsubscribed,
                    UnsubscribedDate = companySurvey.UnsubscribedDate,
                    SurveyLink = companySurvey.SurveyLink,
                    AnswerCount = _context.Answer.Count(a =>
                        a.CompanySurveyId == companySurvey.Id &&
                        (a.AnswerText != null || a.AnswerCurrency != null || a.AnswerNumber != null))
                };

            if (financialYear.HasValue)
            {
                query = query.Where(x => x.FinancialYear == financialYear.Value);
            }

            var rows = await query
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            foreach (var row in rows)
            {
                row.SurveyLink = NormalizeSurveyLink(row.SurveyLink);
            }

            return rows;
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            return await _context.Survey
                .Select(s => s.FinancialYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<int?> GetCurrentSurveyFinancialYearAsync()
        {
            return await _context.Survey
                .Where(s => s.CurrentSurvey)
                .Select(s => (int?)s.FinancialYear)
                .OrderByDescending(y => y)
                .FirstOrDefaultAsync();
        }

        public async Task<CompanySurvey?> GetByIdAsync(int id)
        {
            var record = await _context.CompanySurvey.FindAsync(id);
            if (record != null)
            {
                record.SurveyLink = NormalizeSurveyLink(record.SurveyLink);
            }

            return record;
        }

        private string? NormalizeSurveyLink(string? surveyLink)
        {
            if (string.IsNullOrWhiteSpace(surveyLink))
            {
                return surveyLink;
            }

            if (!Uri.TryCreate(surveyLink.Trim(), UriKind.Absolute, out var existingUri))
            {
                return surveyLink;
            }

            if (!IsLocalHost(existingUri.Host))
            {
                return surveyLink;
            }

            var configuredBaseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(configuredBaseUrl)
                || !Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredUri)
                || IsLocalHost(configuredUri.Host))
            {
                return surveyLink;
            }

            return $"{configuredBaseUrl}{existingUri.PathAndQuery}{existingUri.Fragment}";
        }

        private static bool IsLocalHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<CompanySurvey> CreateAsync(CompanySurvey record)
        {
            _context.CompanySurvey.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<CompanySurvey> UpdateAsync(CompanySurvey record)
        {
            _context.CompanySurvey.Update(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task DeleteAsync(int id)
        {
            var record = await GetByIdAsync(id);
            if (record != null)
            {
                _context.CompanySurvey.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.CompanySurvey.AnyAsync(x => x.Id == id);
        }

        public async Task<int> BulkSubmitWithAnswersAsync(int? financialYear)
        {
            var surveyIds = financialYear.HasValue
                ? await _context.Survey
                    .Where(s => s.FinancialYear == financialYear.Value)
                    .Select(s => s.Id)
                    .ToListAsync()
                : null;

            var query = _context.CompanySurvey
                .Where(cs => !cs.Submitted)
                .Where(cs => _context.Answer.Any(a =>
                    a.CompanySurveyId == cs.Id &&
                    (a.AnswerText != null || a.AnswerCurrency != null || a.AnswerNumber != null)));

            if (surveyIds != null)
                query = query.Where(cs => surveyIds.Contains(cs.SurveyId));

            var records = await query.ToListAsync();
            var submittedDate = new DateTime(2025, 12, 1);

            foreach (var r in records)
            {
                r.Submitted = true;
                r.SubmittedDate = submittedDate;
            }

            await _context.SaveChangesAsync();
            return records.Count;
        }

        public async Task<int> SetLockedAsync(IEnumerable<int> companySurveyIds, bool locked)
        {
            var ids = companySurveyIds
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return 0;
            }

            var rows = await _context.CompanySurvey
                .Where(cs => ids.Contains(cs.Id))
                .ToListAsync();

            foreach (var row in rows)
            {
                row.Locked = locked;
            }

            await _context.SaveChangesAsync();
            return rows.Count;
        }

        private const string SurveyContactFirstNameTitle = "Survey Contact First Name";
        private const string SurveyContactLastNameTitle = "Survey Contact Last Name";
        private const string SurveyContactEmailTitle = "Survey Contact Email";

        private static readonly string[] ContactAnswerTitles =
        {
            SurveyContactFirstNameTitle,
            SurveyContactLastNameTitle,
            SurveyContactEmailTitle
        };

        public async Task<CopyContactDetailsResult> CopyContactDetailsFromCompaniesAsync(IEnumerable<int> companySurveyIds)
        {
            var ids = companySurveyIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var result = new CopyContactDetailsResult();
            if (!ids.Any())
            {
                return result;
            }

            var selectedSurveys = await (
                from companySurvey in _context.CompanySurvey
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where ids.Contains(companySurvey.Id)
                select new
                {
                    CompanySurveyId = companySurvey.Id,
                    companySurvey.CompanyId,
                    company.CompanyName,
                    company.ContactFirstName,
                    company.ContactLastName,
                    company.ContactEmail
                })
                .ToListAsync();

            result.SelectedSurveyCount = selectedSurveys.Count;
            if (!selectedSurveys.Any())
            {
                return result;
            }

            var contactQuestionIdsByTitle = (await _context.Question
                    .AsNoTracking()
                    .Select(q => new { q.Id, q.Title })
                    .ToListAsync())
                .Where(q => !string.IsNullOrWhiteSpace(q.Title))
                .Select(q => new { q.Id, Title = q.Title!.Trim() })
                .Where(q => ContactAnswerTitles.Contains(q.Title, StringComparer.OrdinalIgnoreCase))
                .GroupBy(q => q.Title, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(q => q.Id).Distinct().ToList(),
                    StringComparer.OrdinalIgnoreCase);

            result.MissingQuestionTitles = ContactAnswerTitles
                .Where(title => !contactQuestionIdsByTitle.ContainsKey(title))
                .ToList();

            var targetQuestionIds = contactQuestionIdsByTitle
                .SelectMany(pair => pair.Value)
                .Distinct()
                .ToList();

            if (!targetQuestionIds.Any())
            {
                return result;
            }

            var existingAnswers = await _context.Answer
                .Where(a => ids.Contains(a.CompanySurveyId) && targetQuestionIds.Contains(a.QuestionId))
                .ToListAsync();

            var existingAnswerLookup = existingAnswers.ToLookup(a => (a.CompanySurveyId, a.QuestionId));
            var affectedCompanyIds = new HashSet<int>();

            foreach (var selectedSurvey in selectedSurveys)
            {
                var answerValues = new (string Title, string? Value)[]
                {
                    (SurveyContactFirstNameTitle, NullIfWhiteSpace(selectedSurvey.ContactFirstName)),
                    (SurveyContactLastNameTitle, NullIfWhiteSpace(selectedSurvey.ContactLastName)),
                    (SurveyContactEmailTitle, NullIfWhiteSpace(selectedSurvey.ContactEmail))
                };

                foreach (var answerValue in answerValues)
                {
                    if (!contactQuestionIdsByTitle.TryGetValue(answerValue.Title, out var questionIds))
                    {
                        continue;
                    }

                    foreach (var questionId in questionIds)
                    {
                        var matchingAnswers = existingAnswerLookup[(selectedSurvey.CompanySurveyId, questionId)].ToList();

                        if (!matchingAnswers.Any())
                        {
                            if (answerValue.Value == null)
                            {
                                continue;
                            }

                            _context.Answer.Add(new Answer
                            {
                                CompanySurveyId = selectedSurvey.CompanySurveyId,
                                QuestionId = questionId,
                                AnswerText = answerValue.Value,
                                AnswerNumber = null,
                                AnswerCurrency = null
                            });

                            result.InsertedAnswerCount++;
                            affectedCompanyIds.Add(selectedSurvey.CompanyId);
                            continue;
                        }

                        foreach (var answer in matchingAnswers)
                        {
                            var currentValue = NullIfWhiteSpace(answer.AnswerText);
                            if (string.Equals(currentValue, answerValue.Value, StringComparison.Ordinal)
                                && !answer.AnswerNumber.HasValue
                                && !answer.AnswerCurrency.HasValue)
                            {
                                continue;
                            }

                            answer.AnswerText = answerValue.Value;
                            answer.AnswerNumber = null;
                            answer.AnswerCurrency = null;
                            result.UpdatedAnswerCount++;
                            affectedCompanyIds.Add(selectedSurvey.CompanyId);
                        }
                    }
                }
            }

            if (result.UpdatedAnswerCount > 0 || result.InsertedAnswerCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            result.AffectedCompanyCount = affectedCompanyIds.Count;
            return result;
        }

        public async Task<PopulatePriorYearDataResult> PreviewPopulatePriorYearDataAsync(IEnumerable<int> companySurveyIds, int financialYear, int previewLimit = 20)
        {
            return await BuildPopulatePriorYearDataResultAsync(companySurveyIds, financialYear, applyUpdates: false, previewLimit: previewLimit);
        }

        public async Task<PopulatePriorYearDataResult> PopulatePriorYearDataAsync(IEnumerable<int> companySurveyIds, int financialYear, int previewLimit = 20)
        {
            return await BuildPopulatePriorYearDataResultAsync(companySurveyIds, financialYear, applyUpdates: true, previewLimit: previewLimit);
        }

        private async Task<PopulatePriorYearDataResult> BuildPopulatePriorYearDataResultAsync(IEnumerable<int> companySurveyIds, int financialYear, bool applyUpdates, int previewLimit)
        {
            var ids = companySurveyIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var result = new PopulatePriorYearDataResult
            {
                FinancialYear = financialYear,
                YearMinusOne = financialYear - 1,
                YearMinusTwo = financialYear - 2,
                PreviewRows = new List<PopulatePriorYearDataPreviewRow>()
            };

            if (!ids.Any() || financialYear <= 0)
            {
                return result;
            }

            var allQuestions = await _context.Question
                .AsNoTracking()
                .Where(q => q.Active != false)
                .Select(q => new
                {
                    q.Id,
                    q.GroupId,
                    q.Title,
                    q.QuestionText,
                    q.ImportColumnName,
                    q.ImportColumnNameAlt,
                    q.OrderNumber
                })
                .ToListAsync();

            var subgroupByQuestionId = await _context.QuestionSubgroupQuestion
                .AsNoTracking()
                .GroupBy(x => x.QuestionId)
                .Select(g => new
                {
                    QuestionId = g.Key,
                    QuestionSubgroupId = g
                        .OrderBy(x => x.OrderNumber ?? int.MaxValue)
                        .ThenBy(x => x.Id)
                        .Select(x => (int?)x.QuestionSubgroupId)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.QuestionId, x => x.QuestionSubgroupId);

            var temporalQuestions = allQuestions
                .Select(q => new TemporalQuestionDescriptor
                {
                    QuestionId = q.Id,
                    BaseKey = BuildTemporalBaseKey(
                        financialYear,
                        q.GroupId,
                        subgroupByQuestionId.TryGetValue(q.Id, out var subgroupId) ? subgroupId : null,
                        q.Title,
                        q.QuestionText,
                        q.ImportColumnName,
                        q.ImportColumnNameAlt),
                    Offset = ResolveFinancialYearOffset(financialYear, q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt),
                    Label = FirstNonBlank(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt) ?? $"Question {q.Id}",
                    OrderNumber = q.OrderNumber ?? int.MaxValue
                })
                .Where(q => q.Offset.HasValue && !string.IsNullOrWhiteSpace(q.BaseKey))
                .ToList();

            var questionById = temporalQuestions
                .GroupBy(q => q.QuestionId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.OrderNumber).First());

            var targetQuestions = questionById.Values
                .Where(q => q.Offset is 1 or 2)
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.QuestionId)
                .ToList();

            if (targetQuestions.Count == 0)
            {
                return result;
            }

            var selectedCompanySurveys = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id into companyJoin
                from company in companyJoin.DefaultIfEmpty()
                where ids.Contains(companySurvey.Id) && survey.FinancialYear == financialYear
                select new
                {
                    companySurvey.Id,
                    companySurvey.CompanyId,
                    company.CompanyName
                })
                .ToListAsync();

            result.TotalRecords = selectedCompanySurveys.Count;
            if (selectedCompanySurveys.Count == 0)
            {
                return result;
            }

            var selectedCompanySurveyIds = selectedCompanySurveys.Select(x => x.Id).ToList();
            var companyIds = selectedCompanySurveys.Select(x => x.CompanyId).Distinct().ToList();
            var temporalQuestionIds = questionById.Keys.ToList();
            var targetQuestionIds = targetQuestions.Select(q => q.QuestionId).Distinct().ToList();

            var existingTargetAnswers = await _context.Answer
                .Where(a => selectedCompanySurveyIds.Contains(a.CompanySurveyId) && targetQuestionIds.Contains(a.QuestionId))
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            var latestTargetAnswerByCompanySurveyAndQuestion = existingTargetAnswers
                .GroupBy(a => (a.CompanySurveyId, a.QuestionId))
                .ToDictionary(g => g.Key, g => g.First());

            var priorAnswerRows = await (
                from answer in _context.Answer.AsNoTracking()
                join companySurvey in _context.CompanySurvey.AsNoTracking() on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                where companyIds.Contains(companySurvey.CompanyId)
                    && survey.FinancialYear < financialYear
                    && temporalQuestionIds.Contains(answer.QuestionId)
                select new
                {
                    companySurvey.CompanyId,
                    SurveyFinancialYear = survey.FinancialYear,
                    answer.QuestionId,
                    answer.Id,
                    answer.AnswerText,
                    answer.AnswerNumber,
                    answer.AnswerCurrency
                })
                .ToListAsync();

            var priorAnswersByCompanyYearKey = priorAnswerRows
                .Where(x => questionById.ContainsKey(x.QuestionId))
                .Select(x => new
                {
                    x.CompanyId,
                    x.SurveyFinancialYear,
                    Descriptor = questionById[x.QuestionId],
                    Snapshot = new AnswerValueSnapshot
                    {
                        AnswerId = x.Id,
                        AnswerText = x.AnswerText,
                        AnswerNumber = x.AnswerNumber,
                        AnswerCurrency = x.AnswerCurrency
                    }
                })
                .Where(x => HasAnswerValue(x.Snapshot))
                .GroupBy(x => (x.CompanyId, x.SurveyFinancialYear, x.Descriptor.BaseKey!, Offset: x.Descriptor.Offset!.Value))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Snapshot.AnswerId).First().Snapshot);

            var affectedCompanyCount = 0;
            var updatedFieldCount = 0;
            var existingValueCount = 0;
            var missingSourceCount = 0;
            var previewRows = new List<PopulatePriorYearDataPreviewRow>();

            foreach (var companySurvey in selectedCompanySurveys
                .OrderBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.CompanyId))
            {
                var companyName = string.IsNullOrWhiteSpace(companySurvey.CompanyName)
                    ? $"Company {companySurvey.CompanyId}"
                    : companySurvey.CompanyName.Trim();

                var companyWillUpdate = 0;
                var companyExisting = 0;
                var companyMissing = 0;
                var updatedLabels = new List<string>();

                foreach (var targetQuestion in targetQuestions)
                {
                    latestTargetAnswerByCompanySurveyAndQuestion.TryGetValue((companySurvey.Id, targetQuestion.QuestionId), out var existingAnswer);

                    if (HasAnswerValue(existingAnswer))
                    {
                        existingValueCount++;
                        companyExisting++;
                        continue;
                    }

                    var source = ResolveSourceAnswer(
                        companySurvey.CompanyId,
                        financialYear,
                        targetQuestion.BaseKey!,
                        targetQuestion.Offset!.Value,
                        priorAnswersByCompanyYearKey);

                    if (source == null)
                    {
                        missingSourceCount++;
                        companyMissing++;
                        continue;
                    }

                    companyWillUpdate++;
                    updatedFieldCount++;
                    updatedLabels.Add(targetQuestion.Label);

                    if (applyUpdates)
                    {
                        if (existingAnswer != null)
                        {
                            existingAnswer.AnswerText = source.AnswerText;
                            existingAnswer.AnswerNumber = source.AnswerNumber;
                            existingAnswer.AnswerCurrency = source.AnswerCurrency;
                        }
                        else
                        {
                            _context.Answer.Add(new Answer
                            {
                                CompanySurveyId = companySurvey.Id,
                                QuestionId = targetQuestion.QuestionId,
                                AnswerText = source.AnswerText,
                                AnswerNumber = source.AnswerNumber,
                                AnswerCurrency = source.AnswerCurrency
                            });
                        }
                    }
                }

                if (companyWillUpdate > 0)
                {
                    affectedCompanyCount++;
                }

                if (companyWillUpdate > 0 || companyExisting > 0 || companyMissing > 0)
                {
                    var distinctLabels = updatedLabels
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.Ordinal)
                        .Take(3)
                        .ToList();

                    var fieldsSummary = distinctLabels.Count == 0
                        ? string.Empty
                        : string.Join(", ", distinctLabels) + (updatedLabels.Count > distinctLabels.Count ? ", ..." : string.Empty);

                    previewRows.Add(new PopulatePriorYearDataPreviewRow
                    {
                        CompanyName = companyName,
                        WillUpdateCount = companyWillUpdate,
                        ExistingValueCount = companyExisting,
                        MissingSourceCount = companyMissing,
                        FieldsSummary = fieldsSummary
                    });
                }
            }

            if (applyUpdates && updatedFieldCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            result.AffectedCompanyCount = affectedCompanyCount;
            result.UpdatedFieldCount = updatedFieldCount;
            result.ExistingValueCount = existingValueCount;
            result.MissingSourceCount = missingSourceCount;
            result.PreviewRows = previewRows
                .Take(Math.Max(1, previewLimit))
                .ToList();

            return result;
        }

        private static AnswerValueSnapshot? ResolveSourceAnswer(
            int companyId,
            int selectedFinancialYear,
            string baseKey,
            int targetOffset,
            IReadOnlyDictionary<(int CompanyId, int SurveyFinancialYear, string BaseKey, int Offset), AnswerValueSnapshot> priorAnswersByCompanyYearKey)
        {
            if (targetOffset == 0)
            {
                if (priorAnswersByCompanyYearKey.TryGetValue((companyId, selectedFinancialYear - 1, baseKey, 0), out var lastFinancialYearSource)
                    && HasAnswerValue(lastFinancialYearSource))
                {
                    return lastFinancialYearSource;
                }

                return null;
            }

            for (var step = 1; step <= targetOffset; step++)
            {
                var sourceSurveyYear = selectedFinancialYear - step;
                var sourceOffset = targetOffset - step;

                if (sourceOffset < 0)
                {
                    continue;
                }

                if (priorAnswersByCompanyYearKey.TryGetValue((companyId, sourceSurveyYear, baseKey, sourceOffset), out var source)
                    && HasAnswerValue(source))
                {
                    return source;
                }
            }

            return null;
        }

        private static bool HasAnswerValue(Answer? answer)
        {
            return answer != null && HasAnswerValue(new AnswerValueSnapshot
            {
                AnswerText = answer.AnswerText,
                AnswerNumber = answer.AnswerNumber,
                AnswerCurrency = answer.AnswerCurrency
            });
        }

        private static bool HasAnswerValue(AnswerValueSnapshot? answer)
        {
            return answer != null
                && (!string.IsNullOrWhiteSpace(answer.AnswerText)
                    || answer.AnswerNumber.HasValue
                    || answer.AnswerCurrency.HasValue);
        }

        private static int? ResolveFinancialYearOffset(int selectedFinancialYear, params string?[] candidates)
        {
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

                    var offset = (selectedFinancialYear - 1) - year;
                    if (offset >= 0 && offset <= 2)
                    {
                        return offset;
                    }
                }
            }

            return null;
        }

        private static string? BuildTemporalBaseKey(int selectedFinancialYear, int? groupId, int? subgroupId, params string?[] candidates)
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
                    (selectedFinancialYear - 1).ToString(CultureInfo.InvariantCulture),
                    (selectedFinancialYear - 2).ToString(CultureInfo.InvariantCulture),
                    (selectedFinancialYear - 3).ToString(CultureInfo.InvariantCulture)
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

        private static string? FirstNonBlank(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed class TemporalQuestionDescriptor
        {
            public int QuestionId { get; set; }
            public string? BaseKey { get; set; }
            public int? Offset { get; set; }
            public string Label { get; set; } = string.Empty;
            public int OrderNumber { get; set; }
        }

        private sealed class AnswerValueSnapshot
        {
            public int AnswerId { get; set; }
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        public class CompanySurveyListRow
        {
            public int Id { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int? LastTIN200Year { get; set; }
            public string? ContactFirstName { get; set; }
            public string? ContactLastName { get; set; }
            public string? ContactEmail { get; set; }
            public string? ExternalId { get; set; }
            public int? TinStatus { get; set; }
            public bool IsTestCompany { get; set; }
            public int FinancialYear { get; set; }
            public bool Saved { get; set; }
            public bool Submitted { get; set; }
            public bool Requested { get; set; }
            public bool Locked { get; set; }
            public bool Estimate { get; set; }
            public bool SurveyEmailSent { get; set; }
            public DateTime? SurveyEmailSentLastDate { get; set; }
            public bool SurveyReminderEmailSent { get; set; }
            public DateTime? SurveyReminderEmailSentLastDate { get; set; }
            public DateTime? SavedDate { get; set; }
            public DateTime? SubmittedDate { get; set; }
            public DateTime? RequestedDate { get; set; }
            public bool? Unsubscribed { get; set; }
            public DateTime? UnsubscribedDate { get; set; }
            public string? SurveyLink { get; set; }
            public int AnswerCount { get; set; }
        }

        public class PopulatePriorYearDataResult
        {
            public int FinancialYear { get; set; }
            public int YearMinusOne { get; set; }
            public int YearMinusTwo { get; set; }
            public int TotalRecords { get; set; }
            public int AffectedCompanyCount { get; set; }
            public int UpdatedFieldCount { get; set; }
            public int ExistingValueCount { get; set; }
            public int MissingSourceCount { get; set; }
            public List<PopulatePriorYearDataPreviewRow> PreviewRows { get; set; } = new();
        }

        public class PopulatePriorYearDataPreviewRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public int WillUpdateCount { get; set; }
            public int ExistingValueCount { get; set; }
            public int MissingSourceCount { get; set; }
            public string FieldsSummary { get; set; } = string.Empty;
        }

        public class CopyContactDetailsResult
        {
            public int SelectedSurveyCount { get; set; }
            public int AffectedCompanyCount { get; set; }
            public int UpdatedAnswerCount { get; set; }
            public int InsertedAnswerCount { get; set; }
            public List<string> MissingQuestionTitles { get; set; } = new();
        }
    }
}
