using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class EditableSurveyPdfService
    {
        private const double Margin = 42;
        private const double FieldHeight = 24;
        private const double MultilineFieldHeight = 54;
        private readonly ApplicationDbContext _context;
        private readonly IImageStorageService _imageStorageService;

        public EditableSurveyPdfService(ApplicationDbContext context, IImageStorageService imageStorageService)
        {
            _context = context;
            _imageStorageService = imageStorageService;
        }

        public async Task<EditableSurveyPdf?> GenerateAsync(int companySurveyId)
        {
            var surveyRecord = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
                where companySurvey.Id == companySurveyId
                select new
                {
                    CompanySurvey = companySurvey,
                    Survey = survey,
                    CompanyName = company.CompanyName,
                    ExternalId = company.ExternalId
                })
                .FirstOrDefaultAsync();

            if (surveyRecord == null)
            {
                return null;
            }

            var questions = await _context.Question
                .AsNoTracking()
                .Where(question => question.Active != false)
                .OrderBy(question => question.OrderNumber)
                .ThenBy(question => question.Id)
                .ToListAsync();

            var groups = await _context.QuestionGroup
                .AsNoTracking()
                .OrderBy(group => group.OrderNumber ?? int.MaxValue)
                .ThenBy(group => group.Id)
                .ToListAsync();

            var questionIds = questions.Select(question => question.Id).ToList();
            var subgroupAssignments = await (
                from assignment in _context.QuestionSubgroupQuestion.AsNoTracking()
                join subgroup in _context.QuestionSubgroup.AsNoTracking() on assignment.QuestionSubgroupId equals subgroup.Id
                where questionIds.Contains(assignment.QuestionId)
                select new PdfSubgroupAssignment(
                    assignment.QuestionId,
                    subgroup.Id,
                    subgroup.Title,
                    subgroup.NewHeader == true,
                    subgroup.QuestionRows ?? 1,
                    assignment.OrderNumber ?? int.MaxValue))
                .ToListAsync();
            var subgroupByQuestionId = subgroupAssignments
                .GroupBy(assignment => assignment.QuestionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(assignment => assignment.OrderNumber).ThenBy(assignment => assignment.SubgroupId).First());

            var answers = await _context.Answer
                .AsNoTracking()
                .Where(answer => answer.CompanySurveyId == companySurveyId)
                .OrderByDescending(answer => answer.Id)
                .ToListAsync();
            var answersByQuestion = answers
                .GroupBy(answer => answer.QuestionId)
                .ToDictionary(group => group.Key, group => group.First());

            var rows = questions.Select(question =>
            {
                answersByQuestion.TryGetValue(question.Id, out var answer);
                subgroupByQuestionId.TryGetValue(question.Id, out var subgroup);
                return new PdfQuestionRow(question, answer, subgroup);
            }).ToList();

            var sections = groups
                .Select(group => new PdfSurveySection(group, rows.Where(row => row.Question.GroupId == group.Id).ToList()))
                .Where(section => section.Rows.Count > 0)
                .ToList();
            var ungroupedRows = rows.Where(row => !row.Question.GroupId.HasValue).ToList();
            if (ungroupedRows.Count > 0)
            {
                sections.Add(new PdfSurveySection(null, ungroupedRows));
            }

            var imageIds = groups
                .SelectMany(group => new[] { group.ImageId1, group.ImageId2, group.ImageId3 })
                .Append(surveyRecord.Survey.HeaderImageId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var imagePaths = await _context.Image
                .AsNoTracking()
                .Where(image => imageIds.Contains(image.Id) && !string.IsNullOrWhiteSpace(image.FilePath))
                .ToDictionaryAsync(image => image.Id, image => image.FilePath);
            var imageBytes = new Dictionary<int, byte[]>();
            foreach (var imagePath in imagePaths)
            {
                await using var imageStream = await _imageStorageService.OpenReadAsync(imagePath.Value);
                if (imageStream == null)
                {
                    continue;
                }

                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                imageBytes[imagePath.Key] = memoryStream.ToArray();
            }

            using var document = new PdfDocument();
            document.Info.Title = $"{surveyRecord.CompanyName} - {surveyRecord.Survey.FinancialYear} Survey";
            document.Info.Subject = "Editable company survey";

            var titleFont = new XFont("Arial", 17, XFontStyle.Bold);
            var headingFont = new XFont("Arial", 12, XFontStyle.Bold);
            var subgroupFont = new XFont("Arial", 10, XFontStyle.Bold);
            var labelFont = new XFont("Arial", 9.5, XFontStyle.Regular);
            var labelBoldFont = new XFont("Arial", 9.5, XFontStyle.Bold);
            var smallFont = new XFont("Arial", 8, XFontStyle.Regular);
            var italicFont = new XFont("Arial", 9, XFontStyle.Italic);
            var fields = new PdfArray(document);
            PdfPage? page = null;
            XGraphics? graphics = null;
            var cursorY = 0d;
            var contentWidth = 0d;

            var textColor = XColor.FromArgb(33, 37, 41);
            var mutedColor = XColor.FromArgb(108, 117, 125);
            var borderColor = XColor.FromArgb(222, 226, 230);
            var lightColor = XColor.FromArgb(248, 249, 250);
            var headerColor = XColor.FromArgb(233, 236, 239);
            var textBrush = new XSolidBrush(textColor);
            var mutedBrush = new XSolidBrush(mutedColor);
            var borderPen = new XPen(borderColor, 0.8);

            void StartPage()
            {
                graphics?.Dispose();
                page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                graphics = XGraphics.FromPdfPage(page);
                cursorY = Margin;
                contentWidth = page.Width.Point - (Margin * 2);

                if (document.PageCount > 1)
                {
                    graphics.DrawString($"{surveyRecord.CompanyName} - Survey continued", smallFont, mutedBrush, new XPoint(Margin, cursorY + smallFont.Size));
                    cursorY += 18;
                }
            }

            void EnsureSpace(double requiredHeight)
            {
                if (cursorY + requiredHeight > page!.Height.Point - Margin)
                {
                    StartPage();
                }
            }

            void DrawWrapped(string text, XFont font, XBrush brush, double x, double width, double lineHeight)
            {
                foreach (var line in WrapText(graphics!, text, font, width))
                {
                    graphics!.DrawString(line, font, brush, new XPoint(x, cursorY + font.Size));
                    cursorY += lineHeight;
                }
            }

            void DrawImage(int imageId, double maxWidth, double maxHeight)
            {
                if (!imageBytes.TryGetValue(imageId, out var bytes) || bytes.Length == 0)
                {
                    return;
                }

                try
                {
                    using var image = XImage.FromStream(() => new MemoryStream(bytes));
                    var scale = Math.Min(maxWidth / image.PointWidth, maxHeight / image.PointHeight);
                    scale = Math.Min(scale, 1d);
                    var width = image.PointWidth * scale;
                    var height = image.PointHeight * scale;
                    EnsureSpace(height + 10);
                    graphics!.DrawImage(image, Margin, cursorY, width, height);
                    cursorY += height + 10;
                }
                catch
                {
                    // An optional image should not prevent the survey PDF from opening.
                }
            }

            StartPage();
            if (surveyRecord.Survey.HeaderImageId.HasValue)
            {
                DrawImage(surveyRecord.Survey.HeaderImageId.Value, contentWidth, 105);
            }

            var surveyTitle = ResolveFinancialYearText(
                string.IsNullOrWhiteSpace(surveyRecord.Survey.Title) ? "Survey Answers" : surveyRecord.Survey.Title,
                surveyRecord.Survey.FinancialYear);
            DrawWrapped(CleanText(surveyTitle), titleFont, textBrush, Margin, contentWidth, 21);
            cursorY += 5;
            var surveyDescription = ResolveFinancialYearText(
                string.IsNullOrWhiteSpace(surveyRecord.Survey.Description)
                    ? "Please complete the survey answers for the current survey year."
                    : surveyRecord.Survey.Description,
                surveyRecord.Survey.FinancialYear);
            DrawWrapped(CleanText(surveyDescription), italicFont, mutedBrush, Margin, contentWidth, 12);
            cursorY += 7;
            graphics!.DrawLine(borderPen, Margin, cursorY, Margin + contentWidth, cursorY);
            cursorY += 14;

            const double companyRowHeight = 36;
            var companyWidth = contentWidth * 0.68;
            graphics.DrawRectangle(new XSolidBrush(lightColor), Margin, cursorY, contentWidth, companyRowHeight);
            graphics.DrawRectangle(borderPen, Margin, cursorY, contentWidth, companyRowHeight);
            graphics.DrawLine(borderPen, Margin + companyWidth, cursorY, Margin + companyWidth, cursorY + companyRowHeight);
            graphics.DrawString("Company Name", smallFont, mutedBrush, new XPoint(Margin + 7, cursorY + 11));
            graphics.DrawString(surveyRecord.CompanyName ?? string.Empty, labelBoldFont, textBrush, new XRect(Margin + 7, cursorY + 16, companyWidth - 14, 16), XStringFormats.TopLeft);
            graphics.DrawString("Financial Year", smallFont, mutedBrush, new XPoint(Margin + companyWidth + 7, cursorY + 11));
            graphics.DrawString(surveyRecord.Survey.FinancialYear.ToString(CultureInfo.InvariantCulture), labelBoldFont, textBrush, new XPoint(Margin + companyWidth + 7, cursorY + 28));
            cursorY += companyRowHeight + 18;

            foreach (var section in sections)
            {
                var group = section.Group;
                if (group?.DisplayTitleDesc == true)
                {
                    var groupTitle = CleanText(ResolveFinancialYearText(group.Title, surveyRecord.Survey.FinancialYear));
                    var groupDescription = CleanText(ResolveFinancialYearText(group.Description, surveyRecord.Survey.FinancialYear));
                    var titleLines = string.IsNullOrWhiteSpace(groupTitle) ? new List<string>() : WrapText(graphics, groupTitle, headingFont, contentWidth - 20);
                    var descriptionLines = string.IsNullOrWhiteSpace(groupDescription) ? new List<string>() : WrapText(graphics, groupDescription, smallFont, contentWidth - 20);
                    var cardHeight = 14 + (titleLines.Count * 15) + (descriptionLines.Count * 11) + 8;
                    EnsureSpace(cardHeight + 8);
                    graphics.DrawRectangle(new XSolidBrush(lightColor), Margin, cursorY, contentWidth, cardHeight);
                    graphics.DrawRectangle(borderPen, Margin, cursorY, contentWidth, cardHeight);
                    cursorY += 8;
                    foreach (var line in titleLines)
                    {
                        graphics.DrawString(line, headingFont, textBrush, new XPoint(Margin + 10, cursorY + headingFont.Size));
                        cursorY += 15;
                    }
                    foreach (var line in descriptionLines)
                    {
                        graphics.DrawString(line, smallFont, mutedBrush, new XPoint(Margin + 10, cursorY + smallFont.Size));
                        cursorY += 11;
                    }
                    cursorY += 14;
                }

                if (group != null)
                {
                    foreach (var imageId in new[] { group.ImageId1, group.ImageId2, group.ImageId3 }.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
                    {
                        DrawImage(imageId, contentWidth, 180);
                    }
                }

                var subgroupRows = group?.TableFormat == true
                    ? section.Rows.Where(row => row.Subgroup != null).ToList()
                    : new List<PdfQuestionRow>();

                if (subgroupRows.Count > 0)
                {
                    var subgroupGroups = subgroupRows
                        .GroupBy(row => row.Subgroup!.SubgroupId)
                        .Select(rowsInSubgroup => new
                        {
                            Subgroup = rowsInSubgroup.First().Subgroup!,
                            Rows = rowsInSubgroup
                                .OrderBy(row => row.Subgroup!.OrderNumber)
                                .ThenBy(row => row.Question.OrderNumber ?? int.MaxValue)
                                .ThenBy(row => row.Question.Id)
                                .ToList()
                        })
                        .OrderBy(subgroup => subgroup.Rows.Min(row => row.Subgroup!.OrderNumber))
                        .ThenBy(subgroup => subgroup.Subgroup.SubgroupId)
                        .ToList();

                    var headerIndexes = subgroupGroups
                        .Select((subgroup, index) => new { subgroup.Subgroup.NewHeader, Index = index })
                        .Where(item => item.NewHeader)
                        .Select(item => item.Index)
                        .ToList();
                    if (headerIndexes.Count == 0)
                    {
                        headerIndexes.Add(0);
                    }

                    for (var blockIndex = 0; blockIndex < headerIndexes.Count; blockIndex++)
                    {
                        var startIndex = headerIndexes[blockIndex];
                        var endIndex = blockIndex + 1 < headerIndexes.Count ? headerIndexes[blockIndex + 1] : subgroupGroups.Count;
                        var tableSubgroups = subgroupGroups.Skip(startIndex).Take(endIndex - startIndex).ToList();
                        if (tableSubgroups.Count == 0)
                        {
                            continue;
                        }

                        var headerRows = tableSubgroups[0].Rows;
                        var showItemColumn = tableSubgroups.Count > 1;
                        var itemWidth = showItemColumn ? contentWidth * 0.28 : 0d;
                        var dataColumnWidth = (contentWidth - itemWidth) / Math.Max(1, headerRows.Count);
                        const double matrixHeaderHeight = 31;

                        void DrawMatrixHeader()
                        {
                            var x = Margin;
                            if (showItemColumn)
                            {
                                graphics!.DrawRectangle(new XSolidBrush(headerColor), x, cursorY, itemWidth, matrixHeaderHeight);
                                graphics.DrawRectangle(borderPen, x, cursorY, itemWidth, matrixHeaderHeight);
                                graphics.DrawString("Item", labelBoldFont, textBrush, new XRect(x + 7, cursorY, itemWidth - 14, matrixHeaderHeight), XStringFormats.CenterLeft);
                                x += itemWidth;
                            }

                            foreach (var headerRow in headerRows)
                            {
                                var headerText = CleanText(ResolveFinancialYearText(
                                    !string.IsNullOrWhiteSpace(headerRow.Question.QuestionText)
                                        ? headerRow.Question.QuestionText
                                        : headerRow.Question.Title,
                                    surveyRecord.Survey.FinancialYear));
                                graphics!.DrawRectangle(new XSolidBrush(headerColor), x, cursorY, dataColumnWidth, matrixHeaderHeight);
                                graphics.DrawRectangle(borderPen, x, cursorY, dataColumnWidth, matrixHeaderHeight);
                                graphics.DrawString(headerText, labelBoldFont, textBrush, new XRect(x + 4, cursorY, dataColumnWidth - 8, matrixHeaderHeight), XStringFormats.Center);
                                x += dataColumnWidth;
                            }

                            cursorY += matrixHeaderHeight;
                        }

                        EnsureSpace(matrixHeaderHeight + 34);
                        DrawMatrixHeader();

                        foreach (var tableSubgroup in tableSubgroups)
                        {
                            var rowHeight = Math.Max(34d, tableSubgroup.Subgroup.QuestionRows * 30d);
                            if (cursorY + rowHeight > page!.Height.Point - Margin)
                            {
                                StartPage();
                                DrawMatrixHeader();
                            }

                            var x = Margin;
                            if (showItemColumn)
                            {
                                var itemTitle = CleanText(ResolveFinancialYearText(tableSubgroup.Subgroup.Title, surveyRecord.Survey.FinancialYear));
                                graphics!.DrawRectangle(new XSolidBrush(lightColor), x, cursorY, itemWidth, rowHeight);
                                graphics.DrawRectangle(borderPen, x, cursorY, itemWidth, rowHeight);
                                graphics.DrawString(itemTitle, labelBoldFont, textBrush, new XRect(x + 7, cursorY, itemWidth - 14, rowHeight), XStringFormats.CenterLeft);
                                x += itemWidth;
                            }

                            for (var columnIndex = 0; columnIndex < headerRows.Count; columnIndex++)
                            {
                                graphics!.DrawRectangle(borderPen, x, cursorY, dataColumnWidth, rowHeight);
                                if (columnIndex < tableSubgroup.Rows.Count)
                                {
                                    var matrixRow = tableSubgroup.Rows[columnIndex];
                                    var answerValue = FormatAnswer(matrixRow.Answer, matrixRow.Question);
                                    var fieldX = x + 5;
                                    var fieldY = cursorY + 5;
                                    var fieldWidth = dataColumnWidth - 10;
                                    var fieldHeight = rowHeight - 10;
                                    graphics.DrawRectangle(XPens.Gray, XBrushes.White, fieldX, fieldY, fieldWidth, fieldHeight);
                                    AddTextField(document, page!, fields, $"answer_{matrixRow.Question.Id}", answerValue, fieldX, fieldY, fieldWidth, fieldHeight, IsMultiline(matrixRow.Question.AnswerType));
                                }
                                x += dataColumnWidth;
                            }

                            cursorY += rowHeight;
                        }

                        cursorY += 12;
                    }
                }

                var rowsForIndividualRendering = subgroupRows.Count > 0
                    ? section.Rows.Where(row => row.Subgroup == null)
                    : section.Rows;
                var orderedRows = rowsForIndividualRendering
                    .OrderBy(row => row.Question.OrderNumber ?? int.MaxValue)
                    .ThenBy(row => row.Question.Id);
                foreach (var row in orderedRows)
                {
                    var question = row.Question;
                    var questionText = CleanText(ResolveFinancialYearText(
                        !string.IsNullOrWhiteSpace(question.QuestionText) ? question.QuestionText : question.Title,
                        surveyRecord.Survey.FinancialYear));
                    var choiceOptions = GetChoiceOptions(question)
                        .Select(option => ResolveFinancialYearText(option, surveyRecord.Survey.FinancialYear))
                        .ToList();
                    var answerValue = FormatAnswer(row.Answer, question);

                    if (group?.TableFormat == true)
                    {
                        var labelWidth = contentWidth * 0.46;
                        var answerWidth = contentWidth - labelWidth;
                        var questionLines = WrapText(graphics, questionText, labelBoldFont, labelWidth - 14);
                        var optionLines = choiceOptions.Count == 0
                            ? new List<string>()
                            : WrapText(graphics, $"Options: {string.Join(" | ", choiceOptions)}", smallFont, answerWidth - 14);
                        var fieldHeight = IsMultiline(question.AnswerType) ? 40 : FieldHeight;
                        var rowHeight = Math.Max((questionLines.Count * 12) + 14, (optionLines.Count * 10) + fieldHeight + 13);
                        EnsureSpace(rowHeight);
                        graphics.DrawRectangle(new XSolidBrush(lightColor), Margin, cursorY, labelWidth, rowHeight);
                        graphics.DrawRectangle(borderPen, Margin, cursorY, contentWidth, rowHeight);
                        graphics.DrawLine(borderPen, Margin + labelWidth, cursorY, Margin + labelWidth, cursorY + rowHeight);

                        var textY = cursorY + 7;
                        foreach (var line in questionLines)
                        {
                            graphics.DrawString(line, labelBoldFont, textBrush, new XPoint(Margin + 7, textY + labelBoldFont.Size));
                            textY += 12;
                        }

                        var answerX = Margin + labelWidth + 7;
                        var answerY = cursorY + 7;
                        foreach (var line in optionLines)
                        {
                            graphics.DrawString(line, smallFont, mutedBrush, new XPoint(answerX, answerY + smallFont.Size));
                            answerY += 10;
                        }
                        graphics.DrawRectangle(XPens.Gray, XBrushes.White, answerX, answerY, answerWidth - 14, fieldHeight);
                        AddTextField(document, page!, fields, $"answer_{question.Id}", answerValue, answerX, answerY, answerWidth - 14, fieldHeight, IsMultiline(question.AnswerType));
                        cursorY += rowHeight;
                    }
                    else
                    {
                        var questionLines = WrapText(graphics, questionText, labelBoldFont, contentWidth);
                        var optionLines = choiceOptions.Count == 0
                            ? new List<string>()
                            : WrapText(graphics, $"Options: {string.Join(" | ", choiceOptions)}", smallFont, contentWidth * 0.7);
                        var fieldHeight = IsMultiline(question.AnswerType) ? MultilineFieldHeight : FieldHeight;
                        var blockHeight = (questionLines.Count * 12) + (optionLines.Count * 10) + fieldHeight + 24;
                        EnsureSpace(blockHeight);

                        foreach (var line in questionLines)
                        {
                            graphics.DrawString(line, labelBoldFont, textBrush, new XPoint(Margin, cursorY + labelBoldFont.Size));
                            cursorY += 12;
                        }
                        foreach (var line in optionLines)
                        {
                            graphics.DrawString(line, smallFont, mutedBrush, new XPoint(Margin, cursorY + smallFont.Size));
                            cursorY += 10;
                        }

                        var fieldWidth = contentWidth * 0.72;
                        graphics.DrawRectangle(XPens.Gray, XBrushes.White, Margin, cursorY + 3, fieldWidth, fieldHeight);
                        AddTextField(document, page!, fields, $"answer_{question.Id}", answerValue, Margin, cursorY + 3, fieldWidth, fieldHeight, IsMultiline(question.AnswerType));
                        cursorY += fieldHeight + 13;
                        graphics.DrawLine(borderPen, Margin, cursorY, Margin + contentWidth, cursorY);
                        cursorY += 11;
                    }
                }

                cursorY += 16;
            }

            graphics?.Dispose();
            ConfigureAcroForm(document, fields);

            using var stream = new MemoryStream();
            document.Save(stream, false);
            var safeCompanyName = Regex.Replace(surveyRecord.CompanyName ?? "Company", @"[^A-Za-z0-9_-]+", "-").Trim('-');
            return new EditableSurveyPdf(stream.ToArray(), $"{safeCompanyName}-{surveyRecord.Survey.FinancialYear}-survey.pdf");
        }

        private static void AddTextField(PdfDocument document, PdfPage page, PdfArray fields, string name, string value, double x, double y, double width, double height, bool multiline)
        {
            var field = new PdfDictionary(document);
            field.Elements.SetName("/Type", "/Annot");
            field.Elements.SetName("/Subtype", "/Widget");
            field.Elements.SetName("/FT", "/Tx");
            field.Elements.SetString("/T", name);
            field.Elements.SetString("/V", value, PdfStringEncoding.Unicode);
            field.Elements.SetString("/DV", value, PdfStringEncoding.Unicode);
            field.Elements.SetString("/DA", "/Helv 10 Tf 0 g");
            field.Elements.SetInteger("/F", 4);
            if (multiline)
            {
                field.Elements.SetInteger("/Ff", 4096);
            }

            var pageHeight = page.Height.Point;
            field.Elements.SetRectangle("/Rect", new PdfRectangle(new XRect(x, pageHeight - y - height, width, height)));
            field.Elements.SetReference("/P", page);
            document.Internals.AddObject(field);

            var annotations = page.Elements["/Annots"] as PdfArray;
            if (annotations == null)
            {
                annotations = new PdfArray(document);
                page.Elements.SetObject("/Annots", annotations);
            }

            annotations.Elements.Add(field.Reference);
            fields.Elements.Add(field.Reference);
        }

        private static void ConfigureAcroForm(PdfDocument document, PdfArray fields)
        {
            var font = new PdfDictionary(document);
            font.Elements.SetName("/Type", "/Font");
            font.Elements.SetName("/Subtype", "/Type1");
            font.Elements.SetName("/BaseFont", "/Helvetica");
            font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
            document.Internals.AddObject(font);

            var fonts = new PdfDictionary(document);
            fonts.Elements.SetReference("/Helv", font);
            var resources = new PdfDictionary(document);
            resources.Elements.SetObject("/Font", fonts);

            var acroForm = new PdfDictionary(document);
            acroForm.Elements.SetObject("/Fields", fields);
            acroForm.Elements.SetObject("/DR", resources);
            acroForm.Elements.SetString("/DA", "/Helv 10 Tf 0 g");
            acroForm.Elements.SetBoolean("/NeedAppearances", true);
            document.Internals.AddObject(acroForm);
            document.Internals.Catalog.Elements.SetReference("/AcroForm", acroForm);
        }

        private static List<string> WrapText(XGraphics graphics, string text, XFont font, double width)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var currentLine = string.Empty;

            foreach (var word in words)
            {
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (currentLine.Length > 0 && graphics.MeasureString(candidate, font).Width > width)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }

            return lines.Count > 0 ? lines : new List<string> { "Question" };
        }

        private static string FormatAnswer(Answer? answer, Question question)
        {
            if (answer == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(answer.AnswerText))
            {
                return answer.AnswerText.Trim();
            }

            var precision = question.DecimalPoints.HasValue && question.DecimalPoints.Value >= 0
                ? Math.Clamp(question.DecimalPoints.Value, 0, 6)
                : 0;
            var scale = question.DecimalPoints.HasValue && question.DecimalPoints.Value < 0
                ? (decimal)Math.Pow(10, -question.DecimalPoints.Value)
                : 1m;
            if (answer.AnswerCurrency.HasValue)
            {
                return (answer.AnswerCurrency.Value / scale).ToString($"N{precision}", CultureInfo.CurrentCulture);
            }

            if (answer.AnswerNumber.HasValue)
            {
                return (answer.AnswerNumber.Value / (double)scale).ToString($"N{precision}", CultureInfo.CurrentCulture);
            }

            return string.Empty;
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
            .Select(option => (option ?? string.Empty).Trim())
            .Where(option => option.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        }

        private static string ResolveFinancialYearText(string? value, int financialYear)
        {
            if (string.IsNullOrWhiteSpace(value) || financialYear <= 0)
            {
                return value ?? string.Empty;
            }

            var resolved = Regex.Replace(
                value,
                @"last\s*fin\w*\s*year",
                financialYear.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            resolved = Regex.Replace(
                resolved,
                @"current\s*financial\s*year\s*-\s*(\d+)|year\s*-\s*(\d+)",
                match =>
                {
                    var offsetGroup = match.Groups[1].Success ? match.Groups[1] : match.Groups[2];
                    return int.TryParse(offsetGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)
                        ? (financialYear - offset).ToString(CultureInfo.InvariantCulture)
                        : match.Value;
                },
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return Regex.Replace(
                resolved,
                @"current\s*financial\s*year",
                financialYear.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool IsMultiline(string? answerType)
        {
            var type = answerType?.Trim();
            return string.IsNullOrWhiteSpace(type)
                || type.Equals("Text", StringComparison.OrdinalIgnoreCase)
                || type.Equals("Textarea", StringComparison.OrdinalIgnoreCase)
                || type.Equals("LongText", StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanText(string? value)
        {
            var withoutHtml = Regex.Replace(value ?? string.Empty, "<[^>]+>", " ");
            return Regex.Replace(WebUtility.HtmlDecode(withoutHtml), @"\s+", " ").Trim();
        }

        private sealed record PdfSubgroupAssignment(
            int QuestionId,
            int SubgroupId,
            string? Title,
            bool NewHeader,
            int QuestionRows,
            int OrderNumber);

        private sealed record PdfQuestionRow(
            Question Question,
            Answer? Answer,
            PdfSubgroupAssignment? Subgroup);

        private sealed record PdfSurveySection(
            QuestionGroup? Group,
            List<PdfQuestionRow> Rows);
    }

    public record EditableSurveyPdf(byte[] Content, string FileName);
}