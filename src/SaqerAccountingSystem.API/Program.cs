using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<AppStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    if (!context.Request.Headers.TryGetValue("Authorization", out var authorization) ||
        !authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "يلزم تسجيل الدخول." });
        return;
    }

    var token = authorization.ToString()["Bearer ".Length..].Trim();
    var store = context.RequestServices.GetRequiredService<AppStore>();

    if (!store.Sessions.TryGetValue(token, out var session))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "جلسة الدخول غير صالحة." });
        return;
    }

    context.Items["session"] = session;
    await next();
});

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    system = "Saqer Accounting System",
    version = "1.0.0"
})).WithTags("System");

app.MapPost("/api/auth/login", (LoginRequest request, AppStore store) =>
{
    var user = store.Users.Values.FirstOrDefault(u =>
        u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase) &&
        PasswordHasher.Verify(request.Password, u.PasswordHash));

    if (user is null)
        return Results.Unauthorized();

    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    var session = new Session(token, user.Id, user.Username, user.FullName, user.Group, user.Permissions);
    store.Sessions[token] = session;

    return Results.Ok(new
    {
        token,
        user = new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.Group,
            permissions = user.Permissions
        },
        navigation = store.Navigation.Where(n => user.Permissions.Contains(n.Permission))
    });
}).WithTags("Authentication");

app.MapPost("/api/auth/logout", (HttpContext context, AppStore store) =>
{
    var session = (Session)context.Items["session"]!;
    store.Sessions.TryRemove(session.Token, out _);
    return Results.Ok(new { message = "تم تسجيل الخروج." });
}).WithTags("Authentication");

app.MapGet("/api/auth/me", (HttpContext context, AppStore store) =>
{
    var session = (Session)context.Items["session"]!;
    return Results.Ok(new
    {
        user = new
        {
            session.UserId,
            session.Username,
            session.FullName,
            session.Group,
            permissions = session.Permissions
        },
        navigation = store.Navigation.Where(n => session.Permissions.Contains(n.Permission))
    });
}).WithTags("Authentication");

app.MapGet("/api/navigation", (HttpContext context, AppStore store) =>
{
    var session = (Session)context.Items["session"]!;
    return Results.Ok(store.Navigation.Where(n => session.Permissions.Contains(n.Permission)));
}).WithTags("Navigation");

app.MapGet("/api/dashboard", (HttpContext context, AppStore store) =>
{
    var session = (Session)context.Items["session"]!;
    if (!session.Permissions.Contains("dashboard.view"))
        return Results.Forbid();

    return Results.Ok(new
    {
        cards = new[]
        {
            new { key = "sales", title = "مبيعات اليوم", value = 128450m, trend = 12.4m },
            new { key = "purchases", title = "مشتريات اليوم", value = 74200m, trend = 8.1m },
            new { key = "receivables", title = "ذمم العملاء", value = 318600m, trend = -3.2m },
            new { key = "payables", title = "ذمم الموردين", value = 164900m, trend = 5.7m }
        },
        recentInvoices = store.Invoices.OrderByDescending(x => x.Date).Take(8),
        activity = store.Activity
    });
}).WithTags("Dashboard");

app.MapGet("/api/modules/{module}", (string module, HttpContext context, AppStore store) =>
{
    var session = (Session)context.Items["session"]!;

    if (!store.ModulePermissions.TryGetValue(module, out var permission) ||
        !session.Permissions.Contains(permission))
        return Results.Forbid();

    return module.ToLowerInvariant() switch
    {
        "companies" => Results.Ok(store.Companies),
        "customers" => Results.Ok(store.Customers),
        "suppliers" => Results.Ok(store.Suppliers),
        "items" => Results.Ok(store.Items),
        "sales" => Results.Ok(store.Invoices.Where(x => x.Kind == "Sale")),
        "purchases" => Results.Ok(store.Invoices.Where(x => x.Kind == "Purchase")),
        "accounts" => Results.Ok(store.Accounts),
        "inventory" => Results.Ok(store.Inventory),
        "reports" => Results.Ok(store.Reports),
        "settings" => Results.Ok(new { system = "Saqer Accounting System", environment = "Development" }),
        _ => Results.NotFound(new { message = "الوحدة غير موجودة." })
    };
}).WithTags("Modules");

app.MapFallbackToFile("index.html");

app.UseSwagger();
app.UseSwaggerUI();

app.Run();

public record LoginRequest(string Username, string Password);
public record Session(string Token, int UserId, string Username, string FullName, string Group, string[] Permissions);
public record User(int Id, string Username, string FullName, string Group, string PasswordHash, string[] Permissions);
public record NavItem(string Key, string Title, string Icon, string Permission, string Route);
public record InvoiceRow(int Id, string Number, string Kind, string Party, string Date, decimal Amount, string Status);

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    public static bool Verify(string password, string expectedHash)
        => Hash(password).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
}

public sealed class AppStore
{
    public ConcurrentDictionary<string, Session> Sessions { get; } = new();

    public Dictionary<string, User> Users { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = new(
            1,
            "admin",
            "مدير النظام",
            "Administrators",
            PasswordHasher.Hash("admin123"),
            new[]
            {
                "dashboard.view", "companies.view", "customers.view", "suppliers.view",
                "items.view", "sales.view", "purchases.view", "accounts.view",
                "inventory.view", "reports.view", "settings.view"
            }),
        ["accountant"] = new(
            2,
            "accountant",
            "المحاسب",
            "Accountants",
            PasswordHasher.Hash("123456"),
            new[]
            {
                "dashboard.view", "customers.view", "suppliers.view",
                "sales.view", "purchases.view", "accounts.view",
                "inventory.view", "reports.view"
            })
    };

