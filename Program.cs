using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/Tin200/SurveyUpdate");
    options.Conventions.AllowAnonymousToPage("/Tin200/SurveyLinkInvalid");
});
builder.Services.AddHealthChecks();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.Cookie.Name = "TINWorkspaceTemp.Auth";
        options.SlidingExpiration = true;
    });

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add custom services
builder.Services.AddScoped<Tin200Service>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<CompanySurveyService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<AzureCommunicationEmailSettings>(builder.Configuration.GetSection("AzureCommunicationEmail"));
builder.Services.Configure<SurveyLinkSettings>(builder.Configuration.GetSection("SurveyLinkSettings"));
builder.Services.AddScoped<ISurveyEmailService, SurveyEmailService>();
builder.Services.AddScoped<ISurveyLinkTokenService, SurveyLinkTokenService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapRazorPages();

app.Run();
