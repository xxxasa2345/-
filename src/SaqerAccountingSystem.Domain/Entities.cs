using System.ComponentModel.DataAnnotations;

namespace SaqerAccountingSystem.Domain;

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }
public enum DocumentStatus { Draft, Approved, Paid, Cancelled }
public enum PartyType { Customer, Supplier }
public enum PaymentMethod { Cash, Bank, Card, Transfer, Cheque }

public sealed class Company
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(50)] public string TaxNumber { get; set; } = "";
    [MaxLength(10)] public string Currency { get; set; } = "SAR";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class Branch
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(300)] public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public sealed class Customer
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(50)] public string TaxNumber { get; set; } = "";
    [MaxLength(400)] public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public decimal CreditLimit { get; set; }
}
public sealed class Supplier
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(50)] public string TaxNumber { get; set; } = "";
    [MaxLength(400)] public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public sealed class Item
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(50)] public string Code { get; set; } = "";
    [MaxLength(250)] public string Name { get; set; } = "";
    [MaxLength(100)] public string Category { get; set; } = "";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal StockQuantity { get; set; }
    public int InventoryAccountId { get; set; }
    public int SalesAccountId { get; set; }
    public int CostAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class Account
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(250)] public string Name { get; set; } = "";
    public AccountType Type { get; set; }
    public int? ParentId { get; set; }
    public bool IsControlAccount { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class JournalEntry
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    [MaxLength(40)] public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    [MaxLength(500)] public string Description { get; set; } = "";
    [MaxLength(50)] public string ReferenceType { get; set; } = "";
    public long? ReferenceId { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public List<JournalLine> Lines { get; set; } = new();
}
public sealed class JournalLine
{
    public long Id { get; set; }
    public long JournalEntryId { get; set; }
    public int AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    [MaxLength(500)] public string Description { get; set; } = "";
    public int? CostCenterId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
public sealed class SalesInvoice
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    [MaxLength(40)] public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public int? CustomerId { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
    public List<SalesInvoiceLine> Lines { get; set; } = new();
}
public sealed class SalesInvoiceLine
{
    public long Id { get; set; }
    public long SalesInvoiceId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
}
public sealed class PurchaseInvoice
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    [MaxLength(40)] public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public int? SupplierId { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
    public List<PurchaseInvoiceLine> Lines { get; set; } = new();
}
public sealed class PurchaseInvoiceLine
{
    public long Id { get; set; }
    public long PurchaseInvoiceId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
}
public sealed class InventoryMovement
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int ItemId { get; set; }
    public DateTime Date { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal UnitCost { get; set; }
    [MaxLength(50)] public string ReferenceType { get; set; } = "";
    public long? ReferenceId { get; set; }
}
public sealed class Payment
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    [MaxLength(40)] public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public PartyType PartyType { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public int CashOrBankAccountId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    [MaxLength(500)] public string Description { get; set; } = "";
}
public sealed class TaxCode
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(150)] public string Name { get; set; } = "";
    public decimal Rate { get; set; }
    public bool IsPurchase { get; set; }
    public bool IsSales { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class FixedAsset
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(50)] public string Code { get; set; } = "";
    [MaxLength(250)] public string Name { get; set; } = "";
    public DateTime AcquisitionDate { get; set; }
    public decimal Cost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class CostCenter
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public sealed class Budget
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int AccountId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
}
public sealed class Group
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<GroupPermission> Permissions { get; set; } = new();
}
public sealed class Permission
{
    public int Id { get; set; }
    [MaxLength(120)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
}
public sealed class GroupPermission
{
    public int GroupId { get; set; }
    public int PermissionId { get; set; }
    public Group Group { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
public sealed class UserAccount
{
    public int Id { get; set; }
    [MaxLength(100)] public string Username { get; set; } = "";
    [MaxLength(250)] public string FullName { get; set; } = "";
    [MaxLength(500)] public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<UserGroup> Groups { get; set; } = new();
}
public sealed class UserGroup
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public UserAccount User { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
public sealed class AuthSession
{
    public long Id { get; set; }
    [MaxLength(128)] public string TokenHash { get; set; } = "";
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserAccount User { get; set; } = null!;
}
public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    [MaxLength(100)] public string Action { get; set; } = "";
    [MaxLength(100)] public string Entity { get; set; } = "";
    public long? EntityId { get; set; }
    [MaxLength(1000)] public string Details { get; set; } = "";
}
