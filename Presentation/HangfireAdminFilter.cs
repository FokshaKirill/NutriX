using Hangfire.Dashboard;

namespace Presentation;

public class HangfireAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        // Разрешаем только залогиненным Admin
        return http.User.Identity?.IsAuthenticated == true
               && http.User.IsInRole("Admin");
    }
}