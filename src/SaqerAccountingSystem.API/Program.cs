using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SaqerAccountingSystem.Application;
using SaqerAccountingSystem.Domain;
using SaqerAccountingSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");
builder.Services.AddDbContext<AccountingDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddSingleton<MajedSoftLegacyStore>();
builder.Services.AddSingleton<LegacySessionStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/legacy/auth/login", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    if (!context.Request.Headers.TryGetValue("Authorization", out var header) ||
        !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        await Unauthorized(context, "يلزم تسجيل الدخول.");
        return;
    }

    var token = header.ToString()["Bearer ".Length..].Trim();

    var legacySessions = context.RequestServices.GetRequiredService<LegacySessionStore>();
    if (legacySessions.TryGet(token, out var legacySession))
    {
        context.Items["legacySession"] = legacySession;
        context.Items["permissions"] = legacySession.Permissions.ToArray();
        await next();
        return;
    }

    var db = context.RequestServices.GetRequiredService<AccountingDbContext>();
    var session = await db.AuthSessions.AsNoTracking()
        .SingleOrDefaultAsync(x => x.TokenHash == PasswordHasher.HashToken(token) && x.ExpiresAt > DateTime.UtcNow);

    if (session is null)
    {
        await Unauthorized(context, "جلسة الدخول غير صالحة أو منتهية.");
        return;
    }

    var user = await db.Users.AsNoTracking()
        .Include(x => x.Groups).ThenInclude(x => x.Group)
        .ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
        .SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive);

    if (user is null)
    {
        await Unauthorized(context, "المستخدم غير فعال.");
        return;
    }

    var permissions = user.Groups.SelectMany(x => x.Group.Permissions)
        .Select(x => x.Permission.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    context.Items["session"] = session;
    context.Items["user"] = user;
    context.Items["permissions"] = permissions;
    await next();
});

app.MapGet("/api/health", async (AccountingDbContext db) =>
    Results.Ok(new { status = await db.Database.CanConnectAsync() ? "ok" : "database-unavailable", system = "Saqer Accounting System", version = "2.0.0" }))
    .WithTags("System");

app.MapPost("/api/auth/login", async (LoginRequest request, AccountingDbContext db) =>
{
    var user = await db.Users.AsNoTracking()
        .Include(x => x.Groups).ThenInclude(x => x.Group)
        .ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
        .SingleOrDefaultAsync(x => x.Username == request.Username);

    if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var session = new AuthSession
    {
        UserId = user.Id,
        TokenHash = PasswordHasher.HashToken(token),
        ExpiresAt = DateTime.UtcNow.AddHours(12)
    };
    db.AuthSessions.Add(session);
    db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "LOGIN", Entity = "UserAccount", EntityId = user.Id, Details = "تسجيل دخول ناجح" });
    await db.SaveChangesAsync();

    var permissions = user.Groups.SelectMany(x => x.Group.Permissions).Select(x => x.Permission.Code).Distinct().OrderBy(x => x).ToArray();
    return Results.Ok(new
    {
        token,
        expiresAt = session.ExpiresAt,
        user = new { user.Id, user.Username, user.FullName },
        groups = user.Groups.Select(x => new { x.GroupId, name = x.Group.Name }),
        permissions,
        navigation = Navigation(permissions)
    });
}).WithTags("Authentication");

app.MapPost("/api/auth/logout", async (HttpContext ctx, AccountingDbContext db) =>
{
    var session = GetSession(ctx);
    db.AuthSessions.Remove(session);
    await Audit(db, ctx, "LOGOUT", "AuthSession", session.Id, "تسجيل خروج");
    return Results.Ok(new { message = "تم تسجيل الخروج." });
}).WithTags("Authentication");

app.MapGet("/api/auth/me", async (HttpContext ctx, AccountingDbContext db) =>
{
    var user = GetUser(ctx);
    var permissions = GetPermissions(ctx);
    var company = await db.Companies.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
    var branches = company == null
        ? new List<Branch>()
        : await db.Branches.AsNoTracking().Where(x => x.CompanyId == company.Id).OrderBy(x => x.Code).ToListAsync();

    return Results.Ok(new { user = new { user.Id, user.Username, user.FullName }, company, branches, permissions, navigation = Navigation(permissions) });
}).WithTags("Authentication");

app.MapGet("/api/navigation", (HttpContext ctx) => Results.Ok(Navigation(GetPermissions(ctx))));

