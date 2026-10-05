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
            .RequireLegacySession();

        group.MapGet("/accounts", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetAccountsAsync(ct)))
            .RequireLegacyPermission("accounts.view");

        group.MapGet("/parties", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPartiesAsync(ct)))
            .RequireLegacyAnyPermission("customers.view", "suppliers.view");

        group.MapGet("/branches", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetBranchesAsync(ct)))
            .RequireLegacyPermission("companies.view");

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

        group.MapGet("/sales/{id:int}/details", async (int id, MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSaleDetailsAsync(id, ct)))
            .RequireLegacyPermission("sales.view");

        group.MapGet("/purchases/{id:int}/details", async (int id, MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPurchaseDetailsAsync(id, ct)))
            .RequireLegacyPermission("purchases.view");

        group.MapGet("/accounts/{id:int}/ledger", async (int id, MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetAccountLedgerAsync(id, ct)))
            .RequireLegacyPermission("accounts.view");

        group.MapGet("/security", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSecurityAsync(ct)))
            .RequireLegacyPermission("users.manage");

        group.MapGet("/stores", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetStoresAsync(ct)))
            .RequireLegacyPermission("items.view");

        group.MapGet("/cost-centers", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetCostCentersAsync(ct)))
            .RequireLegacyPermission("costcenters.view");

        group.MapGet("/stock-balances", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetStockBalancesAsync(ct)))
            .RequireLegacyPermission("inventory.view");

        group.MapGet("/treasury", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetTreasuryAsync(ct)))
            .RequireLegacyPermission("payments.view");

        group.MapGet("/tax-summary", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetTaxSummaryAsync(ct)))
            .RequireLegacyPermission("tax.view");

        group.MapGet("/sales-returns", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSalesReturnsAsync(ct)))
            .RequireLegacyPermission("sales.view");

        group.MapGet("/purchase-returns", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPurchaseReturnsAsync(ct)))
            .RequireLegacyPermission("purchases.view");

        group.MapGet("/reports/trial-balance", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetTrialBalanceAsync(ct)))
            .RequireLegacyPermission("reports.view");

        group.MapGet("/health", async (MajedSoftLegacyStore store, CancellationToken ct) =>
        {
            var overview = await store.GetOverviewAsync(ct);
            return Results.Ok(new { status = "ok", database = "GtsDb2026", overview });
        });

        endpoints.MapLegacyWriteEndpoints();
        return group;
    }

    private static RouteHandlerBuilder RequireLegacySession(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var session = GetLegacySession(context.HttpContext);
            if (session is null)
                return Results.Unauthorized();

            SetLegacySessionItems(context.HttpContext, session);
            return await next(context);
        });
    }

    private static RouteHandlerBuilder RequireLegacyAnyPermission(this RouteHandlerBuilder builder, params string[] permissions)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var session = GetLegacySession(context.HttpContext);
            if (session is null)
                return Results.Unauthorized();

            SetLegacySessionItems(context.HttpContext, session);

            if (!permissions.Any(permission =>
                    session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)))
                return Results.Forbid();

            return await next(context);
        });
    }

    private static RouteHandlerBuilder RequireLegacyPermission(this RouteHandlerBuilder builder, string permission)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var session = GetLegacySession(context.HttpContext);
            if (session is null)
                return Results.Unauthorized();

            SetLegacySessionItems(context.HttpContext, session);

            if (!session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                return Results.Forbid();

            return await next(context);
        });
    }

    private static LegacySession? GetLegacySession(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var header))
            return null;

        var value = header.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = value["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var sessions = context.RequestServices.GetService<LegacySessionStore>();
        if (sessions is null || !sessions.TryGet(token, out var session))
            return null;

        return session;
    }

    private static void SetLegacySessionItems(HttpContext context, LegacySession session)
    {
        context.Items["legacySession"] = session;
        context.Items["permissions"] = session.Permissions.ToArray();
        context.Items["legacyUser"] = session.User;
        context.Items["legacyGroup"] = session.Group;
    }
}
