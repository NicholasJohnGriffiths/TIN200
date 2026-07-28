using System.Globalization;
using System.Security.Claims;

namespace TINWeb.Services;

public static class UserTypes
{
    public const int StandardUser = 0;
    public const int Admin = 1;
    public const int StandardUserReadOnly = 2;
    public const int SurveyEstimations = 3;

    public static string GetLabel(int userType)
    {
        return userType switch
        {
            Admin => "Admin",
            StandardUser => "Standard User",
            StandardUserReadOnly => "Standard User - Readonly",
            SurveyEstimations => "Survey/Estimations",
            _ => $"UserType {userType}"
        };
    }

    public static bool IsStandardReadOnly(ClaimsPrincipal user)
    {
        return HasUserType(user, StandardUserReadOnly);
    }

    public static bool IsSurveyEstimations(ClaimsPrincipal user)
    {
        return HasUserType(user, SurveyEstimations);
    }

    public static bool IsReadOnly(ClaimsPrincipal user)
    {
        return IsStandardReadOnly(user) || IsSurveyEstimations(user);
    }

    private static bool HasUserType(ClaimsPrincipal user, int userType)
    {
        var roleValue = userType.ToString(CultureInfo.InvariantCulture);
        return user.IsInRole(roleValue)
            || string.Equals(user.FindFirst("UserType")?.Value, roleValue, StringComparison.Ordinal);
    }
}