app.MapGet("/api/companies", async (AccountingDbContext db) => Results.Ok(await db.Companies.AsNoTracking().OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("companies.view");
app.MapPost("/api/companies", async (Company input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; input.CreatedAt = DateTime.UtcNow;
    if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest(new { message = "رمز الشركة واسمها مطلوبان." });
    db.Companies.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Company", input.Id, input.Name);
    return Results.Created("/api/companies/" + input.Id, input);
}).RequirePermission("companies.create");

app.MapGet("/api/branches", async (int? companyId, AccountingDbContext db) =>
    Results.Ok(await db.Branches.AsNoTracking().Where(x => !companyId.HasValue || x.CompanyId == companyId).OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("companies.view");
app.MapPost("/api/branches", async (Branch input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.Branches.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Branch", input.Id, input.Name);
    return Results.Created("/api/branches/" + input.Id, input);
}).RequirePermission("companies.create");

app.MapGet("/api/customers", async (AccountingDbContext db) => Results.Ok(await db.Customers.AsNoTracking().OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("customers.view");
app.MapPost("/api/customers", async (Customer input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.Customers.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Customer", input.Id, input.Name);
    return Results.Created("/api/customers/" + input.Id, input);
}).RequirePermission("customers.create");

app.MapGet("/api/suppliers", async (AccountingDbContext db) => Results.Ok(await db.Suppliers.AsNoTracking().OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("suppliers.view");
app.MapPost("/api/suppliers", async (Supplier input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.Suppliers.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Supplier", input.Id, input.Name);
    return Results.Created("/api/suppliers/" + input.Id, input);
}).RequirePermission("suppliers.create");

app.MapGet("/api/items", async (AccountingDbContext db) => Results.Ok(await db.Items.AsNoTracking().OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("items.view");
app.MapPost("/api/items", async (Item input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0;
    if (input.InventoryAccountId == 0) input.InventoryAccountId = await FindAccountId(db, input.CompanyId, "1301");
    if (input.SalesAccountId == 0) input.SalesAccountId = await FindAccountId(db, input.CompanyId, "4101");
    if (input.CostAccountId == 0) input.CostAccountId = await FindAccountId(db, input.CompanyId, "5101");
    db.Items.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Item", input.Id, input.Name);
    return Results.Created("/api/items/" + input.Id, input);
}).RequirePermission("items.create");

app.MapGet("/api/accounts", async (AccountingDbContext db) =>
{
    var accounts = await db.Accounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync();
    var balances = await db.JournalLines.AsNoTracking()
        .Where(x => x.JournalEntry.Status == DocumentStatus.Approved)
        .GroupBy(x => x.AccountId)
        .Select(g => new { g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
        .ToDictionaryAsync(x => x.Key);
    return Results.Ok(accounts.Select(a =>
    {
        decimal debit = 0, credit = 0;
        if (balances.TryGetValue(a.Id, out var totals))
        {
            debit = totals.Debit;
            credit = totals.Credit;
        }
        var balance = a.Type is AccountType.Asset or AccountType.Expense ? debit - credit : credit - debit;
        return new { id = a.Id, code = a.Code, name = a.Name, type = a.Type.ToString(), balance };
    }));
}).RequirePermission("accounts.view");
app.MapPost("/api/accounts", async (Account input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.Accounts.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Account", input.Id, input.Name);
    return Results.Created("/api/accounts/" + input.Id, input);
}).RequirePermission("accounts.create");

app.MapGet("/api/journals", async (AccountingDbContext db) =>
    Results.Ok(await db.JournalEntries.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Account)
        .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Take(500).ToListAsync()))
   .RequirePermission("journals.view");

app.MapPost("/api/journals", async (JournalCreateRequest input, AccountingDbContext db, HttpContext ctx) =>
{
    if (input.Lines.Count == 0) return Results.BadRequest(new { message = "القيد يحتاج إلى سطور." });
    var accountIds = input.Lines.Select(x => x.AccountId).Distinct().ToArray();
    if (await db.Accounts.CountAsync(x => accountIds.Contains(x.Id) && x.IsActive) != accountIds.Length)
        return Results.BadRequest(new { message = "يوجد حساب غير صالح." });

    var debit = input.Lines.Sum(x => x.Debit);
    var credit = input.Lines.Sum(x => x.Credit);
    try { AccountingRules.ValidateBalanced(debit, credit); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }

    var entry = new JournalEntry
    {
        CompanyId = input.CompanyId, BranchId = input.BranchId, Number = "JE-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
        Date = input.Date.Date, Description = input.Description ?? "", Status = DocumentStatus.Draft,
        Lines = input.Lines.Select(x => new JournalLine
        {
            AccountId = x.AccountId, Debit = x.Debit, Credit = x.Credit, Description = x.Description ?? "", CostCenterId = x.CostCenterId
        }).ToList()
    };
    db.JournalEntries.Add(entry); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "JournalEntry", entry.Id, entry.Number);
    return Results.Created("/api/journals/" + entry.Id, entry);
}).RequirePermission("journals.create");

app.MapPost("/api/journals/{id:long}/post", async (long id, AccountingDbContext db, HttpContext ctx) =>
{
    var entry = await db.JournalEntries.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id);
    if (entry == null) return Results.NotFound();
    if (entry.Status != DocumentStatus.Draft) return Results.BadRequest(new { message = "القيد ليس مسودة." });
    try { AccountingRules.ValidateBalanced(entry.Lines.Sum(x => x.Debit), entry.Lines.Sum(x => x.Credit)); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
    entry.Status = DocumentStatus.Approved; entry.PostedAt = DateTime.UtcNow; entry.PostedByUserId = GetUser(ctx).Id;
    await db.SaveChangesAsync(); await Audit(db, ctx, "POST", "JournalEntry", entry.Id, entry.Number);
    return Results.Ok(entry);
}).RequirePermission("journals.post");

app.MapGet("/api/sales", async (AccountingDbContext db) => Results.Ok(await db.SalesInvoices.AsNoTracking().Include(x => x.Lines).OrderByDescending(x => x.Id).Take(500).ToListAsync()))
   .RequirePermission("sales.view");

app.MapPost("/api/sales", async (SalesCreateRequest input, AccountingDbContext db, HttpContext ctx) =>
{
    if (input.Lines.Count == 0) return Results.BadRequest(new { message = "فاتورة المبيعات يجب أن تحتوي على أصناف." });
    var ids = input.Lines.Select(x => x.ItemId).Distinct().ToArray();
    var items = await db.Items.Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id);
    if (items.Count != ids.Length) return Results.BadRequest(new { message = "يوجد صنف غير صالح." });

    var lines = new List<SalesInvoiceLine>();
    foreach (var line in input.Lines)
    {
        if (line.Quantity <= 0) return Results.BadRequest(new { message = "الكمية يجب أن تكون أكبر من صفر." });
        var item = items[line.ItemId];
        var price = line.UnitPrice ?? item.SalePrice;
        var taxable = Math.Max(0, decimal.Round(line.Quantity * price - Math.Max(0, line.Discount), 2));
        var taxRate = line.TaxRate ?? item.TaxRate;
        var tax = AccountingRules.CalculateTax(taxable, taxRate);
        lines.Add(new SalesInvoiceLine { ItemId = line.ItemId, Quantity = line.Quantity, UnitPrice = price, Discount = Math.Max(0, line.Discount), TaxRate = taxRate, TaxAmount = tax, Total = taxable + tax });
    }

    var subtotal = decimal.Round(lines.Sum(x => x.Quantity * x.UnitPrice - x.Discount), 2);
    var taxTotal = decimal.Round(lines.Sum(x => x.TaxAmount), 2);
    var invoice = new SalesInvoice
    {
        CompanyId = input.CompanyId, BranchId = input.BranchId, Number = input.Number, Date = input.Date.Date,
        CustomerId = input.CustomerId, SubTotal = subtotal, TaxAmount = taxTotal, Total = subtotal + taxTotal, Lines = lines
    };
    db.SalesInvoices.Add(invoice); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "SalesInvoice", invoice.Id, invoice.Number);
    return Results.Created("/api/sales/" + invoice.Id, invoice);
}).RequirePermission("sales.create");

app.MapPost("/api/sales/{id:long}/post", async (long id, InvoicePostRequest request, AccountingDbContext db, HttpContext ctx) =>
{
    await using var tx = await db.Database.BeginTransactionAsync();
    var invoice = await db.SalesInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id);
    if (invoice == null) return Results.NotFound();
    if (invoice.Status != DocumentStatus.Draft) return Results.BadRequest(new { message = "الفاتورة ليست مسودة." });

    var itemIds = invoice.Lines.Select(x => x.ItemId).Distinct().ToArray();
    var items = await db.Items.Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
    var receivable = await FindAccount(db, invoice.CompanyId, "1201");
    var sales = await FindAccount(db, invoice.CompanyId, "4101");
    var inventory = await FindAccount(db, invoice.CompanyId, "1301");
    var cogs = await FindAccount(db, invoice.CompanyId, "5101");
    var vat = await FindAccount(db, invoice.CompanyId, "2201");
    var cash = request.CashOrBankAccountId > 0
        ? await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.CashOrBankAccountId && x.CompanyId == invoice.CompanyId)
        : null;
    cash ??= await FindAccount(db, invoice.CompanyId, "1101");

    decimal totalCost = 0;
    foreach (var line in invoice.Lines)
    {
        var item = items[line.ItemId];
        if (item.StockQuantity < line.Quantity) return Results.BadRequest(new { message = "المخزون غير كافٍ للصنف " + item.Name });
        totalCost += line.Quantity * item.PurchasePrice;
        item.StockQuantity -= line.Quantity;
        db.InventoryMovements.Add(new InventoryMovement
        {
            CompanyId = invoice.CompanyId, BranchId = invoice.BranchId, ItemId = item.Id, Date = invoice.Date,
            QuantityOut = line.Quantity, UnitCost = item.PurchasePrice, ReferenceType = "SalesInvoice", ReferenceId = invoice.Id
        });
    }

    invoice.IsPaid = request.IsPaid;
    var journalLines = new List<JournalLine>
    {
        new JournalLine { AccountId = invoice.IsPaid ? cash.Id : receivable.Id, Debit = invoice.Total, Credit = 0, Description = invoice.Number },
        new JournalLine { AccountId = sales.Id, Debit = 0, Credit = invoice.SubTotal, Description = invoice.Number }
    };
    if (invoice.TaxAmount > 0) journalLines.Add(new JournalLine { AccountId = vat.Id, Debit = 0, Credit = invoice.TaxAmount, Description = invoice.Number });
    if (totalCost > 0)
    {
        journalLines.Add(new JournalLine { AccountId = cogs.Id, Debit = totalCost, Credit = 0, Description = "تكلفة " + invoice.Number });
        journalLines.Add(new JournalLine { AccountId = inventory.Id, Debit = 0, Credit = totalCost, Description = invoice.Number });
    }

    var entry = new JournalEntry
    {
        CompanyId = invoice.CompanyId, BranchId = invoice.BranchId, Number = "JE-S-" + invoice.Number, Date = invoice.Date,
        Description = "ترحيل فاتورة مبيعات " + invoice.Number, ReferenceType = "SalesInvoice", ReferenceId = invoice.Id,
        Status = DocumentStatus.Approved, PostedAt = DateTime.UtcNow, PostedByUserId = GetUser(ctx).Id, Lines = journalLines
    };
    try { AccountingRules.ValidateBalanced(journalLines.Sum(x => x.Debit), journalLines.Sum(x => x.Credit)); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }

    invoice.Status = request.IsPaid ? DocumentStatus.Paid : DocumentStatus.Approved;
    db.JournalEntries.Add(entry);
    await db.SaveChangesAsync();
    await Audit(db, ctx, "POST", "SalesInvoice", invoice.Id, invoice.Number);
    await tx.CommitAsync();
    return Results.Ok(invoice);
}).RequirePermission("sales.post");

app.MapGet("/api/purchases", async (AccountingDbContext db) => Results.Ok(await db.PurchaseInvoices.AsNoTracking().Include(x => x.Lines).OrderByDescending(x => x.Id).Take(500).ToListAsync()))
   .RequirePermission("purchases.view");

app.MapPost("/api/purchases", async (PurchaseCreateRequest input, AccountingDbContext db, HttpContext ctx) =>
{
    if (input.Lines.Count == 0) return Results.BadRequest(new { message = "فاتورة المشتريات يجب أن تحتوي على أصناف." });
    var ids = input.Lines.Select(x => x.ItemId).Distinct().ToArray();
    var items = await db.Items.Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id);
    if (items.Count != ids.Length) return Results.BadRequest(new { message = "يوجد صنف غير صالح." });

    var lines = new List<PurchaseInvoiceLine>();
    foreach (var line in input.Lines)
    {
        if (line.Quantity <= 0) return Results.BadRequest(new { message = "الكمية يجب أن تكون أكبر من صفر." });
        var item = items[line.ItemId];
        var price = line.UnitPrice ?? item.PurchasePrice;
        var taxable = Math.Max(0, decimal.Round(line.Quantity * price - Math.Max(0, line.Discount), 2));
        var taxRate = line.TaxRate ?? item.TaxRate;
        var tax = AccountingRules.CalculateTax(taxable, taxRate);
        lines.Add(new PurchaseInvoiceLine { ItemId = line.ItemId, Quantity = line.Quantity, UnitPrice = price, Discount = Math.Max(0, line.Discount), TaxRate = taxRate, TaxAmount = tax, Total = taxable + tax });
    }

    var subtotal = decimal.Round(lines.Sum(x => x.Quantity * x.UnitPrice - x.Discount), 2);
    var taxTotal = decimal.Round(lines.Sum(x => x.TaxAmount), 2);
    var invoice = new PurchaseInvoice
    {
        CompanyId = input.CompanyId, BranchId = input.BranchId, Number = input.Number, Date = input.Date.Date,
        SupplierId = input.SupplierId, SubTotal = subtotal, TaxAmount = taxTotal, Total = subtotal + taxTotal, Lines = lines
    };
    db.PurchaseInvoices.Add(invoice); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "PurchaseInvoice", invoice.Id, invoice.Number);
    return Results.Created("/api/purchases/" + invoice.Id, invoice);
}).RequirePermission("purchases.create");

app.MapPost("/api/purchases/{id:long}/post", async (long id, InvoicePostRequest request, AccountingDbContext db, HttpContext ctx) =>
{
    await using var tx = await db.Database.BeginTransactionAsync();
    var invoice = await db.PurchaseInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id);
    if (invoice == null) return Results.NotFound();
    if (invoice.Status != DocumentStatus.Draft) return Results.BadRequest(new { message = "الفاتورة ليست مسودة." });

    var itemIds = invoice.Lines.Select(x => x.ItemId).Distinct().ToArray();
    var items = await db.Items.Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
    var inventory = await FindAccount(db, invoice.CompanyId, "1301");
    var vat = await FindAccount(db, invoice.CompanyId, "2201");
    var payable = await FindAccount(db, invoice.CompanyId, "2101");
    var cash = request.CashOrBankAccountId > 0
        ? await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.CashOrBankAccountId && x.CompanyId == invoice.CompanyId)
        : null;
    cash ??= await FindAccount(db, invoice.CompanyId, "1101");

    foreach (var line in invoice.Lines)
    {
        var item = items[line.ItemId];
        item.StockQuantity += line.Quantity;
        item.PurchasePrice = line.UnitPrice;
        db.InventoryMovements.Add(new InventoryMovement
        {
            CompanyId = invoice.CompanyId, BranchId = invoice.BranchId, ItemId = item.Id, Date = invoice.Date,
            QuantityIn = line.Quantity, UnitCost = line.UnitPrice, ReferenceType = "PurchaseInvoice", ReferenceId = invoice.Id
        });
    }

    invoice.IsPaid = request.IsPaid;
    var journalLines = new List<JournalLine>
    {
        new JournalLine { AccountId = inventory.Id, Debit = invoice.SubTotal, Credit = 0, Description = invoice.Number },
        new JournalLine { AccountId = vat.Id, Debit = invoice.TaxAmount, Credit = 0, Description = invoice.Number },
        new JournalLine { AccountId = invoice.IsPaid ? cash.Id : payable.Id, Debit = 0, Credit = invoice.Total, Description = invoice.Number }
    };
    try { AccountingRules.ValidateBalanced(journalLines.Sum(x => x.Debit), journalLines.Sum(x => x.Credit)); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }

    var entry = new JournalEntry
    {
        CompanyId = invoice.CompanyId, BranchId = invoice.BranchId, Number = "JE-P-" + invoice.Number, Date = invoice.Date,
        Description = "ترحيل فاتورة مشتريات " + invoice.Number, ReferenceType = "PurchaseInvoice", ReferenceId = invoice.Id,
        Status = DocumentStatus.Approved, PostedAt = DateTime.UtcNow, PostedByUserId = GetUser(ctx).Id, Lines = journalLines
    };
    invoice.Status = request.IsPaid ? DocumentStatus.Paid : DocumentStatus.Approved;
    db.JournalEntries.Add(entry);
    await db.SaveChangesAsync();
    await Audit(db, ctx, "POST", "PurchaseInvoice", invoice.Id, invoice.Number);
    await tx.CommitAsync();
    return Results.Ok(invoice);
}).RequirePermission("purchases.post");

app.MapGet("/api/inventory", async (AccountingDbContext db) => Results.Ok(await db.InventoryMovements.AsNoTracking().OrderByDescending(x => x.Id).Take(1000).ToListAsync()))
   .RequirePermission("inventory.view");

app.MapPost("/api/inventory", async (InventoryMovementRequest input, AccountingDbContext db, HttpContext ctx) =>
{
    if (input.QuantityIn < 0 || input.QuantityOut < 0 || input.QuantityIn == input.QuantityOut)
        return Results.BadRequest(new { message = "أدخل كمية وارد أو صادر واحدة فقط." });
    var item = await db.Items.SingleOrDefaultAsync(x => x.Id == input.ItemId);
    if (item == null) return Results.NotFound();
    var newStock = item.StockQuantity + input.QuantityIn - input.QuantityOut;
    if (newStock < 0) return Results.BadRequest(new { message = "لا يسمح بمخزون سالب." });
    item.StockQuantity = newStock;
    var move = new InventoryMovement
    {
        CompanyId = input.CompanyId, BranchId = input.BranchId, ItemId = input.ItemId, Date = input.Date.Date,
        QuantityIn = input.QuantityIn, QuantityOut = input.QuantityOut, UnitCost = input.UnitCost, ReferenceType = "Manual"
    };
    db.InventoryMovements.Add(move); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "InventoryMovement", move.Id, "حركة مخزون");
    return Results.Created("/api/inventory/" + move.Id, move);
}).RequirePermission("inventory.create");

app.MapGet("/api/payments", async (AccountingDbContext db) => Results.Ok(await db.Payments.AsNoTracking().OrderByDescending(x => x.Id).Take(500).ToListAsync()))
   .RequirePermission("payments.view");

app.MapPost("/api/payments", async (PaymentCreateRequest input, AccountingDbContext db, HttpContext ctx) =>
{
    if (input.Amount <= 0) return Results.BadRequest(new { message = "مبلغ الدفعة غير صالح." });
    var cash = await db.Accounts.SingleOrDefaultAsync(x => x.Id == input.CashOrBankAccountId && x.CompanyId == input.CompanyId);
    if (cash == null) return Results.BadRequest(new { message = "حساب الصندوق أو البنك غير صالح." });
    var counter = await FindAccount(db, input.CompanyId, input.PartyType == PartyType.Customer ? "1201" : "2101");

    var payment = new Payment
    {
        CompanyId = input.CompanyId, BranchId = input.BranchId, Number = input.Number, Date = input.Date.Date,
        PartyType = input.PartyType, CustomerId = input.CustomerId, SupplierId = input.SupplierId,
        CashOrBankAccountId = input.CashOrBankAccountId, Amount = input.Amount, Method = input.Method, Description = input.Description ?? ""
    };
    var entry = new JournalEntry
    {
        CompanyId = input.CompanyId, BranchId = input.BranchId, Number = "JE-PAY-" + input.Number, Date = input.Date.Date,
        Description = input.Description ?? "دفعة", ReferenceType = "Payment", Status = DocumentStatus.Approved,
        PostedAt = DateTime.UtcNow, PostedByUserId = GetUser(ctx).Id,
        Lines = input.PartyType == PartyType.Customer
            ? new List<JournalLine> { new JournalLine { AccountId = cash.Id, Debit = input.Amount, Credit = 0 }, new JournalLine { AccountId = counter.Id, Debit = 0, Credit = input.Amount } }
            : new List<JournalLine> { new JournalLine { AccountId = counter.Id, Debit = input.Amount, Credit = 0 }, new JournalLine { AccountId = cash.Id, Debit = 0, Credit = input.Amount } }
    };
    db.Payments.Add(payment); db.JournalEntries.Add(entry); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Payment", payment.Id, payment.Number);
    return Results.Created("/api/payments/" + payment.Id, payment);
}).RequirePermission("payments.create");

app.MapGet("/api/tax", async (AccountingDbContext db) => Results.Ok(await db.TaxCodes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("tax.view");
app.MapPost("/api/tax", async (TaxCode input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.TaxCodes.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "TaxCode", input.Id, input.Code);
    return Results.Created("/api/tax/" + input.Id, input);
}).RequirePermission("tax.create");

app.MapGet("/api/assets", async (AccountingDbContext db) => Results.Ok(await db.FixedAssets.AsNoTracking().OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("assets.view");
app.MapPost("/api/assets", async (FixedAsset input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.FixedAssets.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "FixedAsset", input.Id, input.Name);
    return Results.Created("/api/assets/" + input.Id, input);
}).RequirePermission("assets.create");

app.MapPost("/api/assets/{id:int}/depreciate", async (int id, DepreciationRequest request, AccountingDbContext db, HttpContext ctx) =>
{
    var asset = await db.FixedAssets.SingleOrDefaultAsync(x => x.Id == id && x.IsActive);
    if (asset == null) return Results.NotFound();
    if (asset.UsefulLifeMonths <= 0 || asset.Cost <= asset.SalvageValue)
        return Results.BadRequest(new { message = "بيانات الإهلاك للأصل غير صالحة." });

    var monthsElapsed = request.Months <= 0 ? 1 : request.Months;
    var monthly = decimal.Round((asset.Cost - asset.SalvageValue) / asset.UsefulLifeMonths, 2);
    var remaining = decimal.Round(asset.Cost - asset.SalvageValue - asset.AccumulatedDepreciation, 2);
    var depreciation = decimal.Min(monthly * monthsElapsed, remaining);
    if (depreciation <= 0) return Results.BadRequest(new { message = "لا يوجد رصيد قابل للإهلاك لهذا الأصل." });

    var expense = await FindAccount(db, asset.CompanyId, "5201");
    var accumulated = await FindAccount(db, asset.CompanyId, "1601");

    var entry = new JournalEntry
    {
        CompanyId = asset.CompanyId,
        BranchId = await db.Branches.Where(x => x.CompanyId == asset.CompanyId).Select(x => x.Id).FirstAsync(),
        Number = "JE-DEP-" + asset.Code + "-" + request.Year + "-" + request.Month,
        Date = new DateTime(request.Year, request.Month, 1),
        Description = "إهلاك أصل " + asset.Name,
        ReferenceType = "FixedAsset",
        ReferenceId = asset.Id,
        Status = DocumentStatus.Approved,
        PostedAt = DateTime.UtcNow,
        PostedByUserId = GetUser(ctx).Id,
        Lines = new List<JournalLine>
        {
            new JournalLine { AccountId = expense.Id, Debit = depreciation, Credit = 0, Description = "مصروف إهلاك" },
            new JournalLine { AccountId = accumulated.Id, Debit = 0, Credit = depreciation, Description = "مجمع إهلاك" }
        }
    };
    asset.AccumulatedDepreciation += depreciation;
    db.JournalEntries.Add(entry);
    await db.SaveChangesAsync();
    await Audit(db, ctx, "DEPRECIATE", "FixedAsset", asset.Id, asset.Name);
    return Results.Ok(new { asset, depreciation, journal = entry.Number });
}).RequirePermission("assets.create");

app.MapGet("/api/cost-centers", async (AccountingDbContext db) => Results.Ok(await db.CostCenters.AsNoTracking().OrderBy(x => x.Code).ToListAsync()))
   .RequirePermission("costcenters.view");
app.MapPost("/api/cost-centers", async (CostCenter input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.CostCenters.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "CostCenter", input.Id, input.Name);
    return Results.Created("/api/cost-centers/" + input.Id, input);
}).RequirePermission("costcenters.create");

app.MapGet("/api/budgets", async (int year, AccountingDbContext db) =>
    Results.Ok(await db.Budgets.AsNoTracking().Where(x => x.Year == year).OrderBy(x => x.Month).ToListAsync()))
   .RequirePermission("budgets.view");
app.MapPost("/api/budgets", async (Budget input, AccountingDbContext db, HttpContext ctx) =>
{
    input.Id = 0; db.Budgets.Add(input); await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "Budget", input.Id, "موازنة");
    return Results.Created("/api/budgets/" + input.Id, input);
}).RequirePermission("budgets.create");

app.MapGet("/api/reports/trial-balance", async (DateTime? from, DateTime? to, AccountingDbContext db) =>
{
    var q = db.JournalLines.AsNoTracking().Where(x => x.JournalEntry.Status == DocumentStatus.Approved);
    if (from.HasValue) q = q.Where(x => x.JournalEntry.Date >= from.Value.Date);
    if (to.HasValue) q = q.Where(x => x.JournalEntry.Date <= to.Value.Date);
    var rows = await q.GroupBy(x => new { x.AccountId, x.Account.Code, x.Account.Name, x.Account.Type })
        .Select(g => new { g.Key.AccountId, g.Key.Code, g.Key.Name, type = g.Key.Type.ToString(), debit = g.Sum(x => x.Debit), credit = g.Sum(x => x.Credit), balance = g.Sum(x => x.Debit - x.Credit) })
        .OrderBy(x => x.Code).ToListAsync();
    return Results.Ok(rows);
}).RequirePermission("reports.view");

app.MapGet("/api/reports/income-statement", async (DateTime? from, DateTime? to, AccountingDbContext db) =>
{
    var start = from?.Date ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
    var end = to?.Date ?? DateTime.UtcNow.Date;
    var rows = await db.JournalLines.AsNoTracking()
        .Where(x => x.JournalEntry.Status == DocumentStatus.Approved && x.JournalEntry.Date >= start && x.JournalEntry.Date <= end)
        .GroupBy(x => new { x.AccountId, x.Account.Code, x.Account.Name, x.Account.Type })
        .Select(g => new { g.Key.AccountId, g.Key.Code, g.Key.Name, type = g.Key.Type.ToString(), net = g.Sum(x => x.Credit - x.Debit) })
        .Where(x => x.type == "Revenue" || x.type == "Expense").OrderBy(x => x.Code).ToListAsync();
    var revenue = rows.Where(x => x.type == "Revenue").Sum(x => x.net);
    var expenses = rows.Where(x => x.type == "Expense").Sum(x => -x.net);
    return Results.Ok(new { start, end, rows, revenue, expenses, netIncome = revenue - expenses });
}).RequirePermission("reports.view");

app.MapGet("/api/reports/balance-sheet", async (DateTime? to, AccountingDbContext db) =>
{
    var end = to?.Date ?? DateTime.UtcNow.Date;
    var rows = await db.JournalLines.AsNoTracking()
        .Where(x => x.JournalEntry.Status == DocumentStatus.Approved && x.JournalEntry.Date <= end)
        .GroupBy(x => new { x.AccountId, x.Account.Code, x.Account.Name, x.Account.Type })
        .Select(g => new { g.Key.AccountId, g.Key.Code, g.Key.Name, type = g.Key.Type.ToString(), net = g.Sum(x => x.Debit - x.Credit) })
        .Where(x => x.type == "Asset" || x.type == "Liability" || x.type == "Equity").OrderBy(x => x.Code).ToListAsync();
    var assets = rows.Where(x => x.type == "Asset").Sum(x => x.net);
    var liabilities = rows.Where(x => x.type == "Liability").Sum(x => -x.net);
    var equity = rows.Where(x => x.type == "Equity").Sum(x => -x.net);
    return Results.Ok(new { asOf = end, rows, assets, liabilities, equity, totalLiabilitiesAndEquity = liabilities + equity });
}).RequirePermission("reports.view");

app.MapGet("/api/dashboard", async (AccountingDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var sales = await db.SalesInvoices.Where(x => x.Date == today && x.Status != DocumentStatus.Cancelled).SumAsync(x => (decimal?)x.Total) ?? 0;
    var purchases = await db.PurchaseInvoices.Where(x => x.Date == today && x.Status != DocumentStatus.Cancelled).SumAsync(x => (decimal?)x.Total) ?? 0;
    var companyId = await db.Companies.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync();
    var receivables = companyId == 0 ? 0m : await AccountBalance(db, companyId, "1201");
    var stock = await db.Items.SumAsync(x => (decimal?)x.StockQuantity * x.PurchasePrice) ?? 0;
    return Results.Ok(new
    {
        cards = new[]
        {
            new { key = "sales", title = "مبيعات اليوم", value = sales, trend = 0m },
            new { key = "purchases", title = "مشتريات اليوم", value = purchases, trend = 0m },
            new { key = "receivables", title = "ذمم العملاء", value = receivables, trend = 0m },
            new { key = "inventory", title = "قيمة المخزون", value = stock, trend = 0m }
        },
        recentInvoices = await db.SalesInvoices.AsNoTracking().OrderByDescending(x => x.Id).Take(8).Select(x => new { number = x.Number, party = x.CustomerId, date = x.Date, amount = x.Total, status = x.Status.ToString() }).ToListAsync(),
        activity = await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id).Take(12).Select(x => new { time = x.CreatedAt, text = x.Action + " " + x.Entity, type = x.Entity }).ToListAsync()
    });
}).RequirePermission("dashboard.view");

