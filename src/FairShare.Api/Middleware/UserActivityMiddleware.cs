using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace FairShare.Api.Middleware;

public class UserActivityMiddleware(RequestDelegate next)
{
    // Throttle interval to reduce DB writes
    private static readonly TimeSpan LastSeenUpdateInterval = TimeSpan.FromMinutes(5);

    public async Task Invoke(HttpContext ctx, UserManager<ApplicationUser> userMgr)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && !ctx.User.HasClaim("guest", "true"))
        {
            ApplicationUser? user = await userMgr.GetUserAsync(ctx.User);

            if (user is not null)
            {
                if (user.IsDisabled)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
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
