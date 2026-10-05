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
    HashSet<string> Permissions);

public sealed record ModuleInfo(string Key, string Title, string Permission);

public sealed class AppStore
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private string _token = "";

    public AppStore()
    {
        var url = Environment.GetEnvironmentVariable("SAQER_API_URL") ?? "http://localhost:5000";
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

    public UserProfile? Authenticate(string username, string password)
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
                .Select(x => x.GetString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);

            Refresh();

            return new UserProfile(
                user.GetProperty("id").GetInt32(),
                user.GetProperty("username").GetString() ?? username,
                user.GetProperty("fullName").GetString() ?? username,
                groups, password, permissions);
        }
        catch
        {
            _token = "";
            return null;
        }
    }

    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(_token)) return;
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

    private T Get<T>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var response = _http.Send(request);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<T>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), _json) ?? throw new InvalidOperationException("Empty API response.");
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