app.MapGet("/api/modules/{module}", async (string module, AccountingDbContext db, HttpContext ctx) =>
{
    var required = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"]="dashboard.view",["companies"]="companies.view",["customers"]="customers.view",["suppliers"]="suppliers.view",["items"]="items.view",
        ["sales"]="sales.view",["purchases"]="purchases.view",["accounts"]="accounts.view",["journals"]="journals.view",["inventory"]="inventory.view",
        ["payments"]="payments.view",["tax"]="tax.view",["assets"]="assets.view",["costcenters"]="costcenters.view",["budgets"]="budgets.view",["reports"]="reports.view",["settings"]="settings.view"
    };
    if (!required.TryGetValue(module, out var permission) || !GetPermissions(ctx).Contains(permission, StringComparer.OrdinalIgnoreCase))
        return Results.Forbid();

    return module.ToLowerInvariant() switch
    {
        "companies" => Results.Ok(await db.Companies.AsNoTracking().ToListAsync()),
        "customers" => Results.Ok(await db.Customers.AsNoTracking().ToListAsync()),
        "suppliers" => Results.Ok(await db.Suppliers.AsNoTracking().ToListAsync()),
        "items" => Results.Ok(await db.Items.AsNoTracking().ToListAsync()),
        "sales" => Results.Ok(await db.SalesInvoices.AsNoTracking().OrderByDescending(x => x.Id).Take(500).ToListAsync()),
        "purchases" => Results.Ok(await db.PurchaseInvoices.AsNoTracking().OrderByDescending(x => x.Id).Take(500).ToListAsync()),
        "accounts" => Results.Ok(await db.Accounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync()),
        "journals" => Results.Ok(await db.JournalEntries.AsNoTracking().OrderByDescending(x => x.Id).Take(500).ToListAsync()),
        "inventory" => Results.Ok(await db.InventoryMovements.AsNoTracking().OrderByDescending(x => x.Id).Take(1000).ToListAsync()),
        "payments" => Results.Ok(await db.Payments.AsNoTracking().OrderByDescending(x => x.Id).Take(500).ToListAsync()),
        "tax" => Results.Ok(await db.TaxCodes.AsNoTracking().ToListAsync()),
        "assets" => Results.Ok(await db.FixedAssets.AsNoTracking().ToListAsync()),
        "costcenters" => Results.Ok(await db.CostCenters.AsNoTracking().ToListAsync()),
        "budgets" => Results.Ok(await db.Budgets.AsNoTracking().OrderByDescending(x => x.Year).ThenBy(x => x.Month).ToListAsync()),
        "reports" => Results.Ok(new { reports = new[] { "/api/reports/trial-balance", "/api/reports/income-statement", "/api/reports/balance-sheet" } }),
        "settings" => Results.Ok(new { system = "Saqer Accounting System", database = "SQL Server" }),
        _ => Results.NotFound(new { message = "الوحدة غير موجودة." })
    };
});

