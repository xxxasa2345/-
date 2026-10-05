using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SaqerAccountingSystem.Infrastructure;

public sealed class MajedSoftLegacyWriteStore
{
    private readonly string _connectionString;

    public MajedSoftLegacyWriteStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("LegacyConnection")
            ?? throw new InvalidOperationException("LegacyConnection is not configured.");
    }

    public async Task<LegacyWriteResult> CreateJournalAsync(
        LegacyJournalWriteRequest request,
        LegacyUserDto actor,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
            throw new ArgumentException("القيد يحتاج إلى سطور.");

        ValidateLines(request.Lines);

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var branchId = actor.BranchId ?? 0;
            if (request.BranchId.HasValue && request.BranchId.Value != branchId)
                throw new InvalidOperationException("لا يمكن إنشاء قيد خارج فرع المستخدم.");

            var tranId = await NextIdAsync(cn, tx, "Tran_Tran", "ID", cancellationToken);
            var docCode = string.IsNullOrWhiteSpace(request.DocCode)
                ? $"JE-{DateTime.Now:yyyyMMddHHmmssfff}"
                : request.DocCode.Trim();

            await ExecuteAsync(cn, tx, """
                INSERT INTO dbo.Tran_Tran
                    (ID, ReferenceCode, TranTypeID, DocCode, TranDate, Note,
                     BranchID, UserID_Add, UserBranch_Add, UserDate_Add, ProjectId, YearId)
                VALUES
                    (@ID, @ReferenceCode, NULL, @DocCode, @TranDate, @Note,
                     @BranchID, @UserID, @UserBranch, @UserDate, @ProjectId, @YearId)
                """,
                cancellationToken,
                ("@ID", tranId),
                ("@ReferenceCode", request.ReferenceCode),
                ("@DocCode", docCode),
                ("@TranDate", request.Date.Date),
                ("@Note", request.Note ?? ""),
                ("@BranchID", branchId),
                ("@UserID", actor.Id),
                ("@UserBranch", branchId),
                ("@UserDate", DateTime.Now),
                ("@ProjectId", request.ProjectId),
                ("@YearId", request.YearId));

            await InsertJournalLinesAsync(cn, tx, tranId, branchId, request.ProjectId, request.YearId, request.Lines, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new LegacyWriteResult(tranId, docCode, "journal");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LegacyWriteResult> CreateSaleAsync(
        LegacySaleWriteRequest request,
        LegacyUserDto actor,
        CancellationToken cancellationToken = default)
    {
        ValidateInvoiceLines(request.Lines);
        if (request.SalesAccountId <= 0)
            throw new ArgumentException("حساب المبيعات مطلوب.");
        if (request.VatAccountId <= 0 && request.Lines.Any(x => x.VatRate > 0))
            throw new ArgumentException("حساب ضريبة المخرجات مطلوب.");

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var branchId = ResolveBranch(actor, request.BranchId);
            var party = request.CustomerId.HasValue
                ? await FindPartyAsync(cn, tx, request.CustomerId.Value, true, cancellationToken)
                : null;

            var lines = await CalculateLinesAsync(cn, tx, request.Lines, branchId, cancellationToken);
            var subtotal = Math.Round(lines.Sum(x => x.TotalPrice), 2);
            var tax = Math.Round(lines.Sum(x => x.Vat), 2);
            var net = Math.Round(subtotal + tax, 2);
            var invoiceId = await NextIdAsync(cn, tx, "Order_Orders", "ID", cancellationToken);
            var invoiceCode = await NextBranchCodeAsync(cn, tx, "Order_Orders", branchId, cancellationToken);

            await ExecuteAsync(cn, tx, """
                INSERT INTO dbo.Order_Orders
                    (ID, PurBranchID, BranchID, SupplierID, SupplierName, SupplierVatNum,
                     Purchases_Date, Order_Paymant_Type, CostCentersID, ProjectId, YearId,
                     Note, IsWaiting, TotalPrices, Tax, Net, CashMoney, CashBank,
                     UserID_Add, UserBranch_Add, UserDate_Add)
                VALUES
                    (@ID, @PurBranchID, @BranchID, @SupplierID, @SupplierName, @SupplierVatNum,
                     @Date, @PaymentType, @CostCenter, @ProjectId, @YearId,
                     @Note, 0, @Subtotal, @Tax, @Net, @CashMoney, @CashBank,
                     @UserID, @UserBranch, @UserDate)
                """,
                cancellationToken,
                ("@ID", invoiceId),
                ("@PurBranchID", invoiceCode),
                ("@BranchID", branchId),
                ("@SupplierID", request.CustomerId),
                ("@SupplierName", party?.Name ?? request.CustomerName ?? ""),
                ("@SupplierVatNum", party?.VatNumber ?? ""),
                ("@Date", request.Date.Date),
                ("@PaymentType", request.PaymentType),
                ("@CostCenter", request.CostCenterId),
                ("@ProjectId", request.ProjectId),
                ("@YearId", request.YearId),
                ("@Note", request.Note ?? ""),
                ("@Subtotal", subtotal),
                ("@Tax", tax),
                ("@Net", net),
                ("@CashMoney", request.PaymentType == 1 ? net : 0m),
                ("@CashBank", request.PaymentType == 3 ? net : 0m),
                ("@UserID", actor.Id),
                ("@UserBranch", branchId),
                ("@UserDate", DateTime.Now));

            await InsertSaleLinesAsync(cn, tx, invoiceId, branchId, lines, cancellationToken);
            await ApplyStockAsync(cn, tx, lines, increase: false, cancellationToken);

            var debitAccount = request.PaymentType switch
            {
                1 or 3 when request.DebitAccountId > 0 => request.DebitAccountId,
                _ => party?.AccountId ?? 0
            };
            if (debitAccount <= 0)
                throw new InvalidOperationException("تعذر تحديد الحساب المدين للعميل/الصندوق.");

            var journalLines = new List<LegacyJournalLineRequest>
            {
                new(debitAccount, net, 0m, $"فاتورة مبيعات {invoiceCode}", request.CostCenterId, request.ProjectId),
                new(request.SalesAccountId, 0m, subtotal, $"مبيعات {invoiceCode}", request.CostCenterId, request.ProjectId)
            };
            if (tax > 0)
                journalLines.Add(new(request.VatAccountId, 0m, tax, $"ضريبة مخرجات {invoiceCode}", request.CostCenterId, request.ProjectId));

            var journal = await InsertJournalForDocumentAsync(
                cn, tx, branchId, actor.Id, invoiceId, invoiceCode, request.Date, $"ترحيل فاتورة مبيعات {invoiceCode}",
                request.ProjectId, request.YearId, journalLines, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new LegacyWriteResult(invoiceId, invoiceCode.ToString(), "sale", journal);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LegacyWriteResult> CreatePurchaseAsync(
        LegacyPurchaseWriteRequest request,
        LegacyUserDto actor,
        CancellationToken cancellationToken = default)
    {
        ValidateInvoiceLines(request.Lines);
        if (request.InventoryAccountId <= 0)
            throw new ArgumentException("حساب المخزون/المشتريات مطلوب.");
        if (request.VatAccountId <= 0 && request.Lines.Any(x => x.VatRate > 0))
            throw new ArgumentException("حساب ضريبة المدخلات مطلوب.");

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var branchId = ResolveBranch(actor, request.BranchId);
            var party = request.SupplierId.HasValue
                ? await FindPartyAsync(cn, tx, request.SupplierId.Value, false, cancellationToken)
                : null;

            var lines = await CalculateLinesAsync(cn, tx, request.Lines, branchId, cancellationToken);
            var subtotal = Math.Round(lines.Sum(x => x.TotalPrice), 2);
            var tax = Math.Round(lines.Sum(x => x.Vat), 2);
            var net = Math.Round(subtotal + tax, 2);
            var invoiceId = await NextIdAsync(cn, tx, "Order_Purchases", "ID", cancellationToken);
            var invoiceCode = await NextBranchCodeAsync(cn, tx, "Order_Purchases", branchId, cancellationToken);

            await ExecuteAsync(cn, tx, """
                INSERT INTO dbo.Order_Purchases
                    (ID, PurBranchID, BranchID, SupplierID, SupplierName, SupplierVatNum,
                     Purchases_Date, Order_Paymant_Type, CostCentersID, ProjectId, YearId,
                     Note, TotalPrices, Tax, Net, Cash, Bank, Acc_Cash, Acc_Bank,
                     UserID_Add, UserBranch_Add, UserDate_Add)
                VALUES
                    (@ID, @PurBranchID, @BranchID, @SupplierID, @SupplierName, @SupplierVatNum,
                     @Date, @PaymentType, @CostCenter, @ProjectId, @YearId,
                     @Note, @Subtotal, @Tax, @Net, @Cash, @Bank, @CashAccount, @BankAccount,
                     @UserID, @UserBranch, @UserDate)
                """,
                cancellationToken,
                ("@ID", invoiceId),
                ("@PurBranchID", invoiceCode),
                ("@BranchID", branchId),
                ("@SupplierID", request.SupplierId),
                ("@SupplierName", party?.Name ?? request.SupplierName ?? ""),
                ("@SupplierVatNum", party?.VatNumber ?? ""),
                ("@Date", request.Date.Date),
                ("@PaymentType", request.PaymentType),
                ("@CostCenter", request.CostCenterId),
                ("@ProjectId", request.ProjectId),
                ("@YearId", request.YearId),
                ("@Note", request.Note ?? ""),
                ("@Subtotal", subtotal),
                ("@Tax", tax),
                ("@Net", net),
                ("@Cash", request.PaymentType == 1 ? net : 0m),
                ("@Bank", request.PaymentType == 3 ? net : 0m),
                ("@CashAccount", request.PaymentType == 1 ? request.CreditAccountId : null),
                ("@BankAccount", request.PaymentType == 3 ? request.CreditAccountId : null),
                ("@UserID", actor.Id),
                ("@UserBranch", branchId),
                ("@UserDate", DateTime.Now));

            await InsertPurchaseLinesAsync(cn, tx, invoiceId, branchId, lines, cancellationToken);
            await ApplyStockAsync(cn, tx, lines, increase: true, cancellationToken);

            var creditAccount = request.PaymentType switch
            {
                1 or 3 when request.CreditAccountId > 0 => request.CreditAccountId,
                _ => party?.AccountId ?? 0
            };
            if (creditAccount <= 0)
                throw new InvalidOperationException("تعذر تحديد الحساب الدائن للمورد/الصندوق.");

            var journalLines = new List<LegacyJournalLineRequest>
            {
                new(request.InventoryAccountId, subtotal, 0m, $"مشتريات {invoiceCode}", request.CostCenterId, request.ProjectId),
                new(creditAccount, 0m, net, $"فاتورة مشتريات {invoiceCode}", request.CostCenterId, request.ProjectId)
            };
            if (tax > 0)
                journalLines.Add(new(request.VatAccountId, tax, 0m, $"ضريبة مدخلات {invoiceCode}", request.CostCenterId, request.ProjectId));

            var journal = await InsertJournalForDocumentAsync(
                cn, tx, branchId, actor.Id, invoiceId, invoiceCode, request.Date, $"ترحيل فاتورة مشتريات {invoiceCode}",
                request.ProjectId, request.YearId, journalLines, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new LegacyWriteResult(invoiceId, invoiceCode.ToString(), "purchase", journal);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<LegacyPartyAccount?> FindPartyAsync(
        SqlConnection cn, SqlTransaction tx, int partyId, bool customer, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP 1 p.CustSuppName, p.VatNum, a.ID
            FROM dbo.Account_CustSup p
            LEFT JOIN dbo.Account_Accounts a ON a.Account_No = p.AccountNo
            WHERE p.ID=@ID AND ((@Customer=1 AND ISNULL(p.IsCustomers,0)=1) OR (@Customer=0 AND ISNULL(p.IsSuppliers,0)=1))
            """;

        await using var cmd = new SqlCommand(sql, cn, tx);
        cmd.Parameters.AddWithValue("@ID", partyId);
        cmd.Parameters.AddWithValue("@Customer", customer ? 1 : 0);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct))
            throw new InvalidOperationException("الطرف المحدد غير موجود أو ليس من نوع الطرف المطلوب.");

        return new LegacyPartyAccount(
            rd.IsDBNull(0) ? "" : rd.GetString(0),
            rd.IsDBNull(1) ? "" : rd.GetString(1),
            rd.IsDBNull(2) ? 0 : rd.GetInt32(2));
    }

    private async Task<List<CalculatedLegacyLine>> CalculateLinesAsync(
        SqlConnection cn, SqlTransaction tx, List<LegacyInvoiceLineRequest> requestLines,
        int branchId, CancellationToken ct)
    {
        var result = new List<CalculatedLegacyLine>();
        foreach (var line in requestLines)
        {
            if (line.ItemId <= 0 || line.Quantity <= 0 || line.UnitPrice < 0)
                throw new ArgumentException("بيانات سطر الفاتورة غير صحيحة.");

            const string itemSql = """
                SELECT ItemId, item_Name, LastCost, ISNULL(UnitSmall,0),
                       ISNULL(Is_Tax,0), ISNULL(Tax_Value,0),
                       ISNULL(ConvertMediumUnit,1), ISNULL(ConvertBigUnit,1)
                FROM dbo.Item_Items
                WHERE ItemId=@ItemID
                """;

            await using var itemCmd = new SqlCommand(itemSql, cn, tx);
            itemCmd.Parameters.AddWithValue("@ItemID", line.ItemId);
            await using var rd = await itemCmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct))
                throw new InvalidOperationException($"الصنف {line.ItemId} غير موجود.");

            var name = rd.IsDBNull(1) ? "" : rd.GetString(1);
            var lastCost = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2);
            var defaultTax = rd.IsDBNull(4) && rd.IsDBNull(5)
                ? 0m
                : (rd.IsDBNull(5) ? 0m : rd.GetDecimal(5));
            var taxRate = line.VatRate > 0 ? line.VatRate : (rd.IsDBNull(4) || !rd.GetBoolean(4) ? 0m : defaultTax);
            var defaultUnit = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
            var mediumFactor = rd.IsDBNull(6) ? 1m : rd.GetDecimal(6);
            var bigFactor = rd.IsDBNull(7) ? 1m : rd.GetDecimal(7);
            await rd.DisposeAsync();

            var discount = Math.Max(0m, line.Discount);
            var totalPrice = Math.Round(Math.Max(0m, line.Quantity * line.UnitPrice - discount), 2);
            var vat = Math.Round(totalPrice * taxRate / 100m, 2);
            var netUnit = line.Quantity == 0 ? 0m : Math.Round(totalPrice / line.Quantity, 2);

            result.Add(new CalculatedLegacyLine(
                line.ItemId, name, line.StoreId > 0 ? line.StoreId : 1,
                line.UnitId > 0 ? line.UnitId : defaultUnit,
                line.UnitType ?? "الصغرى",
                line.Quantity, line.UnitPrice, discount, totalPrice, vat, netUnit,
                lastCost, mediumFactor, bigFactor));
        }
        return result;
    }

    private static void ValidateInvoiceLines(List<LegacyInvoiceLineRequest> lines)
    {
        if (lines.Count == 0)
            throw new ArgumentException("الفاتورة يجب أن تحتوي على صنف واحد على الأقل.");
        if (lines.Count > 500)
            throw new ArgumentException("عدد سطور الفاتورة يتجاوز الحد المسموح.");
    }

    private static void ValidateLines(List<LegacyJournalLineRequest> lines)
    {
        var debit = Math.Round(lines.Sum(x => x.Debit), 2);
        var credit = Math.Round(lines.Sum(x => x.Credit), 2);
        if (lines.Any(x => x.AccountId <= 0 || x.Debit < 0 || x.Credit < 0 || (x.Debit > 0 && x.Credit > 0)))
            throw new ArgumentException("كل سطر يجب أن يحتوي على حساب ومدين أو دائن فقط.");
        if (debit <= 0 || Math.Abs(debit - credit) > 0.01m)
            throw new ArgumentException($"القيد غير متوازن. المدين={debit:N2} والدائن={credit:N2}.");
    }

    private async Task InsertSaleLinesAsync(SqlConnection cn, SqlTransaction tx, int invoiceId, int branchId, List<CalculatedLegacyLine> lines, CancellationToken ct)
    {
        var sn = await NextIdAsync(cn, tx, "Order_OrdersDetails", "Sn", ct);
        foreach (var line in lines)
        {
            await ExecuteAsync(cn, tx, """
                INSERT INTO dbo.Order_OrdersDetails
                    (Sn, Purchese_ID, ItemID, BranchID, StoreID, ItemUnitID, Quantity,
                     LastCost, SmallUnitPrice, UnitPrice, TotalPrice, VAT,
                     NetUnitPrice, NetTotalPrice, ItemUnitType)
                VALUES
                    (@SN, @InvoiceID, @ItemID, @BranchID, @StoreID, @UnitID, @Quantity,
                     @LastCost, @SmallUnitPrice, @UnitPrice, @TotalPrice, @VAT,
                     @NetUnitPrice, @NetTotalPrice, @UnitType)
                """,
                ct,
                ("@SN", sn++),
                ("@InvoiceID", invoiceId),
                ("@ItemID", line.ItemId),
                ("@BranchID", branchId),
                ("@StoreID", line.StoreId),
                ("@UnitID", line.UnitId),
                ("@Quantity", line.Quantity),
                ("@LastCost", line.LastCost),
                ("@SmallUnitPrice", line.UnitPrice),
                ("@UnitPrice", line.UnitPrice),
                ("@TotalPrice", line.TotalPrice),
                ("@VAT", line.Vat),
                ("@NetUnitPrice", line.NetUnitPrice),
                ("@NetTotalPrice", line.TotalPrice + line.Vat),
                ("@UnitType", line.UnitType));
        }
    }

    private async Task InsertPurchaseLinesAsync(SqlConnection cn, SqlTransaction tx, int invoiceId, int branchId, List<CalculatedLegacyLine> lines, CancellationToken ct)
    {
        var sn = await NextIdAsync(cn, tx, "Order_PurchasesDetails", "SN", ct);
        foreach (var line in lines)
        {
            await ExecuteAsync(cn, tx, """
                INSERT INTO dbo.Order_PurchasesDetails
                    (SN, Purchese_ID, ItemID, BranchID, StoreID, ItemUnitID, Quantity,
                     UnitPrice, TotalPrice, VAT, NetUnitPrice, NetTotalPrice)
                VALUES
                    (@SN, @InvoiceID, @ItemID, @BranchID, @StoreID, @UnitID, @Quantity,
                     @UnitPrice, @TotalPrice, @VAT, @NetUnitPrice, @NetTotalPrice)
                """,
                ct,
                ("@SN", sn++),
                ("@InvoiceID", invoiceId),
                ("@ItemID", line.ItemId),
                ("@BranchID", branchId),
                ("@StoreID", line.StoreId),
                ("@UnitID", line.UnitId),
                ("@Quantity", line.Quantity),
                ("@UnitPrice", line.UnitPrice),
                ("@TotalPrice", line.TotalPrice),
                ("@VAT", line.Vat),
                ("@NetUnitPrice", line.NetUnitPrice),
                ("@NetTotalPrice", line.TotalPrice + line.Vat));
        }
    }

    private async Task ApplyStockAsync(
        SqlConnection cn, SqlTransaction tx, List<CalculatedLegacyLine> lines, bool increase, CancellationToken ct)
    {
        foreach (var line in lines)
        {
            if (line.StoreId <= 0)
                continue;

            var factor = line.UnitType switch
            {
                "المتوسطة" => line.MediumFactor,
                "الكبرى" => line.MediumFactor * line.BigFactor,
                _ => 1m
            };
            var baseQuantity = Math.Round(line.Quantity * factor, 6);
            var delta = increase ? baseQuantity : -baseQuantity;

            const string itemTypeSql = "SELECT ISNULL(item_Type,1) FROM dbo.Item_Items WHERE ItemId=@ItemID";
            var itemType = await ScalarIntAsync(cn, tx, itemTypeSql, ct, ("@ItemID", line.ItemId));

            if (itemType is not (1 or 3))
                continue;

            var affected = await ExecuteAsync(cn, tx, """
                UPDATE dbo.ItemQuantities
                SET CurrentBalance = ISNULL(CurrentBalance,0) + @Delta,
                    UnitNumber = ISNULL(UnitNumber,0) + @UnitNumber
                WHERE ItemID=@ItemID AND StoreID=@StoreID
                """,
                ct,
                ("@Delta", delta),
                ("@UnitNumber", increase ? line.Quantity : -line.Quantity),
                ("@ItemID", line.ItemId),
                ("@StoreID", line.StoreId));

            if (affected == 0)
            {
                if (!increase)
                    throw new InvalidOperationException($"لا توجد كمية مسجلة للصنف {line.ItemId} في المخزن {line.StoreId}.");

                var newId = await NextIdAsync(cn, tx, "ItemQuantities", "ItemQuantityID", ct);
                await ExecuteAsync(cn, tx, """
                    INSERT INTO dbo.ItemQuantities
                        (ItemQuantityID, ItemID, CurrentBalance, UnitNumber,
                         BeginningInventory, BeginningInventoryPrice, OpeningBalance, StoreID)
                    VALUES
                        (@ID, @ItemID, @Balance, @UnitNumber, 0, 0, 0, @StoreID)
                    """,
                    ct,
                    ("@ID", newId),
                    ("@ItemID", line.ItemId),
                    ("@Balance", baseQuantity),
                    ("@UnitNumber", line.Quantity),
                    ("@StoreID", line.StoreId));
                continue;
            }

            if (!increase)
            {
                var balance = await ScalarDecimalAsync(cn, tx,
                    "SELECT ISNULL(CurrentBalance,0) FROM dbo.ItemQuantities WHERE ItemID=@ItemID AND StoreID=@StoreID",
                    ct,
                    ("@ItemID", line.ItemId),
                    ("@StoreID", line.StoreId));
                if (balance < -0.000001m)
                    throw new InvalidOperationException($"المخزون غير كافٍ للصنف {line.ItemId} في المخزن {line.StoreId}.");
            }
        }
    }

    private static async Task InsertJournalLinesAsync(
        SqlConnection cn, SqlTransaction tx, int tranId, int branchId, int? projectId, int? yearId,
        List<LegacyJournalLineRequest> lines, CancellationToken ct)
    {
        var index = 0;
        foreach (var line in lines)
        {
            await ExecuteAsync(cn, tx, """
                INSERT INTO dbo.Tran_TranDetails
                    (TranSn, Account_Sn, AccounIindex, TranDesc, Debit, Credit,
                     CostCentersID, BranchID, ProjectId, YearId)
                VALUES
                    (@TranSn, @Account, @Index, @Description, @Debit, @Credit,
                     @CostCenter, @BranchID, @ProjectId, @YearId)
                """,
                ct,
                ("@TranSn", tranId),
                ("@Account", line.AccountId),
                ("@Index", index++),
                ("@Description", line.Description ?? ""),
                ("@Debit", line.Debit),
                ("@Credit", line.Credit),
                ("@CostCenter", line.CostCenterId),
                ("@BranchID", branchId),
                ("@ProjectId", line.ProjectId ?? projectId),
                ("@YearId", yearId));
        }
    }

    private async Task<LegacyPostedJournal?> InsertJournalForDocumentAsync(
        SqlConnection cn, SqlTransaction tx, int branchId, int userId, int referenceCode, int documentCode,
        DateTime date, string note, int? projectId, int? yearId, List<LegacyJournalLineRequest> lines, CancellationToken ct)
    {
        ValidateLines(lines);

        var tranId = await NextIdAsync(cn, tx, "Tran_Tran", "ID", ct);
        await ExecuteAsync(cn, tx, """
            INSERT INTO dbo.Tran_Tran
                (ID, ReferenceCode, TranTypeID, DocCode, TranDate, Note, BranchID,
                 UserID_Add, UserBranch_Add, UserDate_Add, ProjectId, YearId)
            VALUES
                (@ID, @ReferenceCode, NULL, @DocCode, @TranDate, @Note, @BranchID,
                 @UserID, @UserBranch, @UserDate, @ProjectId, @YearId)
            """,
            ct,
            ("@ID", tranId),
            ("@ReferenceCode", referenceCode),
            ("@DocCode", documentCode.ToString()),
            ("@TranDate", date.Date),
            ("@Note", note),
            ("@BranchID", branchId),
            ("@UserID", userId),
            ("@UserBranch", branchId),
            ("@UserDate", DateTime.Now),
            ("@ProjectId", projectId),
            ("@YearId", yearId));

        await InsertJournalLinesAsync(cn, tx, tranId, branchId, projectId, yearId, lines, ct);
        return new LegacyPostedJournal(tranId, documentCode);
    }

    private static int ResolveBranch(LegacyUserDto actor, int? requestedBranch)
    {
        var branch = actor.BranchId ?? 0;
        if (requestedBranch.HasValue && requestedBranch.Value != branch)
            throw new InvalidOperationException("الفرع المطلوب لا يطابق فرع المستخدم.");
        if (branch <= 0)
            throw new InvalidOperationException("المستخدم غير مربوط بفرع صالح.");
        return branch;
    }

    private static async Task<int> NextBranchCodeAsync(SqlConnection cn, SqlTransaction tx, string table, int branchId, CancellationToken ct)
    {
        var sql = $"SELECT ISNULL(MAX(PurBranchID),0)+1 FROM dbo.[{table}] WITH (TABLOCKX) WHERE BranchID=@BranchID";
        return await ScalarIntAsync(cn, tx, sql, ct, ("@BranchID", branchId));
    }

    private static async Task<int> NextIdAsync(SqlConnection cn, SqlTransaction tx, string table, string column, CancellationToken ct)
    {
        var sql = $"SELECT ISNULL(MAX([{column}]),0)+1 FROM dbo.[{table}] WITH (TABLOCKX)";
        return await ScalarIntAsync(cn, tx, sql, ct);
    }

    private static async Task<int> ExecuteAsync(
        SqlConnection cn, SqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = new SqlCommand(sql, cn, tx);
        foreach (var p in parameters)
            cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> ScalarIntAsync(
        SqlConnection cn, SqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = new SqlCommand(sql, cn, tx);
        foreach (var p in parameters)
            cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static async Task<decimal> ScalarDecimalAsync(
        SqlConnection cn, SqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = new SqlCommand(sql, cn, tx);
        foreach (var p in parameters)
            cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
    }
}

public sealed record LegacyWriteResult(int Id, string Number, string Type, LegacyPostedJournal? Journal = null);
public sealed record LegacyPostedJournal(int Id, int DocumentCode);

public sealed record LegacyJournalWriteRequest(
    DateTime Date,
    string? DocCode,
    string? Note,
    int? ReferenceCode,
    int? BranchId,
    int? ProjectId,
    int? YearId,
    List<LegacyJournalLineRequest> Lines);

public sealed record LegacyJournalLineRequest(
    int AccountId,
    decimal Debit,
    decimal Credit,
    string? Description,
    int? CostCenterId,
    int? ProjectId);

public sealed record LegacySaleWriteRequest(
    DateTime Date,
    int PaymentType,
    int? BranchId,
    int? CustomerId,
    string? CustomerName,
    int? CostCenterId,
    int? ProjectId,
    int? YearId,
    int DebitAccountId,
    int SalesAccountId,
    int VatAccountId,
    List<LegacyInvoiceLineRequest> Lines,
    string? Note);

public sealed record LegacyPurchaseWriteRequest(
    DateTime Date,
    int PaymentType,
    int? BranchId,
    int? SupplierId,
    string? SupplierName,
    int? CostCenterId,
    int? ProjectId,
    int? YearId,
    int CreditAccountId,
    int InventoryAccountId,
    int VatAccountId,
    List<LegacyInvoiceLineRequest> Lines,
    string? Note);

public sealed record LegacyInvoiceLineRequest(
    int ItemId,
    int StoreId,
    int UnitId,
    string? UnitType,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal Discount);

internal sealed record CalculatedLegacyLine(
    int ItemId,
    string ItemName,
    int StoreId,
    int UnitId,
    string UnitType,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal TotalPrice,
    decimal Vat,
    decimal NetUnitPrice,
    decimal LastCost,
    decimal MediumFactor,
    decimal BigFactor);

internal sealed record LegacyPartyAccount(string Name, string VatNumber, int AccountId);
