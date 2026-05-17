using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Stripe;
using TINWeb.Data;
using TINWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDistributedMemoryCache();
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
})
    .AddSessionStateTempDataProvider();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "TINWeb.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
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
builder.Services.AddOptions<StripeSettings>().Configure<IConfiguration>((settings, configuration) =>
{
    var testModeRaw = configuration["Stripe:Testmode:TINWeb"]
        ?? Environment.GetEnvironmentVariable("Stripe__Testmode__TINWeb");
    var normalizedTestMode = testModeRaw?.Trim();
    var useTestMode = string.Equals(normalizedTestMode, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedTestMode, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedTestMode, "yes", StringComparison.OrdinalIgnoreCase);

    var livePublishableKey = configuration["Stripe:PublishableKey:TINWeb"]
        ?? Environment.GetEnvironmentVariable("Stripe__PublishableKey__TINWeb")
        ?? configuration["Stripe:PublishableKey"]
        ?? Environment.GetEnvironmentVariable("Stripe__PublishableKey")
        ?? string.Empty;
    var liveSecretKey = configuration["Stripe:SecretKey:TINWeb"]
        ?? Environment.GetEnvironmentVariable("Stripe__SecretKey__TINWeb")
        ?? configuration["Stripe:SecretKey"]
        ?? Environment.GetEnvironmentVariable("Stripe__SecretKey")
        ?? string.Empty;
    var liveWebhookSecret = configuration["Stripe:WebhookSecret:TINWeb"]
        ?? Environment.GetEnvironmentVariable("Stripe__WebhookSecret__TINWeb")
        ?? configuration["Stripe:WebhookSecret"]
        ?? Environment.GetEnvironmentVariable("Stripe__WebhookSecret")
        ?? string.Empty;
    var testPublishableKey = configuration["Stripe:PublishableKey:TINWeb:Test"]
        ?? Environment.GetEnvironmentVariable("Stripe__PublishableKey__TINWeb__Test")
        ?? configuration["Stripe:PublishableKey:Test"]
        ?? Environment.GetEnvironmentVariable("Stripe__PublishableKey__Test")
        ?? string.Empty;
    var testSecretKey = configuration["Stripe:SecretKey:TINWeb:Test"]
        ?? Environment.GetEnvironmentVariable("Stripe__SecretKey__TINWeb__Test")
        ?? configuration["Stripe:SecretKey:Test"]
        ?? Environment.GetEnvironmentVariable("Stripe__SecretKey__Test")
        ?? string.Empty;
    var testWebhookSecret = configuration["Stripe:WebhookSecret:TINWeb:Test"]
        ?? Environment.GetEnvironmentVariable("Stripe__WebhookSecret__TINWeb__Test")
        ?? configuration["Stripe:WebhookSecret:Test"]
        ?? Environment.GetEnvironmentVariable("Stripe__WebhookSecret__Test")
        ?? string.Empty;

    settings.UseTestMode = useTestMode;
    settings.PublishableKey = useTestMode
        ? (!string.IsNullOrWhiteSpace(testPublishableKey) ? testPublishableKey : livePublishableKey)
        : livePublishableKey;
    settings.SecretKey = useTestMode
        ? (!string.IsNullOrWhiteSpace(testSecretKey) ? testSecretKey : liveSecretKey)
        : liveSecretKey;
    settings.WebhookSecret = liveWebhookSecret;
    settings.WebhookSecretTest = testWebhookSecret;
});
builder.Services.AddScoped<ISurveyEmailService, SurveyEmailService>();
builder.Services.AddScoped<ISurveyLinkTokenService, SurveyLinkTokenService>();
builder.Services.AddScoped<StripeTransactionService>();
builder.Services.Configure<GravityFormsSettings>(builder.Configuration.GetSection("WordPress"));
builder.Services.AddHttpClient<GravityFormsService>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var settings = sp.GetRequiredService<IOptions<GravityFormsSettings>>().Value;
    var baseUrlCandidate = !string.IsNullOrWhiteSpace(settings.BaseUrl)
        ? settings.BaseUrl
        : configuration["WP:RESTAPI:Url"]
            ?? Environment.GetEnvironmentVariable("WP__RESTAPI__Url")
            ?? configuration["WP:RESTAPI:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("WP__RESTAPI__BaseUrl");

    if (!string.IsNullOrWhiteSpace(baseUrlCandidate))
    {
        var baseUrl = baseUrlCandidate.Trim();
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        client.BaseAddress = new Uri(baseUrl);
    }

    var username = !string.IsNullOrWhiteSpace(settings.Username)
        ? settings.Username
        : configuration["WP:RESTAPI:Username"]
            ?? Environment.GetEnvironmentVariable("WP__RESTAPI__Username");

    var applicationPassword = !string.IsNullOrWhiteSpace(settings.ApplicationPassword)
        ? settings.ApplicationPassword
        : configuration["WP:RESTAPI:Token"]
            ?? Environment.GetEnvironmentVariable("WP__RESTAPI__Token")
            ?? configuration["WP:RESTAPI:ApplicationPassword"]
            ?? Environment.GetEnvironmentVariable("WP__RESTAPI__ApplicationPassword");

    if (!string.IsNullOrWhiteSpace(username)
        && !string.IsNullOrWhiteSpace(applicationPassword))
    {
        var authBytes = Encoding.UTF8.GetBytes($"{username}:{applicationPassword}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authBytes));
    }

    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
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
app.UseSession();

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

app.MapPost("/api/stripe/webhook", async (HttpContext httpContext, IOptions<StripeSettings> stripeOptions, ILogger<Program> logger) =>
{
    var stripeSettings = stripeOptions.Value;

    using var reader = new StreamReader(httpContext.Request.Body);
    var payload = await reader.ReadToEndAsync();
    var signature = httpContext.Request.Headers["Stripe-Signature"].ToString();

    if (string.IsNullOrWhiteSpace(signature))
    {
        logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
        return Results.BadRequest("Missing Stripe-Signature header.");
    }

    var selectedWebhookSecret = stripeSettings.UseTestMode
        ? (!string.IsNullOrWhiteSpace(stripeSettings.WebhookSecretTest) ? stripeSettings.WebhookSecretTest : stripeSettings.WebhookSecret)
        : stripeSettings.WebhookSecret;

    if (string.IsNullOrWhiteSpace(selectedWebhookSecret))
    {
        logger.LogError("Stripe webhook secret is not configured for mode {Mode}.", stripeSettings.UseTestMode ? "Test" : "Live");
        return Results.Problem(
            detail: stripeSettings.UseTestMode
                ? "Stripe test webhook secret is missing. Set Stripe__WebhookSecret__TINWeb__Test."
                : "Stripe live webhook secret is missing. Set Stripe__WebhookSecret__TINWeb.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    Event stripeEvent;

    try
    {
        stripeEvent = EventUtility.ConstructEvent(payload, signature, selectedWebhookSecret, throwOnApiVersionMismatch: false);
    }
    catch (StripeException ex)
    {
        logger.LogWarning(ex, "Stripe webhook signature verification failed.");
        return Results.BadRequest("Invalid webhook signature.");
    }

    logger.LogInformation("Stripe webhook received. EventId={EventId}, EventType={EventType}, Livemode={Livemode}",
        stripeEvent.Id,
        stripeEvent.Type,
        stripeEvent.Livemode);

    return Results.Ok(new { received = true, eventId = stripeEvent.Id, eventType = stripeEvent.Type });
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
