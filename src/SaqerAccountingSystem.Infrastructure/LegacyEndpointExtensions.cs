using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SaqerAccountingSystem.Infrastructure;

public static class LegacyEndpointExtensions
{
    public static RouteGroupBuilder MapLegacyEndpoints(this IEndpointRouteBuilder endpoints)
    {
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
