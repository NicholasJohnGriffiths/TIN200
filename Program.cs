using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using TINWeb.Data;
using TINWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
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

app.MapRazorPages();

app.Run();
