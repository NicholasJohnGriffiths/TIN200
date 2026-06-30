using System.Globalization;
using System.Security.Claims;

namespace TINWeb.Services;

public static class UserTypes
{
    public const int StandardUser = 0;
    public const int Admin = 1;
    public const int StandardUserReadOnly = 2;

    public static string GetLabel(int userType)
    {
        return userType switch
        {
            Admin => "Admin",
            StandardUser => "Standard User",
            StandardUserReadOnly => "Standard User - Readonly",
            _ => $"UserType {userType}"
        };
    }

    public static bool IsReadOnly(ClaimsPrincipal user)
    {
        var readOnlyRole = StandardUserReadOnly.ToString(CultureInfo.InvariantCulture);
        return user.IsInRole(readOnlyRole)
            || string.Equals(user.FindFirst("UserType")?.Value, readOnlyRole, StringComparison.Ordinal);
    }
}