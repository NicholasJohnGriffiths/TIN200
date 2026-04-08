using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;

namespace TINWeb.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public LoginModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    [Required]
    [Display(Name = "Email or username")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var loginIdentifier = Email.Trim();
        var normalizedLogin = loginIdentifier.ToLower();

        var user = await _context.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == normalizedLogin
                || u.UserName.ToLower() == normalizedLogin);

        if (user == null || user.Password != Password)
        {
            ModelState.AddModelError(string.Empty, "Invalid email/username or password.");
            return Page();
        }

        var displayName = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name;
        var email = string.IsNullOrWhiteSpace(user.Email) ? user.UserName : user.Email;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, user.UserType.ToString(CultureInfo.InvariantCulture)),
            new("UserName", user.UserName),
            new("UserType", user.UserType.ToString(CultureInfo.InvariantCulture))
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(claimsIdentity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }
}
