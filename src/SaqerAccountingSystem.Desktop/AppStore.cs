using System;
using System.Collections.Generic;
using System.Linq;

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
    public IReadOnlyList<UserProfile> Users { get; } = new[]
    {
        new UserProfile(1, "admin", "مدير النظام", "Administrators", "admin123",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "dashboard", "companies", "customers", "suppliers", "items",
                "sales", "purchases", "accounts", "inventory", "journals", "reports", "settings"
            }),
        new UserProfile(2, "accountant", "المحاسب", "Accountants", "123456",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "dashboard", "customers", "suppliers", "items",
                "sales", "purchases", "accounts", "inventory", "journals", "reports"
            })
    };

    public IReadOnlyList<ModuleInfo> Modules { get; } = new[]
    {
        new ModuleInfo("dashboard", "لوحة التحكم", "dashboard"),
        new ModuleInfo("companies", "الشركات والفروع", "companies"),
        new ModuleInfo("customers", "العملاء", "customers"),
        new ModuleInfo("suppliers", "الموردون", "suppliers"),
        new ModuleInfo("items", "الأصناف", "items"),
        new ModuleInfo("sales", "المبيعات", "sales"),
        new ModuleInfo("purchases", "المشتريات", "purchases"),
        new ModuleInfo("accounts", "دليل الحسابات", "accounts"),
        new ModuleInfo("inventory", "المخزون", "inventory"),
        new ModuleInfo("journals", "القيود اليومية", "journals"),
        new ModuleInfo("reports", "التقارير", "reports"),
        new ModuleInfo("settings", "الإعدادات", "settings")
    };

    public List<InvoiceRow> Sales { get; } = new()
    {
        new(2048, "INV-2048", "مؤسسة النخبة", new DateTime(2026, 10, 5), 18500m, "معتمدة"),
        new(2047, "INV-2047", "شركة الرؤية", new DateTime(2026, 10, 5), 9200m, "مدفوعة"),
        new(2046, "INV-2046", "مؤسسة المدار", new DateTime(2026, 10, 4), 6400m, "مسودة")
    };

    public List<InvoiceRow> Purchases { get; } = new()
    {
        new(1021, "PUR-1021", "شركة التوريد الحديثة", new DateTime(2026, 10, 4), 27400m, "معتمدة")
    };

    public List<ItemRow> Items { get; } = new()
    {
        new(1, "IT-001", "حاسب محمول", 32m, 3200m),
        new(2, "IT-002", "طابعة ليزر", 18m, 950m),
        new(3, "IT-003", "حبر طابعة", 74m, 180m)
    };

    public List<CustomerRow> Customers { get; } = new()
    {
        new(1, "C-1001", "مؤسسة النخبة", "0500000001", 48200m),
        new(2, "C-1002", "شركة الرؤية", "0500000002", 18500m),
        new(3, "C-1003", "مؤسسة المدار", "0500000003", 7200m)
    };

    public List<AccountRow> Accounts { get; } = new()
    {
        new("1101", "الصندوق", "أصول متداولة", 152400m),
        new("1102", "البنك", "أصول متداولة", 482600m),
        new("2101", "الموردون", "التزامات", 164900m),
        new("4101", "إيرادات المبيعات", "إيرادات", 1284500m)
    };

    public UserProfile? Authenticate(string username, string password) =>
        Users.FirstOrDefault(x =>
            x.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
            x.Password == password);
}

public sealed record InvoiceRow(int Id, string Number, string Party, DateTime Date, decimal Amount, string Status);
public sealed record ItemRow(int Id, string Code, string Name, decimal Stock, decimal SalePrice);
public sealed record CustomerRow(int Id, string Code, string Name, string Phone, decimal Balance);
public sealed record AccountRow(string Code, string Name, string Type, decimal Balance);
