using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SaqerAccountingSystem.Infrastructure;

public static class LegacyWriteEndpointExtensions
{
    public static void MapLegacyWriteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/legacy").WithTags("MajedSoft Legacy - Write");

        group.MapPost("/journals", async (
            LegacyJournalWriteRequest request,
            HttpContext ctx,
            Microsoft.Extensions.Configuration.IConfiguration config,
            CancellationToken ct) =>
        {
            if (!HasLegacyPermission(ctx, "journals.create"))
                return Results.Forbid();

            var session = GetLegacySession(ctx);
            if (session is null) return Results.Unauthorized();
            var store = new MajedSoftLegacyWriteStore(config);

            try
            {
                var result = await store.CreateJournalAsync(request, session.User, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        group.MapPost("/sales", async (
            LegacySaleWriteRequest request,
            HttpContext ctx,
            MajedSoftLegacyWriteStore store,
            CancellationToken ct) =>
        {
            if (!HasLegacyPermission(ctx, "sales.create"))
                return Results.Forbid();

            var session = GetLegacySession(ctx);
            if (session is null) return Results.Unauthorized();
            var store = new MajedSoftLegacyWriteStore(config);

            try
            {
                var result = await store.CreateSaleAsync(request, session.User, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        group.MapPost("/purchases", async (
            LegacyPurchaseWriteRequest request,
            HttpContext ctx,
            MajedSoftLegacyWriteStore store,
            CancellationToken ct) =>
        {
            if (!HasLegacyPermission(ctx, "purchases.create"))
                return Results.Forbid();

            var session = GetLegacySession(ctx);
            if (session is null) return Results.Unauthorized();
            var store = new MajedSoftLegacyWriteStore(config);

            try
            {
                var result = await store.CreatePurchaseAsync(request, session.User, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
    }

    private static LegacySession? GetLegacySession(HttpContext ctx)
        => ctx.Items["legacySession"] as LegacySession;

    private static bool HasLegacyPermission(HttpContext ctx, string permission)
        => (ctx.Items["permissions"] as string[] ?? [])
            .Contains(permission, StringComparer.OrdinalIgnoreCase);
}
