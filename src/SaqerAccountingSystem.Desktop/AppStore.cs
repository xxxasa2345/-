using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SaqerAccountingSystem.Desktop;

public sealed record UserProfile(
    int Id,
    string Username,
    string FullName,
    string Group,
    string Password,
    HashSet<string> Permissions,
    IReadOnlyList<LegacyScreenAccess> LegacyScreens);

public sealed record ModuleInfo(string Key, string Title, string Permission);

public sealed class AppStore
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private string _token = "";
    private bool _legacyMode;

    public AppStore()
    {
        var url = Environment.GetEnvironmentVariable("SAQER_API_URL") ?? LoadApiUrl();
        _http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
    }

    public IReadOnlyList<ModuleInfo> Modules { get; } = new[]
    {
        new ModuleInfo("dashboard","لوحة التحكم","dashboard.view"),
        new ModuleInfo("companies","الشركات والفروع","companies.view"),
        new ModuleInfo("customers","العملاء والذمم","customers.view"),
        new ModuleInfo("suppliers","الموردون والدائنون","suppliers.view"),
        new ModuleInfo("items","الأصناف","items.view"),
        new ModuleInfo("sales","المبيعات","sales.view"),
        new ModuleInfo("purchases","المشتريات","purchases.view"),
        new ModuleInfo("accounts","دليل الحسابات","accounts.view"),
        new ModuleInfo("journals","القيود والأستاذ العام","journals.view"),
        new ModuleInfo("payments","الخزينة والبنوك","payments.view"),
        new ModuleInfo("inventory","المخزون","inventory.view"),
        new ModuleInfo("tax","الضريبة","tax.view"),
        new ModuleInfo("assets","الأصول الثابتة","assets.view"),
        new ModuleInfo("costcenters","مراكز التكلفة","costcenters.view"),
        new ModuleInfo("budgets","الموازنات","budgets.view"),
        new ModuleInfo("reports","التقارير المالية","reports.view"),
        new ModuleInfo("settings","الإعدادات","settings.view")
    };

    public List<InvoiceRow> Sales { get; } = new();
    public List<InvoiceRow> Purchases { get; } = new();
    public List<ItemRow> Items { get; } = new();
    public List<CustomerRow> Customers { get; } = new();
    public List<SupplierRow> Suppliers { get; } = new();
    public List<CompanyRow> Companies { get; } = new();
    public List<AccountRow> Accounts { get; } = new();
    public List<PaymentRow> Payments { get; } = new();
    public List<JournalRow> Journals { get; } = new();
    public List<InventoryRow> Inventory { get; } = new();
    public List<TaxRow> Taxes { get; } = new();
    public List<AssetRow> Assets { get; } = new();
    public List<CostCenterRow> CostCenters { get; } = new();
    public List<BudgetRow> Budgets { get; } = new();

    private static string LoadApiUrl()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("ApiBaseUrl", out var value))
                    return value.GetString() ?? "http://localhost:5000";
            }
        }
        catch { }
        return "http://localhost:5000";
    }

    public UserProfile? Authenticate(string username, string password)
    {
        var legacy = TryAuthenticateLegacy(username, password);
        if (legacy is not null)
            return legacy;

        return TryAuthenticateNative(username, password);
    }

    private UserProfile? TryAuthenticateLegacy(string username, string password)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { username, password }, _json);
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/legacy/auth/login")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            using var response = _http.Send(request);
            if (!response.IsSuccessStatusCode) return null;

            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var root = document.RootElement;
            _token = root.GetProperty("token").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(_token)) return null;

            var user = root.GetProperty("user");
            var group = root.GetProperty("group");
            var permissions = root.GetProperty("permissions").EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var screens = root.TryGetProperty("screens", out var screenNode)
                ? screenNode.EnumerateArray().Select(ParseLegacyScreen).ToList()
                : new List<LegacyScreenAccess>();

            LegacyScreens.Clear();
            LegacyScreens.AddRange(screens);

            _legacyMode = true;
            CurrentPermissions = permissions;
            Refresh();

            return new UserProfile(
                user.GetProperty("id").GetInt32(),
                user.GetProperty("username").GetString() ?? username,
                user.GetProperty("fullName").GetString() ?? username,
                group.GetProperty("name").GetString() ?? "",
                "",
                permissions,
                screens);
        }
        catch
        {
            _token = "";
            _legacyMode = false;
            return null;
        }
    }

    private UserProfile? TryAuthenticateNative(string username, string password)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { username, password }, _json);
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            using var response = _http.Send(request);
            if (!response.IsSuccessStatusCode) return null;

            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var root = document.RootElement;
            _token = root.GetProperty("token").GetString() ?? "";
            var user = root.GetProperty("user");
            var groups = root.TryGetProperty("groups", out var gs) && gs.GetArrayLength() > 0
                ? gs[0].GetProperty("name").GetString() ?? ""
                : "";
            var permissions = root.GetProperty("permissions").EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _legacyMode = false;
            LegacyScreens.Clear();
            CurrentPermissions = permissions;
            Refresh();

            return new UserProfile(
                user.GetProperty("id").GetInt32(),
                user.GetProperty("username").GetString() ?? username,
                user.GetProperty("fullName").GetString() ?? username,
                groups, password, permissions, Array.Empty<LegacyScreenAccess>());
        }
        catch
        {
            _token = "";
            _legacyMode = false;
            return null;
        }
    }

    public DashboardSummary GetDashboard()
    {
        if (!_legacyMode)
            return Get<DashboardSummary>("api/dashboard");

        var overview = Get<LegacyOverviewRow>("api/legacy/overview");
        var cards = new List<MetricRow>
        {
            new("legacy.accounts", "الحسابات", overview.Accounts, 0),
            new("legacy.customers", "العملاء", overview.Customers, 0),
            new("legacy.suppliers", "الموردون", overview.Suppliers, 0),
            new("legacy.items", "الأصناف", overview.Items, 0),
            new("legacy.sales", "المبيعات", overview.Sales, 0),
            new("legacy.purchases", "المشتريات", overview.Purchases, 0),
            new("legacy.journals", "القيود", overview.JournalHeaders, 0)
        };

        var recent = Sales.Take(10)
            .Select(x => new DashboardInvoiceRow(
                x.Number, x.Party, x.Date.ToString("yyyy-MM-dd"), x.Amount, x.Status))
            .ToList();

        var activity = Journals.Take(10)
            .Select(x => new DashboardActivityRow(x.Date, "قيد " + x.Number, "journal"))
            .ToList();

        return new DashboardSummary(cards, recent, activity);
    }

    public string GetReport(string kind)
    {
        if (!_legacyMode)
            return GetJson("api/reports/" + kind);

        var overview = Get<LegacyOverviewRow>("api/legacy/overview");
        return JsonSerializer.Serialize(new
        {
            mode = "legacy",
            database = "GtsDb2026",
            report = kind,
            generatedAt = DateTime.Now,
            overview
        }, _json);
    }

    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(_token)) return;

        if (_legacyMode)
        {
            RefreshLegacy();
            return;
        }

        var data = Get<List<JsonElement>>("api/sales");
        Sales.Clear();
        Sales.AddRange(data.Select(x => InvoiceFromJson(x)).Where(x => x is not null)!);

        var purchases = Get<List<JsonElement>>("api/purchases");
        Purchases.Clear();
        Purchases.AddRange(purchases.Select(x => InvoiceFromJson(x)).Where(x => x is not null)!);

        var items = Get<List<ItemRow>>("api/items");
        Items.Clear(); Items.AddRange(items);

        var customers = Get<List<CustomerRow>>("api/customers");
        Customers.Clear(); Customers.AddRange(customers);

        var suppliers = Get<List<SupplierRow>>("api/suppliers");
        Suppliers.Clear(); Suppliers.AddRange(suppliers);

        var companies = Get<List<CompanyRow>>("api/companies");
        Companies.Clear(); Companies.AddRange(companies);

        var accounts = Get<List<AccountRow>>("api/accounts");
        Accounts.Clear(); Accounts.AddRange(accounts);

        var payments = Get<List<PaymentRow>>("api/payments");
        Payments.Clear(); Payments.AddRange(payments);

        var journals = Get<List<JournalRow>>("api/journals");
        Journals.Clear(); Journals.AddRange(journals);

        var inventory = Get<List<InventoryRow>>("api/inventory");
        Inventory.Clear(); Inventory.AddRange(inventory);

        var taxes = Get<List<TaxRow>>("api/tax");
        Taxes.Clear(); Taxes.AddRange(taxes);

        var assets = Get<List<AssetRow>>("api/assets");
        Assets.Clear(); Assets.AddRange(assets);

        var costCenters = Get<List<CostCenterRow>>("api/cost-centers");
        CostCenters.Clear(); CostCenters.AddRange(costCenters);

        var budgets = Get<List<BudgetRow>>("api/budgets?year=" + DateTime.Now.Year);
        Budgets.Clear(); Budgets.AddRange(budgets);
    }

    private void RefreshLegacy()
    {
        Sales.Clear();
        Purchases.Clear();
        Items.Clear();
        Customers.Clear();
        Suppliers.Clear();
        Accounts.Clear();
        Companies.Clear();
        Payments.Clear();
        Journals.Clear();
        Inventory.Clear();
        Taxes.Clear();
        Assets.Clear();
        CostCenters.Clear();
        Budgets.Clear();

        if (HasPermission("sales.view"))
        {
            var rows = TryGet<List<LegacySalesRow>>("api/legacy/sales") ?? new();
            Sales.AddRange(rows.Select(x => new InvoiceRow(
                x.Id,
                x.Id.ToString(),
                string.IsNullOrWhiteSpace(x.PartyName) ? "" : x.PartyName,
                x.Date ?? DateTime.MinValue,
                x.Net,
                "Legacy")));
        }

        if (HasPermission("purchases.view"))
        {
            var rows = TryGet<List<LegacyPurchaseRow>>("api/legacy/purchases") ?? new();
            Purchases.AddRange(rows.Select(x => new InvoiceRow(
                x.Id,
                x.Id.ToString(),
                x.SupplierName,
                x.Date ?? DateTime.MinValue,
                x.Net,
                "Legacy")));
        }

        if (HasPermission("items.view"))
        {
            var rows = TryGet<List<LegacyItemRow>>("api/legacy/items") ?? new();
            Items.AddRange(rows.Select(x => new ItemRow(
                x.Id, x.Code, x.Name, x.AverageCost, x.SellPriceSmall)));
        }

        if (HasPermission("customers.view") || HasPermission("suppliers.view"))
        {
            var rows = TryGet<List<LegacyPartyRow>>("api/legacy/parties") ?? new();
            if (HasPermission("customers.view"))
                Customers.AddRange(rows.Where(x => x.IsCustomer).Select(x =>
                    new CustomerRow(x.Id, x.Code?.ToString() ?? "", x.Name, x.Phone, 0)));
            if (HasPermission("suppliers.view"))
                Suppliers.AddRange(rows.Where(x => x.IsSupplier).Select(x =>
                    new SupplierRow(x.Id, x.Code?.ToString() ?? "", x.Name, x.Phone, 0)));
        }

        if (HasPermission("accounts.view"))
        {
            var rows = TryGet<List<LegacyAccountRow>>("api/legacy/accounts") ?? new();
            Accounts.AddRange(rows.Select(x =>
                new AccountRow(x.Id, x.AccountNo?.ToString() ?? "", x.Name, "Legacy", x.PrivDebit - x.PrivCredit)));
        }

        if (HasPermission("journals.view"))
        {
            var rows = TryGet<List<LegacyJournalRow>>("api/legacy/journals") ?? new();
            Journals.AddRange(rows.Select(x =>
                new JournalRow(x.Id, x.DocCode ?? x.Id.ToString(), x.Date ?? DateTime.MinValue, x.Note, "Legacy")));
        }
    }

    public List<LegacyScreenAccess> GetLegacyScreens() => LegacyScreens;

    public List<LegacySaleDetailRow> GetLegacySaleDetails(int id)
        => Get<List<LegacySaleDetailRow>>("api/legacy/sales/" + id + "/details");

    public List<LegacyPurchaseDetailRow> GetLegacyPurchaseDetails(int id)
        => Get<List<LegacyPurchaseDetailRow>>("api/legacy/purchases/" + id + "/details");

    public List<LegacyAccountLedgerRow> GetLegacyAccountLedger(int id)
        => Get<List<LegacyAccountLedgerRow>>("api/legacy/accounts/" + id + "/ledger");

    private readonly List<LegacyScreenAccess> LegacyScreens = new();

    public bool HasPermission(string permission)
        => CurrentPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private HashSet<string> CurrentPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private T? TryGet<T>(string path)
    {
        try
        {
            return Get<T>(path);
        }
        catch
        {
            return default;
        }
    }

    private static LegacyScreenAccess ParseLegacyScreen(JsonElement x)
        => new(
            x.GetProperty("screenId").GetInt32(),
            x.GetProperty("name").GetString() ?? "",
            x.TryGetProperty("screenTypeId", out var sti) && sti.ValueKind != JsonValueKind.Null ? sti.GetInt32() : null,
            x.TryGetProperty("screenNum", out var sn) && sn.ValueKind != JsonValueKind.Null ? sn.GetInt32() : null,
            x.GetProperty("screenTypeName").GetString() ?? "",
            x.GetProperty("isShow").GetBoolean(),
            x.GetProperty("allowBranch").GetBoolean(),
            x.GetProperty("allowEnter").GetBoolean(),
            x.GetProperty("allowSave").GetBoolean(),
            x.GetProperty("allowEdit").GetBoolean(),
            x.GetProperty("allowDelete").GetBoolean(),
            x.GetProperty("allowPrint").GetBoolean(),
            x.GetProperty("allowExport").GetBoolean());

    private T Get<T>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var response = _http.Send(request);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<T>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), _json) ?? throw new InvalidOperationException("Empty API response.");
    }

    private string GetJson(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var response = _http.Send(request);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static InvoiceRow? InvoiceFromJson(JsonElement x)
    {
        try
        {
            var id = x.GetProperty("id").GetInt64();
            var number = x.GetProperty("number").GetString() ?? "";
            var date = x.GetProperty("date").GetDateTime();
            var amount = x.GetProperty("total").GetDecimal();
            var status = x.GetProperty("status").GetString() ?? "";
            var party = x.TryGetProperty("customerId", out var c) ? "عميل #" + c.ToString() :
                        x.TryGetProperty("supplierId", out var s) ? "مورد #" + s.ToString() : "";
            return new InvoiceRow(id, number, party, date, amount, status);
        }
        catch { return null; }
    }
}