app.MapGet("/api/admin/users", async (AccountingDbContext db) =>
    Results.Ok(await db.Users.AsNoTracking().Include(x => x.Groups).ThenInclude(x => x.Group)
        .Select(x => new { x.Id, x.Username, x.FullName, x.IsActive, groups = x.Groups.Select(g => g.Group.Name) }).ToListAsync()))
    .RequirePermission("users.manage");

app.MapGet("/api/admin/groups", async (AccountingDbContext db) =>
    Results.Ok(await db.Groups.AsNoTracking().Include(x => x.Permissions).ThenInclude(x => x.Permission)
        .Select(x => new { x.Id, x.Name, permissions = x.Permissions.Select(p => p.Permission.Code) }).ToListAsync()))
    .RequirePermission("users.manage");

app.MapPost("/api/admin/users", async (UserCreateRequest input, AccountingDbContext db, HttpContext ctx) =>
{
    if (await db.Users.AnyAsync(x => x.Username == input.Username))
        return Results.Conflict(new { message = "اسم المستخدم مستخدم مسبقاً." });
    if (!await db.Groups.AnyAsync(x => x.Id == input.GroupId))
        return Results.BadRequest(new { message = "المجموعة غير موجودة." });

    var user = new UserAccount { Username = input.Username, FullName = input.FullName, PasswordHash = PasswordHasher.Hash(input.Password) };
    db.Users.Add(user); await db.SaveChangesAsync();
    db.UserGroups.Add(new UserGroup { UserId = user.Id, GroupId = input.GroupId });
    await db.SaveChangesAsync();
    await Audit(db, ctx, "CREATE", "UserAccount", user.Id, user.Username);
    return Results.Created("/api/admin/users/" + user.Id, new { user.Id, user.Username, user.FullName });
}).RequirePermission("users.manage");

