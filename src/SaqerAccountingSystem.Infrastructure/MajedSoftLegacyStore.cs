using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SaqerAccountingSystem.Infrastructure;

public sealed class MajedSoftLegacyStore
{
    private readonly string _connectionString;

    public MajedSoftLegacyStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("LegacyConnection")
            ?? throw new InvalidOperationException("LegacyConnection is not configured.");
    }

    public async Task<LegacyOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);

        var accounts = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Account_Accounts", cancellationToken);
        var customers = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Account_CustSup WHERE ISNULL(IsCustomers,0)=1", cancellationToken);
        var suppliers = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Account_CustSup WHERE ISNULL(IsSuppliers,0)=1", cancellationToken);
        var items = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Item_Items", cancellationToken);
        var sales = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Order_Orders", cancellationToken);
        var purchases = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Order_Purchases", cancellationToken);
        var journalHeaders = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.Tran_Tran", cancellationToken);
        var users = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.User_Login", cancellationToken);
        var groups = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.User_Groups", cancellationToken);
        var permissions = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.User_Permission", cancellationToken);
        var screens = await ScalarIntAsync(cn, "SELECT COUNT(*) FROM dbo.User_Screens", cancellationToken);

        return new LegacyOverview(accounts, customers, suppliers, items, sales, purchases, journalHeaders, users, groups, permissions, screens);
    }

    public async Task<List<LegacyAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ID,
                Account_No,
                Account_Name,
                E_Account_Name,
                Account_Level,
                Final_Account,
                Account_Type,
                Account_Nature,
                BranchID,
                Priv_Debit,
                Priv_Credit,
                Suspended
            FROM dbo.Account_Accounts
            ORDER BY Account_No, ID
            """;

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<LegacyAccountDto>();
        while (await rd.ReadAsync(cancellationToken))
        {
            result.Add(new LegacyAccountDto(
                GetInt(rd, "ID"),
                GetNullableInt(rd, "Account_No"),
                GetString(rd, "Account_Name"),
                GetString(rd, "E_Account_Name"),
                GetNullableInt(rd, "Account_Level"),
                GetNullableInt(rd, "Final_Account"),
                GetNullableInt(rd, "Account_Type"),
                GetNullableInt(rd, "Account_Nature"),
                GetNullableInt(rd, "BranchID"),
                GetDecimal(rd, "Priv_Debit"),
                GetDecimal(rd, "Priv_Credit"),
                GetNullableInt(rd, "Suspended")));
        }
        return result;
    }

    public async Task<List<LegacyPartyDto>> GetPartiesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ID, CustSuppCode, AccountNo, BranchID, CustSuppName, VatNum, Phone,
                   IsSuppliers, IsCustomers, CreditLimit, AlarmLimit
            FROM dbo.Account_CustSup
            ORDER BY CustSuppCode, ID
            """;

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<LegacyPartyDto>();
        while (await rd.ReadAsync(cancellationToken))
        {
            result.Add(new LegacyPartyDto(
                GetInt(rd, "ID"),
                GetNullableInt(rd, "CustSuppCode"),
                GetNullableInt(rd, "AccountNo"),
                GetNullableInt(rd, "BranchID"),
                GetString(rd, "CustSuppName"),
                GetString(rd, "VatNum"),
                GetString(rd, "Phone"),
                GetBool(rd, "IsCustomers"),
                GetBool(rd, "IsSuppliers"),
                GetDecimal(rd, "CreditLimit"),
                GetDecimal(rd, "AlarmLimit")));
        }
        return result;
    }

    public async Task<List<LegacyItemDto>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ItemId, Item_code, item_Name, item_Name_English, Category_ID, Class_ID,
                   CompanyID, UnitSmall, SellPriceSmall, SellPriceMedium, SellpriceLarge,
                   LastCost, Average_cost, Is_Tax, Tax_Value, VatCode
            FROM dbo.Item_Items
            ORDER BY Item_code, ItemId
            """;

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<LegacyItemDto>();
        while (await rd.ReadAsync(cancellationToken))
        {
            result.Add(new LegacyItemDto(
                GetInt(rd, "ItemId"),
                GetString(rd, "Item_code"),
                GetString(rd, "item_Name"),
                GetString(rd, "item_Name_English"),
                GetNullableInt(rd, "Category_ID"),
                GetNullableInt(rd, "Class_ID"),
                GetNullableInt(rd, "CompanyID"),
                GetNullableInt(rd, "UnitSmall"),
                GetDecimal(rd, "SellPriceSmall"),
                GetDecimal(rd, "SellPriceMedium"),
                GetDecimal(rd, "SellpriceLarge"),
                GetDecimal(rd, "LastCost"),
                GetDecimal(rd, "Average_cost"),
                GetBool(rd, "Is_Tax"),
                GetDecimal(rd, "Tax_Value"),
                GetString(rd, "VatCode")));
        }
        return result;
    }

    public async Task<List<LegacySalesDto>> GetSalesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ID, BranchID, CreditNote, SupplierID, SupplierName, Purchases_Date,
                   TotalPrices, Tax, Net, CashMoney, CashBank, AmountPaid, Rest,
                   UserID_Add, YearId, ProjectId, QRCode, OrderTypeElectronicInvoiceId
            FROM dbo.Order_Orders
            ORDER BY Purchases_Date DESC, ID DESC
            """;

        return await QueryAsync(sql, rd => new LegacySalesDto(
            GetInt(rd, "ID"),
            GetNullableInt(rd, "BranchID"),
            GetNullableInt(rd, "CreditNote"),
            GetNullableInt(rd, "SupplierID"),
            GetString(rd, "SupplierName"),
            GetNullableDateTime(rd, "Purchases_Date"),
            GetDecimal(rd, "TotalPrices"),
            GetDecimal(rd, "Tax"),
            GetDecimal(rd, "Net"),
            GetDecimal(rd, "CashMoney"),
            GetDecimal(rd, "CashBank"),
            GetDecimal(rd, "AmountPaid"),
            GetDecimal(rd, "Rest"),
            GetNullableInt(rd, "UserID_Add"),
            GetNullableInt(rd, "YearId"),
            GetNullableInt(rd, "ProjectId"),
            GetString(rd, "QRCode"),
            GetString(rd, "OrderTypeElectronicInvoiceId")), cancellationToken);
    }

    public async Task<List<LegacyPurchaseDto>> GetPurchasesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ID, BranchID, SupplierID, SupplierName, Purchases_Date,
                   TotalPrices, Tax, Net, Cash, Bank, Acc_Cash, Acc_Bank,
                   UserID_Add, YearId, ProjectId
            FROM dbo.Order_Purchases
            ORDER BY Purchases_Date DESC, ID DESC
            """;

        return await QueryAsync(sql, rd => new LegacyPurchaseDto(
            GetInt(rd, "ID"),
            GetNullableInt(rd, "BranchID"),
            GetNullableInt(rd, "SupplierID"),
            GetString(rd, "SupplierName"),
            GetNullableDateTime(rd, "Purchases_Date"),
            GetDecimal(rd, "TotalPrices"),
            GetDecimal(rd, "Tax"),
            GetDecimal(rd, "Net"),
            GetDecimal(rd, "Cash"),
            GetDecimal(rd, "Bank"),
            GetNullableInt(rd, "Acc_Cash"),
            GetNullableInt(rd, "Acc_Bank"),
            GetNullableInt(rd, "UserID_Add"),
            GetNullableInt(rd, "YearId"),
            GetNullableInt(rd, "ProjectId")), cancellationToken);
    }

    public async Task<List<LegacyJournalDto>> GetJournalsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT t.ID, t.ReferenceCode, t.TranTypeID, t.DocCode, t.TranDate, t.Note,
                   t.BranchID, t.UserID_Add, t.YearId, t.ProjectId,
                   ISNULL(SUM(d.Debit),0) AS Debit,
                   ISNULL(SUM(d.Credit),0) AS Credit
            FROM dbo.Tran_Tran t
            LEFT JOIN dbo.Tran_TranDetails d ON d.TranSn=t.ID
            GROUP BY t.ID, t.ReferenceCode, t.TranTypeID, t.DocCode, t.TranDate,
                     t.Note, t.BranchID, t.UserID_Add, t.YearId, t.ProjectId
            ORDER BY t.TranDate DESC, t.ID DESC
            """;

        return await QueryAsync(sql, rd => new LegacyJournalDto(
            GetInt(rd, "ID"),
            GetNullableInt(rd, "ReferenceCode"),
            GetNullableInt(rd, "TranTypeID"),
            GetString(rd, "DocCode"),
            GetNullableDateTime(rd, "TranDate"),
            GetString(rd, "Note"),
            GetNullableInt(rd, "BranchID"),
            GetNullableInt(rd, "UserID_Add"),
            GetNullableInt(rd, "YearId"),
            GetNullableInt(rd, "ProjectId"),
            GetDecimal(rd, "Debit"),
            GetDecimal(rd, "Credit")), cancellationToken);
    }

    public async Task<LegacyAuthenticationResult?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        const string userSql = """
            SELECT TOP 1 ID, Name, PassWord, BranchID, GroupID, IsActive
            FROM dbo.User_Login
            WHERE Name = @Name
            ORDER BY ID
            """;

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);

        await using var userCmd = new SqlCommand(userSql, cn);
        userCmd.Parameters.AddWithValue("@Name", username.Trim());

        await using var rd = await userCmd.ExecuteReaderAsync(cancellationToken);
        if (!await rd.ReadAsync(cancellationToken))
            return null;

        var user = new LegacyStoredUser(
            GetInt(rd, "ID"),
            GetString(rd, "Name"),
            GetString(rd, "PassWord"),
            GetNullableInt(rd, "BranchID"),
            GetNullableInt(rd, "GroupID"),
            GetBool(rd, "IsActive"));

        if (!user.IsActive || !VerifyLegacyPassword(user.Password, password) || !user.GroupId.HasValue)
            return null;

        await rd.DisposeAsync();

        const string groupSql = "SELECT TOP 1 ID, Name, BranchID FROM dbo.User_Groups WHERE ID=@GroupID";
        await using var groupCmd = new SqlCommand(groupSql, cn);
        groupCmd.Parameters.AddWithValue("@GroupID", user.GroupId.Value);
        await using var groupRd = await groupCmd.ExecuteReaderAsync(cancellationToken);
        if (!await groupRd.ReadAsync(cancellationToken))
            return null;

        var group = new LegacyGroupDto(
            GetInt(groupRd, "ID"),
            GetString(groupRd, "Name"),
            GetNullableInt(groupRd, "BranchID"));

        await groupRd.DisposeAsync();

        const string screensSql = """
            SELECT
                s.ID,
                s.Screen_Name,
                s.ScreenTypeID,
                s.ScreenNum,
                s.ScreenTypeName,
                ISNULL(s.ISShow, 1) AS ISShow,
                p.Allow_Branch,
                p.Allow_Enter,
                p.Allow_Save,
                p.Allow_Edit,
                p.Allow_Delete,
                p.Allow_Print,
                p.Allow_Export
            FROM dbo.User_Screens s
            INNER JOIN dbo.User_Permission p
                ON p.ScreenID = s.ID
               AND p.GroupID = @GroupID
            WHERE ISNULL(s.ISShow, 1) = 1
            ORDER BY s.ScreenNum, s.ID
            """;

        var screens = new List<LegacyScreenAccessDto>();
        await using var screenCmd = new SqlCommand(screensSql, cn);
        screenCmd.Parameters.AddWithValue("@GroupID", user.GroupId.Value);
        await using var screenRd = await screenCmd.ExecuteReaderAsync(cancellationToken);

        while (await screenRd.ReadAsync(cancellationToken))
        {
            screens.Add(new LegacyScreenAccessDto(
                GetInt(screenRd, "ID"),
                GetString(screenRd, "Screen_Name"),
                GetNullableInt(screenRd, "ScreenTypeID"),
                GetNullableInt(screenRd, "ScreenNum"),
                GetString(screenRd, "ScreenTypeName"),
                GetBool(screenRd, "ISShow"),
                GetBool(screenRd, "Allow_Branch"),
                GetBool(screenRd, "Allow_Enter"),
                GetBool(screenRd, "Allow_Save"),
                GetBool(screenRd, "Allow_Edit"),
                GetBool(screenRd, "Allow_Delete"),
                GetBool(screenRd, "Allow_Print"),
                GetBool(screenRd, "Allow_Export")));
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var screen in screens)
            AddCanonicalPermissions(permissions, screen);

        var userDto = new LegacyUserDto(user.Id, user.Name, user.BranchId, user.GroupId, user.IsActive);
        return new LegacyAuthenticationResult(userDto, group, screens, permissions);
    }

    private static bool VerifyLegacyPassword(string stored, string supplied)
    {
        if (string.Equals(stored, supplied, StringComparison.Ordinal))
            return true;

        if (string.IsNullOrWhiteSpace(stored))
            return false;

        var input = Encoding.UTF8.GetBytes(supplied);

        if (stored.Length == 32)
        {
            var md5 = Convert.ToHexString(MD5.HashData(input));
            if (string.Equals(stored, md5, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (stored.Length == 64)
        {
            var sha256 = Convert.ToHexString(SHA256.HashData(input));
            if (string.Equals(stored, sha256, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var base64Sha256 = Convert.ToBase64String(SHA256.HashData(input));
        if (string.Equals(stored, base64Sha256, StringComparison.Ordinal))
            return true;

        if (TryVerifyBase64Digest(stored, MD5.HashData(input)))
            return true;

        var unicodeInput = Encoding.Unicode.GetBytes(supplied);
        if (TryVerifyBase64Digest(stored, MD5.HashData(unicodeInput)))
            return true;

        return TryVerifyBase64Digest(stored, SHA256.HashData(unicodeInput));
    }

    private static bool TryVerifyBase64Digest(string stored, byte[] digest)
    {
        try
        {
            var encoded = Convert.ToBase64String(digest);
            return string.Equals(stored, encoded, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static void AddCanonicalPermissions(HashSet<string> target, LegacyScreenAccessDto screen)
    {
        foreach (var raw in RawPermissionCodes(screen))
            target.Add(raw);

        var key = ResolveModuleKey(screen.Name);
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (screen.AllowEnter)
            target.Add(key + ".view");
        if (screen.AllowSave)
            target.Add(key + ".create");
        if (screen.AllowEdit)
            target.Add(key + ".edit");
        if (screen.AllowDelete)
            target.Add(key + ".delete");
        if (screen.AllowPrint)
            target.Add(key + ".print");
        if (screen.AllowExport)
            target.Add(key + ".export");
    }

    private static IEnumerable<string> RawPermissionCodes(LegacyScreenAccessDto screen)
    {
        var prefix = "legacy.screen." + screen.ScreenId + ".";
        if (screen.AllowBranch) yield return prefix + "branch";
        if (screen.AllowEnter) yield return prefix + "enter";
        if (screen.AllowSave) yield return prefix + "save";
        if (screen.AllowEdit) yield return prefix + "edit";
        if (screen.AllowDelete) yield return prefix + "delete";
        if (screen.AllowPrint) yield return prefix + "print";
        if (screen.AllowExport) yield return prefix + "export";
    }

    private static string ResolveModuleKey(string? screenName)
    {
        var name = (screenName ?? string.Empty).Trim().ToLowerInvariant();

        if (ContainsAny(name, "لوحة", "الرئيسية", "dashboard", "main")) return "dashboard";
        if (ContainsAny(name, "شركة", "شركات", "فرع", "فروع", "company", "branch")) return "companies";
        if (ContainsAny(name, "عميل", "عملاء", "customer")) return "customers";
        if (ContainsAny(name, "مورد", "موردين", "مورّد", "supplier")) return "suppliers";
        if (ContainsAny(name, "صنف", "أصناف", "مادة", "مواد", "item", "product")) return "items";
        if (ContainsAny(name, "مبيع", "مبيعات", "فاتورة بيع", "sales", "sale")) return "sales";
        if (ContainsAny(name, "شراء", "مشتريات", "فاتورة شراء", "purchase")) return "purchases";
        if (ContainsAny(name, "حساب", "الحسابات", "دليل", "account", "chart")) return "accounts";
        if (ContainsAny(name, "قيد", "قيود", "يومية", "journal", "ledger")) return "journals";
        if (ContainsAny(name, "صندوق", "بنك", "قبض", "صرف", "سداد", "payment", "cash", "bank")) return "payments";
        if (ContainsAny(name, "مخزون", "مستودع", "مخازن", "حركة مخزون", "inventory", "stock", "warehouse")) return "inventory";
        if (ContainsAny(name, "ضريبة", "ضريبي", "vat", "tax")) return "tax";
        if (ContainsAny(name, "أصل ثابت", "أصول ثابتة", "fixed asset", "asset")) return "assets";
        if (ContainsAny(name, "مركز تكلفة", "مراكز التكلفة", "cost center", "costcenter")) return "costcenters";
        if (ContainsAny(name, "موازنة", "موازنات", "ميزانية تقديرية", "budget")) return "budgets";
        if (ContainsAny(name, "تقرير", "تقارير", "ميزان مراجعة", "قائمة دخل", "balance sheet", "income statement", "report")) return "reports";
        if (ContainsAny(name, "إعداد", "اعداد", "settings", "system")) return "settings";
        return "";
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(value.Contains);

    public async Task<LegacySecurityDto> GetSecurityAsync(CancellationToken cancellationToken = default)
    {
        const string usersSql = "SELECT ID, Name, BranchID, GroupID, IsActive FROM dbo.User_Login ORDER BY ID";
        const string groupsSql = "SELECT ID, Name, BranchID FROM dbo.User_Groups ORDER BY ID";
        const string screensSql = "SELECT ID, Screen_Name, ScreenTypeID, ScreenNum, ScreenTypeName, ISShow FROM dbo.User_Screens ORDER BY ID";
        const string permissionsSql = """
            SELECT ID, ScreenID, GroupID, Allow_Branch, Allow_Enter, Allow_Save,
                   Allow_Edit, Allow_Delete, Allow_Print, Allow_Export
            FROM dbo.User_Permission
            ORDER BY GroupID, ScreenID, ID
            """;

        var users = await QueryAsync(usersSql, rd => new LegacyUserDto(
            GetInt(rd, "ID"), GetString(rd, "Name"), GetNullableInt(rd, "BranchID"),
            GetNullableInt(rd, "GroupID"), GetBool(rd, "IsActive")), cancellationToken);

        var groups = await QueryAsync(groupsSql, rd => new LegacyGroupDto(
            GetInt(rd, "ID"), GetString(rd, "Name"), GetNullableInt(rd, "BranchID")), cancellationToken);

        var screens = await QueryAsync(screensSql, rd => new LegacyScreenDto(
            GetInt(rd, "ID"), GetString(rd, "Screen_Name"), GetNullableInt(rd, "ScreenTypeID"),
            GetNullableInt(rd, "ScreenNum"), GetString(rd, "ScreenTypeName"), GetBool(rd, "ISShow")), cancellationToken);

        var permissions = await QueryAsync(permissionsSql, rd => new LegacyPermissionDto(
            GetInt(rd, "ID"), GetNullableInt(rd, "ScreenID"), GetNullableInt(rd, "GroupID"),
            GetBool(rd, "Allow_Branch"), GetBool(rd, "Allow_Enter"), GetBool(rd, "Allow_Save"),
            GetBool(rd, "Allow_Edit"), GetBool(rd, "Allow_Delete"), GetBool(rd, "Allow_Print"),
            GetBool(rd, "Allow_Export")), cancellationToken);

        return new LegacySecurityDto(users, groups, screens, permissions);
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader,T> map, CancellationToken cancellationToken)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken);
        var result = new List<T>();
        while (await rd.ReadAsync(cancellationToken)) result.Add(map(rd));
        return result;
    }

    private static async Task<int> ScalarIntAsync(SqlConnection cn, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(sql, cn);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static string GetString(SqlDataReader rd, string name)
        => rd[name] == DBNull.Value ? "" : Convert.ToString(rd[name]) ?? "";

    private static int GetInt(SqlDataReader rd, string name)
        => rd[name] == DBNull.Value ? 0 : Convert.ToInt32(rd[name]);

    private static int? GetNullableInt(SqlDataReader rd, string name)
        => rd[name] == DBNull.Value ? null : Convert.ToInt32(rd[name]);

    private static decimal GetDecimal(SqlDataReader rd, string name)
        => rd[name] == DBNull.Value ? 0m : Convert.ToDecimal(rd[name]);

    private static bool GetBool(SqlDataReader rd, string name)
        => rd[name] != DBNull.Value && Convert.ToBoolean(rd[name]);

    private static DateTime? GetNullableDateTime(SqlDataReader rd, string name)
        => rd[name] == DBNull.Value ? null : Convert.ToDateTime(rd[name]);
}

public sealed record LegacyScreenAccessDto(
    int ScreenId,
    string Name,
    int? ScreenTypeId,
    int? ScreenNum,
    string ScreenTypeName,
    bool IsShow,
    bool AllowBranch,
    bool AllowEnter,
    bool AllowSave,
    bool AllowEdit,
    bool AllowDelete,
    bool AllowPrint,
    bool AllowExport);

public sealed record LegacyAuthenticationResult(
    LegacyUserDto User,
    LegacyGroupDto Group,
    List<LegacyScreenAccessDto> Screens,
    HashSet<string> Permissions);

internal sealed record LegacyStoredUser(
    int Id,
    string Name,
    string Password,
    int? BranchId,
    int? GroupId,
    bool IsActive);

public sealed record LegacyOverview(int Accounts, int Customers, int Suppliers, int Items, int Sales, int Purchases, int JournalHeaders, int Users, int Groups, int Permissions, int Screens);
public sealed record LegacyAccountDto(int Id, int? AccountNo, string Name, string EnglishName, int? Level, int? FinalAccount, int? AccountType, int? Nature, int? BranchId, decimal PrivDebit, decimal PrivCredit, int? Suspended);
public sealed record LegacyPartyDto(int Id, int? Code, int? AccountNo, int? BranchId, string Name, string VatNumber, string Phone, bool IsCustomer, bool IsSupplier, decimal CreditLimit, decimal AlarmLimit);
public sealed record LegacyItemDto(int Id, string Code, string Name, string EnglishName, int? CategoryId, int? ClassId, int? CompanyId, int? UnitSmall, decimal SellPriceSmall, decimal SellPriceMedium, decimal SellPriceLarge, decimal LastCost, decimal AverageCost, bool IsTax, decimal TaxValue, string VatCode);
public sealed record LegacySalesDto(int Id, int? BranchId, int? CreditNote, int? SupplierId, string PartyName, DateTime? Date, decimal TotalPrices, decimal Tax, decimal Net, decimal Cash, decimal Bank, decimal Paid, decimal Rest, int? UserId, int? YearId, int? ProjectId, string QrCode, string ElectronicInvoiceType);
public sealed record LegacyPurchaseDto(int Id, int? BranchId, int? SupplierId, string SupplierName, DateTime? Date, decimal TotalPrices, decimal Tax, decimal Net, decimal Cash, decimal Bank, int? CashAccount, int? BankAccount, int? UserId, int? YearId, int? ProjectId);
public sealed record LegacyJournalDto(int Id, int? ReferenceCode, int? TypeId, string DocCode, DateTime? Date, string Note, int? BranchId, int? UserId, int? YearId, int? ProjectId, decimal Debit, decimal Credit);
public sealed record LegacyUserDto(int Id, string Name, int? BranchId, int? GroupId, bool IsActive);
public sealed record LegacyGroupDto(int Id, string Name, int? BranchId);
public sealed record LegacyScreenDto(int Id, string Name, int? ScreenTypeId, int? ScreenNum, string ScreenTypeName, bool IsShow);
public sealed record LegacyPermissionDto(int Id, int? ScreenId, int? GroupId, bool AllowBranch, bool AllowEnter, bool AllowSave, bool AllowEdit, bool AllowDelete, bool AllowPrint, bool AllowExport);
public sealed record LegacySecurityDto(List<LegacyUserDto> Users, List<LegacyGroupDto> Groups, List<LegacyScreenDto> Screens, List<LegacyPermissionDto> Permissions);