public sealed record LegacySaleDetailRow(int InvoiceId, int ItemId, string ItemCode, string ItemName, int? StoreId, int? UnitId, decimal Quantity, decimal UnitPrice, decimal TotalPrice, decimal Vat, decimal NetTotalPrice);
public sealed record LegacyPurchaseDetailRow(int InvoiceId, int ItemId, string ItemCode, string ItemName, int? StoreId, int? UnitId, decimal Quantity, decimal UnitPrice, decimal TotalPrice, decimal Vat, decimal NetTotalPrice);
public sealed record LegacyAccountLedgerRow(int TranId, string DocCode, DateTime? Date, string Note, string Description, decimal Debit, decimal Credit, int? CostCenterId, int? BranchId);

public sealed record LegacyScreenAccess(
    int ScreenId, string Name, int? ScreenTypeId, int? ScreenNum, string ScreenTypeName,
    bool IsShow, bool AllowBranch, bool AllowEnter, bool AllowSave, bool AllowEdit,
    bool AllowDelete, bool AllowPrint, bool AllowExport);

public sealed record LegacyOverviewRow(
    int Accounts, int Customers, int Suppliers, int Items, int Sales, int Purchases,
    int JournalHeaders, int Users, int Groups, int Permissions, int Screens);

public sealed record LegacyAccountRow(
    int Id, int? AccountNo, string Name, string EnglishName, int? Level, int? FinalAccount,
    int? AccountType, int? Nature, int? BranchId, decimal PrivDebit, decimal PrivCredit, int? Suspended);

