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
            .RequirePermission("dashboard.view");

        group.MapGet("/accounts", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetAccountsAsync(ct)))
            .RequirePermission("accounts.view");

        group.MapGet("/parties", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPartiesAsync(ct)))
            .RequirePermission("customers.view");

        group.MapGet("/items", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetItemsAsync(ct)))
            .RequirePermission("items.view");

        group.MapGet("/sales", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSalesAsync(ct)))
            .RequirePermission("sales.view");

        group.MapGet("/purchases", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetPurchasesAsync(ct)))
            .RequirePermission("purchases.view");

        group.MapGet("/journals", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetJournalsAsync(ct)))
            .RequirePermission("journals.view");

        group.MapGet("/security", async (MajedSoftLegacyStore store, CancellationToken ct) =>
            Results.Ok(await store.GetSecurityAsync(ct)))
            .RequirePermission("users.manage");

        group.MapGet("/health", async (MajedSoftLegacyStore store, CancellationToken ct) =>
        {
            var overview = await store.GetOverviewAsync(ct);
            return Results.Ok(new { status = "ok", database = "GtsDb2026", overview });
        });

        return group;
    }
}
