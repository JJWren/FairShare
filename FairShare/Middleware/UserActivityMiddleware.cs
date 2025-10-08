using FairShare.Models;
using Microsoft.AspNetCore.Identity;

namespace FairShare.Middleware;

public class UserActivityMiddleware(RequestDelegate next)
{
    // Throttle interval to reduce DB writes
    private static readonly TimeSpan LastSeenUpdateInterval = TimeSpan.FromMinutes(5);

    public async Task Invoke(HttpContext ctx, UserManager<ApplicationUser> userMgr, SignInManager<ApplicationUser> signInManager)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && !ctx.User.HasClaim("guest","true"))
        {
            ApplicationUser? user = await userMgr.GetUserAsync(ctx.User);

            if (user is not null)
            {
                if (user.IsDisabled)
                {
                    await signInManager.SignOutAsync();
                    ctx.Response.Redirect("/Account/Login?disabled=1");
                    return;
                }

                if (user.LastSeenUtc is null || (DateTime.UtcNow - user.LastSeenUtc) > LastSeenUpdateInterval)
                {
                    user.LastSeenUtc = DateTime.UtcNow;
                    await userMgr.UpdateAsync(user);
                }
            }
        }

        await next(ctx);
    }
}