public sealed record LegacyPartyRow(
    int Id, int? Code, int? AccountNo, int? BranchId, string Name, string VatNumber, string Phone,
    bool IsCustomer, bool IsSupplier, decimal CreditLimit, decimal AlarmLimit);

public sealed record LegacyItemRow(
    int Id, string Code, string Name, string EnglishName, int? CategoryId, int? ClassId,
    int? CompanyId, int? UnitSmall, decimal SellPriceSmall, decimal SellPriceMedium,
    decimal SellpriceLarge, decimal LastCost, decimal AverageCost, bool IsTax,
    decimal TaxValue, string VatCode);

public sealed record LegacySalesRow(
    int Id, int? BranchId, int? CreditNote, int? SupplierId, string PartyName, DateTime? Date,
    decimal TotalPrices, decimal Tax, decimal Net, decimal Cash, decimal Bank, decimal Paid,
    decimal Rest, int? UserId, int? YearId, int? ProjectId, string QrCode, string ElectronicInvoiceType);

public sealed record LegacyPurchaseRow(
    int Id, int? BranchId, int? SupplierId, string SupplierName, DateTime? Date,
    decimal TotalPrices, decimal Tax, decimal Net, decimal Cash, decimal Bank,
    int? CashAccount, int? BankAccount, int? UserId, int? YearId, int? ProjectId);

