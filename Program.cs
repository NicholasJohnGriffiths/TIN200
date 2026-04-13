using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using TINWeb.Data;
using TINWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AuthorizeFolder("/AppUsers", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Config", "AdminOnly");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/Company/SurveyUpdate");
    options.Conventions.AllowAnonymousToPage("/Company/AnswerSurvey");
    options.Conventions.AllowAnonymousToPage("/Company/SurveyLinkInvalid");

    options.Conventions.AddPageRoute("/Company/Index", "/Tin200");
    options.Conventions.AddPageRoute("/Company/Create", "/Tin200/Create");
    options.Conventions.AddPageRoute("/Company/Edit", "/Tin200/Edit/{id?}");
    options.Conventions.AddPageRoute("/Company/Details", "/Tin200/Details/{id?}");
    options.Conventions.AddPageRoute("/Company/Delete", "/Tin200/Delete/{id?}");
    options.Conventions.AddPageRoute("/Company/SurveyHistory", "/Tin200/SurveyHistory/{id:int}");
    options.Conventions.AddPageRoute("/Company/Import", "/Tin200/Import");
    options.Conventions.AddPageRoute("/Company/SendSurvey", "/Tin200/SendSurvey");
    options.Conventions.AddPageRoute("/Company/SurveyUpdate", "/Tin200/SurveyUpdate/{id:int}");
    options.Conventions.AddPageRoute("/Company/SurveyLinkInvalid", "/Tin200/SurveyLinkInvalid");
});
builder.Services.AddHealthChecks();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.Cookie.Name = "TINWeb.Auth";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("1"));
});

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add custom services
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<CompanySurveyService>();
builder.Services.AddScoped<AnswerService>();
builder.Services.AddScoped<QuestionService>();
builder.Services.AddScoped<QuestionGroupService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<SurveyEmailBounceService>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<AzureCommunicationEmailSettings>(builder.Configuration.GetSection("AzureCommunicationEmail"));
builder.Services.Configure<SurveyLinkSettings>(builder.Configuration.GetSection("SurveyLinkSettings"));
builder.Services.AddScoped<ISurveyEmailService, SurveyEmailService>();
builder.Services.AddScoped<ISurveyLinkTokenService, SurveyLinkTokenService>();
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 20000;
});

var app = builder.Build();

var urls = builder.Configuration["ASPNETCORE_URLS"];
var httpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"] ?? builder.Configuration["HTTPS_PORT"];
var shouldUseHttpsRedirection = !app.Environment.IsDevelopment()
    || !string.IsNullOrWhiteSpace(httpsPort)
    || (!string.IsNullOrWhiteSpace(urls) && urls.Contains("https://", StringComparison.OrdinalIgnoreCase));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (shouldUseHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapPost("/api/webhooks/email-events", async (HttpContext httpContext, SurveyEmailBounceService bounceService) =>
{
    using var payload = await JsonDocument.ParseAsync(httpContext.Request.Body);

    var validationResponse = bounceService.TryBuildSubscriptionValidationResponse(payload);
    if (validationResponse != null)
    {
        return Results.Ok(validationResponse);
    }

    var processedCount = await bounceService.ProcessEventGridEventsAsync(payload);
    return Results.Ok(new { processed = processedCount });
});

app.MapGet("/api/config/email-header-image/{imageId:int}", async (int imageId, ApplicationDbContext dbContext, IImageStorageService imageStorageService) =>
{
    var configuredImageId = await dbContext.AppConfig
        .AsNoTracking()
        .OrderBy(c => c.Id)
        .Select(c => c.EmailHeaderImageId)
        .FirstOrDefaultAsync();

    if (!configuredImageId.HasValue || configuredImageId.Value != imageId)
    {
        return Results.NotFound();
    }

    var image = await dbContext.Image
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == imageId);

    if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
    {
        return Results.NotFound();
    }

    var stream = await imageStorageService.OpenReadAsync(image.FilePath);
    if (stream == null)
    {
        return Results.NotFound();
    }

    var contentTypeProvider = new FileExtensionContentTypeProvider();
    var extension = Path.GetExtension(image.FilePath);
    if (!contentTypeProvider.TryGetContentType($"file{extension}", out var contentType))
    {
        contentType = "application/octet-stream";
    }

    return Results.File(stream, contentType);
});

app.MapRazorPages();

app.Run();
