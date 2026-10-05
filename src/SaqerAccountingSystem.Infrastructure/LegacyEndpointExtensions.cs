using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SaqerAccountingSystem.Infrastructure;

public sealed record LoginLegacyRequest(string Username, string Password);

public static class LegacyEndpointExtensions
{
    public static RouteGroupBuilder MapLegacyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/legacy/auth/login", async (
            LoginLegacyRequest request,
            MajedSoftLegacyStore store,
            LegacySessionStore sessions,
            CancellationToken ct) =>
        {
            var result = await store.AuthenticateAsync(request.Username, request.Password, ct);
            if (result is null)
                return Results.Unauthorized();

            var session = new LegacySession(
                result.User,
                result.Group,
                result.Screens,
                result.Permissions,
                DateTime.UtcNow.AddHours(12));

            var token = sessions.Create(session);
            return Results.Ok(new
            {
                token,
                expiresAt = session.ExpiresAt,
                user = new
                {
                    id = result.User.Id,
                    username = result.User.Name,
                    fullName = result.User.Name,
                    branchId = result.User.BranchId,
                    groupId = result.User.GroupId
                },
                group = new
                {
                    id = result.Group.Id,
                    name = result.Group.Name,
                    branchId = result.Group.BranchId
                },
                permissions = result.Permissions.OrderBy(x => x).ToArray(),
                screens = result.Screens
            });
        }).WithTags("Authentication");

        endpoints.MapGet("/api/legacy/auth/me", (HttpContext ctx) =>
        {
            var session = ctx.Items["legacySession"] as LegacySession;
            if (session is null) return Results.Unauthorized();

            return Results.Ok(new
            {
                user = session.User,
                group = session.Group,
                permissions = session.Permissions.OrderBy(x => x).ToArray(),
                screens = session.Screens
            });
        }).WithTags("Authentication");

        endpoints.MapPost("/api/legacy/auth/logout", (HttpContext ctx, LegacySessionStore sessions) =>
        {
            if (ctx.Request.Headers.TryGetValue("Authorization", out var header) &&
                header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                sessions.Remove(header.ToString()["Bearer ".Length..].Trim());
            }
            return Results.Ok(new { message = "تم تسجيل الخروج." });
        }).WithTags("Authentication");

        var group = endpoints.MapGroup("/api/legacy").WithTags("MajedSoft Legacy");

        group.MapGet("/overview", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetOverviewAsync(ct)))
            .RequireLegacyPermission("dashboard.view");

        group.MapGet("/accounts", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetAccountsAsync(ct)))
            .RequireLegacyPermission("accounts.view");

        group.MapGet("/parties", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPartiesAsync(ct)))
            .RequireLegacyPermission("customers.view");

        group.MapGet("/items", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetItemsAsync(ct)))
            .RequireLegacyPermission("items.view");

        group.MapGet("/sales", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSalesAsync(ct)))
            .RequireLegacyPermission("sales.view");

        group.MapGet("/purchases", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPurchasesAsync(ct)))
            .RequireLegacyPermission("purchases.view");

        group.MapGet("/journals", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetJournalsAsync(ct)))
            .RequireLegacyPermission("journals.view");

        group.MapGet("/security", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSecurityAsync(ct)))
            .RequireLegacyPermission("users.manage");

        group.MapGet("/health", async (MajedSoftLegacyStore store, CancellationToken ct) =>
        {
            var overview = await store.GetOverviewAsync(ct);
            return Results.Ok(new { status = "ok", database = "GtsDb2026", overview });
        });

        return group;
    }

    private static RouteHandlerBuilder RequireLegacyPermission(this RouteHandlerBuilder builder, string permission)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var permissions = context.HttpContext.Items["permissions"] as string[] ?? [];
            if (!permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                return Results.Forbid();
            return await next(context);
        });
    }
}