app.MapGet("/api/audit", async (int? limit, AccountingDbContext db) =>
    Results.Ok(await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id).Take(Math.Clamp(limit ?? 200, 1, 1000)).ToListAsync()))
    .RequirePermission("audit.view");

app.MapLegacyEndpoints();

app.MapFallbackToFile("index.html");
app.Run();

static string[] GetPermissions(HttpContext context) => context.Items["permissions"] as string[] ?? Array.Empty<string>();
static UserAccount GetUser(HttpContext context) => (UserAccount)context.Items["user"]!;
static AuthSession GetSession(HttpContext context) => (AuthSession)context.Items["session"]!;

static List<NavItem> Navigation(string[] permissions)
{
    var all = new[]
    {
        new NavItem("dashboard","لوحة التحكم","▦","dashboard.view","/dashboard"),
        new NavItem("companies","الشركات والفروع","⌂","companies.view","/companies"),
        new NavItem("customers","العملاء والذمم","◉","customers.view","/customers"),
        new NavItem("suppliers","الموردون والدائنون","◎","suppliers.view","/suppliers"),
        new NavItem("items","الأصناف","▤","items.view","/items"),
        new NavItem("sales","المبيعات","▣","sales.view","/sales"),
        new NavItem("purchases","المشتريات","▥","purchases.view","/purchases"),
        new NavItem("accounts","دليل الحسابات","◫","accounts.view","/accounts"),
        new NavItem("journals","القيود والأستاذ العام","≡","journals.view","/journals"),
        new NavItem("payments","الخزينة والبنوك","◌","payments.view","/payments"),
        new NavItem("inventory","المخزون","◈","inventory.view","/inventory"),
        new NavItem("tax","الضريبة","٪","tax.view","/tax"),
        new NavItem("assets","الأصول الثابتة","▥","assets.view","/assets"),
        new NavItem("costcenters","مراكز التكلفة","◇","costcenters.view","/costcenters"),
        new NavItem("budgets","الموازنات","◫","budgets.view","/budgets"),
        new NavItem("reports","التقارير المالية","◰","reports.view","/reports"),
        new NavItem("settings","الإعدادات","⚙","settings.view","/settings")
    };
    return all.Where(x => permissions.Contains(x.Permission, StringComparer.OrdinalIgnoreCase)).ToList();
}