    public Dictionary<string, string> ModulePermissions { get; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["companies"] = "companies.view",
            ["customers"] = "customers.view",
            ["suppliers"] = "suppliers.view",
            ["items"] = "items.view",
            ["sales"] = "sales.view",
            ["purchases"] = "purchases.view",
            ["accounts"] = "accounts.view",
            ["inventory"] = "inventory.view",
            ["reports"] = "reports.view",
            ["settings"] = "settings.view"
        };

    public List<NavItem> Navigation { get; } = new()
    {
        new("dashboard", "لوحة التحكم", "▦", "dashboard.view", "/dashboard"),
        new("companies", "الشركات والفروع", "⌂", "companies.view", "/companies"),
        new("customers", "العملاء", "◉", "customers.view", "/customers"),
        new("suppliers", "الموردون", "◎", "suppliers.view", "/suppliers"),
        new("items", "الأصناف والمخزون", "▤", "items.view", "/items"),
        new("sales", "المبيعات", "▣", "sales.view", "/sales"),
        new("purchases", "المشتريات", "▥", "purchases.view", "/purchases"),
        new("accounts", "دليل الحسابات", "◫", "accounts.view", "/accounts"),
        new("reports", "التقارير المالية", "◰", "reports.view", "/reports"),
        new("settings", "الإعدادات", "⚙", "settings.view", "/settings")
    };

    public List<object> Companies { get; } = new()
    {
        new { id = 1, code = "HQ", name = "شركة صقر القابضة", branch = "الرئيسي", status = "نشطة" },
        new { id = 2, code = "JED", name = "فرع جدة", branch = "جدة", status = "نشطة" },
        new { id = 3, code = "RYD", name = "فرع الرياض", branch = "الرياض", status = "نشطة" }
    };

    public List<object> Customers { get; } = new()
    {
        new { id = 1, code = "C-1001", name = "مؤسسة النخبة", phone = "0500000001", balance = 48200m, status = "نشط" },
        new { id = 2, code = "C-1002", name = "شركة الرؤية", phone = "0500000002", balance = 18500m, status = "نشط" },
        new { id = 3, code = "C-1003", name = "مؤسسة المدار", phone = "0500000003", balance = 7200m, status = "نشط" }
    };

    public List<object> Suppliers { get; } = new()
    {
        new { id = 1, code = "S-2001", name = "شركة التوريد الحديثة", phone = "0110000001", balance = -32600m, status = "نشط" },
        new { id = 2, code = "S-2002", name = "مورد التجزئة", phone = "0110000002", balance = -12800m, status = "نشط" }
    };

    public List<object> Items { get; } = new()
    {
        new { id = 1, code = "IT-001", name = "حاسب محمول", category = "أجهزة", stock = 32, salePrice = 3200m },
        new { id = 2, code = "IT-002", name = "طابعة ليزر", category = "أجهزة", stock = 18, salePrice = 950m },
        new { id = 3, code = "IT-003", name = "حبر طابعة", category = "مستلزمات", stock = 74, salePrice = 180m }
    };

    public List<object> Accounts { get; } = new()
    {
        new { code = "1101", name = "الصندوق", type = "أصول متداولة", balance = 152400m },
        new { code = "1102", name = "البنك", type = "أصول متداولة", balance = 482600m },
        new { code = "2101", name = "الموردون", type = "التزامات", balance = 164900m },
        new { code = "4101", name = "إيرادات المبيعات", type = "إيرادات", balance = 1284500m }
    };

    public List<InvoiceRow> Invoices { get; } = new()
    {
        new(1, "INV-2048", "Sale", "مؤسسة النخبة", "2026-10-05", 18500m, "معتمدة"),
        new(2, "INV-2047", "Sale", "شركة الرؤية", "2026-10-05", 9200m, "مدفوعة"),
        new(3, "PUR-1021", "Purchase", "شركة التوريد الحديثة", "2026-10-04", 27400m, "معتمدة"),
        new(4, "INV-2046", "Sale", "مؤسسة المدار", "2026-10-04", 6400m, "مسودة")
    };

    public List<object> Inventory { get; } = new()
    {
        new { id = 1, item = "حاسب محمول", movement = "بيع", quantity = -2, branch = "جدة", date = "2026-10-05" },
        new { id = 2, item = "طابعة ليزر", movement = "شراء", quantity = 8, branch = "الرياض", date = "2026-10-05" }
    };

    public List<object> Reports { get; } = new()
    {
        new { key = "income", title = "قائمة الدخل", period = "أكتوبر 2026", value = 486200m },
        new { key = "balance", title = "الميزانية العمومية", period = "حتى 05 أكتوبر 2026", value = 902400m },
        new { key = "trial", title = "ميزان المراجعة", period = "حتى 05 أكتوبر 2026", value = 1649000m }
    };

    public List<object> Activity { get; } = new()
    {
        new { time = "08:45", text = "اعتماد قيد يومي TRX-1024", type = "journal" },
        new { time = "09:10", text = "إنشاء فاتورة مبيعات INV-2048", type = "sale" },
        new { time = "09:25", text = "تسجيل دفعة من عميل", type = "payment" },
        new { time = "10:05", text = "إضافة حركة مخزون", type = "inventory" }
    };
}
