using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YKJProAPI.Services;

namespace YKJProAPI.Filters;

// Requires "Authorization: Bearer <session token>" obtained from POST /api/admin/login.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminAuthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<SkipAdminAuthAttribute>().Any())
            return;

        var auth = context.HttpContext.RequestServices.GetRequiredService<AdminAuth>();
        if (!auth.Enabled)
        {
            context.Result = new ObjectResult(new { error = "Admin is disabled (ADMIN_TOKEN not set)." }) { StatusCode = 503 };
            return;
        }

        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Trim() : null;
        if (!auth.ValidateSession(token))
            context.Result = new UnauthorizedObjectResult(new { error = "Not logged in or session expired." });
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class SkipAdminAuthAttribute : Attribute { }
