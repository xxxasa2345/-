namespace SaqerAccountingSystem.Desktop;

public sealed record LegacyWriteResultRow(int Id, string Number, string Type, LegacyPostedJournalRow? Journal);
public sealed record LegacyPostedJournalRow(int Id, int DocumentCode);

public sealed record LegacyJournalWriteClientRequest(
    DateTime Date,
    string? DocCode,
    string? Note,
    int? ReferenceCode,
    int? BranchId,
    int? ProjectId,
    int? YearId,
    List<LegacyJournalLineWriteClientRequest> Lines);

public sealed record LegacyJournalLineWriteClientRequest(
    int AccountId,
    decimal Debit,
    decimal Credit,
    string? Description,
    int? CostCenterId,
    int? ProjectId);

public sealed record LegacySaleWriteClientRequest(
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
    List<LegacyInvoiceLineWriteClientRequest> Lines,
    string? Note);

public sealed record LegacyPurchaseWriteClientRequest(
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
    List<LegacyInvoiceLineWriteClientRequest> Lines,
    string? Note);

public sealed record LegacyInvoiceLineWriteClientRequest(
    int ItemId,
    int StoreId,
    int UnitId,
    string? UnitType,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal Discount);
