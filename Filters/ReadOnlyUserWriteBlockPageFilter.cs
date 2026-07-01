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

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!UserTypes.IsReadOnly(context.HttpContext.User))
        {
            await next();
            return;
        }

        var request = context.HttpContext.Request;

        if (HttpMethods.IsGet(request.Method) && IsBlockedReadOnlyGetPath(request.Path.Value))
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!IsMutatingMethod(request.Method))
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;
        if (AllowedMutatingPaths.Contains(path)
            || AllowedMutatingPathPrefixes.Any(prefix =>
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        var handlerMethodName = context.HandlerMethod?.MethodInfo?.Name ?? string.Empty;
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
}