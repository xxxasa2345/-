using Microsoft.EntityFrameworkCore;
using SaqerAccountingSystem.Domain;

namespace SaqerAccountingSystem.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AccountingDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Companies.AnyAsync()) return;

        var company = new Company { Code = "HQ", Name = "شركة صقر للمحاسبة", Currency = "SAR" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.Branches.Add(new Branch { CompanyId = company.Id, Code = "MAIN", Name = "الفرع الرئيسي", Address = "المملكة العربية السعودية" });

        var accounts = new[]
        {
            new Account { CompanyId = company.Id, Code = "1101", Name = "الصندوق", Type = AccountType.Asset, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "1102", Name = "البنوك", Type = AccountType.Asset, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "1201", Name = "العملاء", Type = AccountType.Asset, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "1301", Name = "المخزون", Type = AccountType.Asset, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "2101", Name = "الموردون", Type = AccountType.Liability, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "2201", Name = "ضريبة القيمة المضافة", Type = AccountType.Liability, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "3101", Name = "رأس المال", Type = AccountType.Equity, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "4101", Name = "إيرادات المبيعات", Type = AccountType.Revenue, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "5101", Name = "تكلفة المبيعات", Type = AccountType.Expense, IsControlAccount = true },
            new Account { CompanyId = company.Id, Code = "5201", Name = "المصروفات التشغيلية", Type = AccountType.Expense }
        };
        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync();

        var inventory = accounts.Single(x => x.Code == "1301").Id;
        var sales = accounts.Single(x => x.Code == "4101").Id;
        var cogs = accounts.Single(x => x.Code == "5101").Id;
        db.Items.AddRange(
            new Item { CompanyId = company.Id, Code = "IT-001", Name = "حاسب محمول", Category = "أجهزة", PurchasePrice = 2500, SalePrice = 3200, TaxRate = 15, InventoryAccountId = inventory, SalesAccountId = sales, CostAccountId = cogs },
            new Item { CompanyId = company.Id, Code = "IT-002", Name = "طابعة ليزر", Category = "أجهزة", PurchasePrice = 700, SalePrice = 950, TaxRate = 15, InventoryAccountId = inventory, SalesAccountId = sales, CostAccountId = cogs }
        );
        db.Customers.Add(new Customer { CompanyId = company.Id, Code = "C-1001", Name = "عميل أول" });
        db.Suppliers.Add(new Supplier { CompanyId = company.Id, Code = "S-2001", Name = "مورد أول" });
        db.TaxCodes.Add(new TaxCode { CompanyId = company.Id, Code = "VAT15", Name = "ضريبة القيمة المضافة 15%", Rate = 15, IsSales = true, IsPurchase = true });

        var permissions = new[]
        {
            "dashboard.view","companies.view","companies.create","companies.edit","customers.view","customers.create","customers.edit",
            "suppliers.view","suppliers.create","suppliers.edit","items.view","items.create","items.edit",
            "accounts.view","accounts.create","accounts.edit","sales.view","sales.create","sales.post",
            "purchases.view","purchases.create","purchases.post","journals.view","journals.create","journals.post",
            "inventory.view","inventory.create","payments.view","payments.create","tax.view","tax.create",
            "assets.view","assets.create","costcenters.view","costcenters.create","budgets.view","budgets.create",
            "reports.view","settings.view","users.manage","audit.view"
        };
        foreach (var code in permissions) db.Permissions.Add(new Permission { Code = code, Name = code });
        await db.SaveChangesAsync();

        var adminGroup = new Group { Name = "Administrators" };
        var accountantGroup = new Group { Name = "Accountants" };
        db.Groups.AddRange(adminGroup, accountantGroup);
        await db.SaveChangesAsync();

        var allPermissionEntities = await db.Permissions.ToListAsync();
        db.GroupPermissions.AddRange(allPermissionEntities.Select(p => new GroupPermission { GroupId = adminGroup.Id, PermissionId = p.Id }));
        db.GroupPermissions.AddRange(allPermissionEntities.Where(p => p.Code.EndsWith(".view") || p.Code is "sales.create" or "sales.post" or "purchases.create" or "purchases.post" or "journals.create" or "journals.post" or "payments.create" or "inventory.create" or "reports.view").Select(p => new GroupPermission { GroupId = accountantGroup.Id, PermissionId = p.Id }));

        var admin = new UserAccount { Username = "admin", FullName = "مدير النظام", PasswordHash = PasswordHasher.Hash("admin123") };
        var accountant = new UserAccount { Username = "accountant", FullName = "المحاسب", PasswordHash = PasswordHasher.Hash("123456") };
        db.Users.AddRange(admin, accountant);
        await db.SaveChangesAsync();
        db.UserGroups.AddRange(new UserGroup { UserId = admin.Id, GroupId = adminGroup.Id }, new UserGroup { UserId = accountant.Id, GroupId = accountantGroup.Id });
        await db.SaveChangesAsync();
    }
}