public sealed record LegacyJournalRow(
    int Id, int? ReferenceCode, int? TypeId, string DocCode, DateTime? Date, string Note,
    int? BranchId, int? UserId, int? YearId, int? ProjectId, decimal Debit, decimal Credit);

public sealed record InvoiceRow(long Id, string Number, string Party, DateTime Date, decimal Amount, string Status);
public sealed record ItemRow(int Id, string Code, string Name, decimal StockQuantity, decimal SalePrice);
public sealed record CustomerRow(int Id, string Code, string Name, string Phone, decimal Balance);
public sealed record AccountRow(int Id, string Code, string Name, string Type, decimal Balance);
public sealed record PaymentRow(long Id, string Number, string PartyType, decimal Amount, string Method, DateTime Date);
public sealed record JournalRow(long Id, string Number, DateTime Date, string Description, string Status);
public sealed record InventoryRow(long Id, int ItemId, DateTime Date, decimal QuantityIn, decimal QuantityOut, decimal UnitCost);

public sealed record SupplierRow(int Id, string Code, string Name, string Phone, decimal Balance);
public sealed record CompanyRow(int Id, string Code, string Name, string TaxNumber, string Currency, bool IsActive);

public sealed record TaxRow(int Id, string Code, string Name, decimal Rate, bool IsSales, bool IsPurchase, bool IsActive);
public sealed record AssetRow(int Id, string Code, string Name, DateTime AcquisitionDate, decimal Cost, decimal AccumulatedDepreciation, int UsefulLifeMonths, bool IsActive);
public sealed record CostCenterRow(int Id, string Code, string Name, bool IsActive);
public sealed record BudgetRow(long Id, int AccountId, int Year, int Month, decimal Amount);

public sealed record DashboardSummary(List<MetricRow> Cards, List<DashboardInvoiceRow> RecentInvoices, List<DashboardActivityRow> Activity);
public sealed record MetricRow(string Key, string Title, decimal Value, decimal Trend);
public sealed record DashboardInvoiceRow(string Number, string Party, string Date, decimal Amount, string Status);
public sealed record DashboardActivityRow(DateTime Time, string Text, string Type);
