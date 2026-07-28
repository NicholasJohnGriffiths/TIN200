using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TINWeb.Services;

namespace TINWeb.Filters;

public sealed class ReadOnlyUserWriteBlockPageFilter : IAsyncPageFilter
{
    private static readonly HashSet<string> ReadOnlyBlockedGetPathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "edit",
        "delete"
    };

    private static readonly HashSet<string> AllowedMutatingPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Logout"
    };

    private static readonly string[] AllowedMutatingPathPrefixes =
    {
        "/Company/AnswerSurvey",
        "/Company/SurveyUpdate",
        "/Tin200/SurveyUpdate"
    };

    private static readonly string[] AllowedReadOnlyPostHandlerPrefixes =
    {
        "OnPostPreview",
        "OnPostExport",
        "OnPostCheck",
        "OnPostCancel"
    };

    private const string CompanySurveyEditPathPrefix = "/CompanySurvey/Edit";
    private const string CompanySurveyEstimationPathPrefix = "/CompanySurvey/Estimation";

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (!UserTypes.IsReadOnly(user))
        {
            await next();
            return;
        }

        var isSurveyEstimationsUser = UserTypes.IsSurveyEstimations(user);

        var request = context.HttpContext.Request;
        var path = request.Path.Value ?? string.Empty;
        var handlerMethodName = context.HandlerMethod?.MethodInfo?.Name ?? string.Empty;

        if (HttpMethods.IsGet(request.Method) && IsBlockedReadOnlyGetPath(request.Path.Value))
        {
            if (isSurveyEstimationsUser && IsSurveyEstimationsAllowedGetPath(path))
            {
                await next();
                return;
            }

            context.Result = new ForbidResult();
            return;
        }

        if (!IsMutatingMethod(request.Method))
        {
            await next();
            return;
        }

        if (AllowedMutatingPaths.Contains(path)
            || AllowedMutatingPathPrefixes.Any(prefix =>
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        if (isSurveyEstimationsUser && IsSurveyEstimationsAllowedMutatingRequest(path, handlerMethodName))
        {
            await next();
            return;
        }

        if (AllowedReadOnlyPostHandlerPrefixes.Any(prefix =>
                handlerMethodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        context.Result = new ForbidResult();
    }

    private static bool IsMutatingMethod(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }

    private static bool IsBlockedReadOnlyGetPath(string? pathValue)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        var segments = pathValue
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Any(segment => ReadOnlyBlockedGetPathSegments.Contains(segment));
    }

    private static bool IsSurveyEstimationsAllowedGetPath(string path)
    {
        return path.StartsWith(CompanySurveyEditPathPrefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(CompanySurveyEstimationPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSurveyEstimationsAllowedMutatingRequest(string path, string handlerMethodName)
    {
        if (path.StartsWith(CompanySurveyEstimationPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith(CompanySurveyEditPathPrefix, StringComparison.OrdinalIgnoreCase)
            && string.Equals(handlerMethodName, "OnPost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}