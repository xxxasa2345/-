using Microsoft.EntityFrameworkCore;
using SaqerAccountingSystem.Domain;

namespace SaqerAccountingSystem.Infrastructure;

public sealed class AccountingDbContext(DbContextOptions<AccountingDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<GroupPermission> GroupPermissions => Set<GroupPermission>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Company>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Branch>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<Customer>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<Supplier>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<Item>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<Account>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<TaxCode>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<CostCenter>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<FixedAsset>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.Entity<UserAccount>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Permission>().HasIndex(x => x.Code).IsUnique();

        foreach (var entity in new[] { typeof(Customer), typeof(Supplier), typeof(Item), typeof(Account), typeof(JournalLine), typeof(SalesInvoiceLine), typeof(PurchaseInvoiceLine), typeof(InventoryMovement), typeof(Payment), typeof(TaxCode), typeof(FixedAsset), typeof(CostCenter), typeof(Budget) })
        {
            foreach (var p in b.Entity(entity).Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                p.SetColumnType("decimal(19,4)");
        }

        b.Entity<JournalEntry>().Property(x => x.Status).HasConversion<string>();
        b.Entity<SalesInvoice>().Property(x => x.Status).HasConversion<string>();
        b.Entity<PurchaseInvoice>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Account>().Property(x => x.Type).HasConversion<string>();
        b.Entity<Payment>().Property(x => x.PartyType).HasConversion<string>();
        b.Entity<Payment>().Property(x => x.Method).HasConversion<string>();

        b.Entity<GroupPermission>().HasKey(x => new { x.GroupId, x.PermissionId });
        b.Entity<UserGroup>().HasKey(x => new { x.UserId, x.GroupId });
        b.Entity<GroupPermission>().HasOne(x => x.Group).WithMany(x => x.Permissions).HasForeignKey(x => x.GroupId);
        b.Entity<GroupPermission>().HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId);
        b.Entity<UserGroup>().HasOne(x => x.User).WithMany(x => x.Groups).HasForeignKey(x => x.UserId);
        b.Entity<UserGroup>().HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<AuthSession>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<JournalEntry>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SalesInvoice>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PurchaseInvoice>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}