static async Task<Account> FindAccount(AccountingDbContext db, int companyId, string code) =>
    await db.Accounts.SingleAsync(x => x.CompanyId == companyId && x.Code == code && x.IsActive);

static async Task<int> FindAccountId(AccountingDbContext db, int companyId, string code) =>
    (await FindAccount(db, companyId, code)).Id;

static async Task<decimal> AccountBalance(AccountingDbContext db, int companyId, string code) =>
    await db.JournalLines.AsNoTracking()
        .Where(x => x.JournalEntry.Status == DocumentStatus.Approved && x.JournalEntry.CompanyId == companyId && x.Account.Code == code)
        .SumAsync(x => (decimal?)(x.Debit - x.Credit)) ?? 0;

static async Task Audit(AccountingDbContext db, HttpContext ctx, string action, string entity, long? id, string details)
{
    db.AuditLogs.Add(new AuditLog { UserId = GetUser(ctx).Id, Action = action, Entity = entity, EntityId = id, Details = details });
    await db.SaveChangesAsync();
}

static async Task Unauthorized(HttpContext context, string message)
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsJsonAsync(new { message });
}

public record LoginRequest(string Username, string Password);
public record JournalCreateRequest(int CompanyId, int BranchId, DateTime Date, string Description, List<JournalLineRequest> Lines);
public record JournalLineRequest(int AccountId, decimal Debit, decimal Credit, string? Description, int? CostCenterId);
public record SalesCreateRequest(int CompanyId, int BranchId, string Number, DateTime Date, int? CustomerId, List<SalesLineRequest> Lines);
public record SalesLineRequest(int ItemId, decimal Quantity, decimal? UnitPrice, decimal Discount = 0, decimal? TaxRate = null);
public record PurchaseCreateRequest(int CompanyId, int BranchId, string Number, DateTime Date, int? SupplierId, List<PurchaseLineRequest> Lines);
public record PurchaseLineRequest(int ItemId, decimal Quantity, decimal? UnitPrice, decimal Discount = 0, decimal? TaxRate = null);
public record InvoicePostRequest(bool IsPaid = false, int CashOrBankAccountId = 0);
public record InventoryMovementRequest(int CompanyId, int BranchId, int ItemId, DateTime Date, decimal QuantityIn, decimal QuantityOut, decimal UnitCost = 0);
public record PaymentCreateRequest(int CompanyId, int BranchId, string Number, DateTime Date, PartyType PartyType, int? CustomerId, int? SupplierId, int CashOrBankAccountId, decimal Amount, PaymentMethod Method, string? Description);
public record UserCreateRequest(string Username, string FullName, string Password, int GroupId);
public record DepreciationRequest(int Year, int Month, int Months = 1);
public record NavItem(string Key, string Title, string Icon, string Permission, string Route);

public static class PermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var permissions = context.HttpContext.Items["permissions"] as string[] ?? Array.Empty<string>();
            if (!permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                return Results.Forbid();
            return await next(context);
        });
    }
}
