using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TINWeb.Services;

namespace TINWeb.Filters;

public sealed class ReadOnlyUserWriteBlockPageFilter : IAsyncPageFilter
{
    private static readonly HashSet<string> AllowedMutatingPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Logout"
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
        if (!IsMutatingMethod(request.Method))
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;
        if (AllowedMutatingPaths.Contains(path))
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
}