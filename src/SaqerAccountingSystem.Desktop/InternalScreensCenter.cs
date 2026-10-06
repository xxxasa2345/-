using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SaqerAccountingSystem.Desktop;

public sealed class InternalScreensCenter : UserControl
{
    private readonly AppStore _store;
    private readonly UserProfile _user;
    private readonly ListBox _screens = new();
    private readonly Panel _host = new();
    private readonly Label _subtitle = new();

    public InternalScreensCenter(AppStore store, UserProfile user)
    {
        _store = store;
        _user = user;
        Dock = DockStyle.Fill;
        RightToLeft = RightToLeft.Yes;
        BackColor = Theme.Background;
        Build();
        LoadScreens();
    }

    private void Build()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            BackColor = Color.White,
            Padding = new Padding(24, 12, 24, 8)
        };
        Controls.Add(header);

        header.Controls.Add(new Label
        {
            Text = "مركز الشاشات الداخلية",
            AutoSize = true,
            Font = new Font("Tahoma", 20F, FontStyle.Bold),
            Location = new Point(24, 10)
        });

        _subtitle.Text = "الشاشات: 0";
        _subtitle.AutoSize = true;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.Location = new Point(24, 52);
        header.Controls.Add(_subtitle);

        var body = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 300,
            BackColor = Theme.Background
        };
        Controls.Add(body);

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        body.Panel1.Controls.Add(left);

        var search = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 34,
            PlaceholderText = "بحث باسم الشاشة..."
        };
        left.Controls.Add(search);

        _screens.Dock = DockStyle.Fill;
        _screens.DisplayMember = nameof(ScreenEntry.Display);
        _screens.IntegralHeight = false;
        _screens.Font = new Font("Tahoma", 9.5F);
        _screens.BorderStyle = BorderStyle.FixedSingle;
        _screens.BackColor = Color.White;
        _screens.ForeColor = Theme.Text;
        _screens.IntegralHeight = false;
        left.Controls.Add(_screens);

        search.TextChanged += (_, _) => LoadScreens(search.Text);
        _screens.DoubleClick += (_, _) => OpenSelected();

        var open = new Button { Text = "فتح الشاشة", Dock = DockStyle.Bottom, Height = 40 };
        Theme.StyleButton(open, true);
        open.Click += (_, _) => OpenSelected();
        left.Controls.Add(open);

        _host.Dock = DockStyle.Fill;
        _host.Padding = new Padding(12);
        body.Panel2.Controls.Add(_host);
    }

    private void LoadScreens(string term = "")
    {
        _screens.Items.Clear();

        var source = _user.LegacyScreens
            .Where(x => x.IsShow)
            .Select(x => new ScreenEntry(x.ScreenId, x.Name, x.ScreenTypeName, x.AllowEnter,
                x.AllowSave, x.AllowEdit, x.AllowDelete, x.AllowPrint, x.AllowExport))
            .Where(x => string.IsNullOrWhiteSpace(term) ||
                        x.Display.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Id)
            .ToList();

        foreach (var entry in source)
            _screens.Items.Add(entry);

        _subtitle.Text = $"الشاشات المتاحة: {source.Count} • المستخدم: {_user.FullName} • المجموعة: {_user.Group}";

        if (_screens.Items.Count > 0)
            _screens.SelectedIndex = 0;
    }

    private void OpenSelected()
    {
        if (_screens.SelectedItem is not ScreenEntry entry) return;

        var access = _user.LegacyScreens.FirstOrDefault(x => x.ScreenId == entry.Id);
        if (access is null) return;

        _host.Controls.Clear();
        _host.Controls.Add(new InternalScreenPage(_store, _user, access));
    }

    private sealed record ScreenEntry(
        int Id, string Name, string Type, bool Enter, bool Save, bool Edit, bool Delete, bool Print, bool Export)
    {
        public string Display => $"{Id} - {Name}";
    }
}

public sealed class InternalScreenPage : UserControl
{
    private readonly AppStore _store;
    private readonly UserProfile _user;
    private readonly LegacyScreenAccess _access;
    private readonly Panel _content = new();

    public InternalScreenPage(AppStore store, UserProfile user, LegacyScreenAccess access)
    {
        _store = store;
        _user = user;
        _access = access;
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        RightToLeft = RightToLeft.Yes;
        Build();
        Render();
    }

    private void Build()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 125,
            BackColor = Color.White,
            Padding = new Padding(18)
        };
        Controls.Add(header);

        header.Controls.Add(new Label
        {
            Text = _access.Name,
            AutoSize = true,
            Font = new Font("Tahoma", 18F, FontStyle.Bold),
            Location = new Point(18, 12)
        });

        header.Controls.Add(new Label
        {
            Text = $"ScreenID: {_access.ScreenId} • {_access.ScreenTypeName} • رقم الشاشة: {_access.ScreenNum?.ToString() ?? "-"}",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Location = new Point(18, 48)
        });

        var permissions = string.Join("   ", new[]
        {
            _access.AllowEnter ? "إدخال" : null,
            _access.AllowSave ? "حفظ" : null,
            _access.AllowEdit ? "تعديل" : null,
            _access.AllowDelete ? "حذف" : null,
            _access.AllowPrint ? "طباعة" : null,
            _access.AllowExport ? "تصدير" : null
        }.Where(x => x is not null));

        header.Controls.Add(new Label
        {
            Text = "العمليات المسموحة: " + (string.IsNullOrWhiteSpace(permissions) ? "عرض فقط" : permissions),
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = new Font("Tahoma", 9.5F, FontStyle.Bold),
            Location = new Point(18, 80)
        });

        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(8, 12, 8, 8);
        _content.AutoScroll = true;
        Controls.Add(_content);
    }

    private void Render()
    {
        var module = LegacyScreenRouter.ResolveModule(_access.Name);

        if (module == "sales")
        {
            RenderSales();
            return;
        }
        if (module == "purchases")
        {
            RenderPurchases();
            return;
        }
        if (module == "journals")
        {
            RenderJournals();
            return;
        }
        if (module == "accounts")
        {
            RenderAccounts();
            return;
        }
        if (module == "items")
        {
            RenderItems();
            return;
        }
        if (module == "customers")
        {
            RenderCustomers();
            return;
        }
        if (module == "suppliers")
        {
            RenderSuppliers();
            return;
        }
        if (module == "companies")
        {
            RenderCompanies();
            return;
        }
        if (module == "payments")
        {
            RenderPayments();
            return;
        }
        if (module == "inventory")
        {
            RenderInventory();
            return;
        }
        if (module == "tax")
        {
            RenderTax();
            return;
        }
        if (module == "costcenters")
        {
            RenderCostCenters();
            return;
        }
        if (module == "reports")
        {
            RenderReport();
            return;
        }
        if (module == "users")
        {
            RenderSecurity();
            return;
        }

        RenderGeneric();
    }

    private void RenderSales()
    {
        AddAction("فاتورة مبيعات جديدة", _access.AllowSave,
            () => LegacyOperationForms.OpenSales(FindOwner(), _store, _user));
        AddGrid(_store.Sales.Select(x => new
        {
            المعرف = x.Id, الرقم = x.Number, الطرف = x.Party,
            التاريخ = x.Date == DateTime.MinValue ? "" : x.Date.ToString("yyyy-MM-dd"),
            المبلغ = x.Amount, الحالة = x.Status
        }).ToArray());
    }

    private void RenderPurchases()
    {
        AddAction("فاتورة مشتريات جديدة", _access.AllowSave,
            () => LegacyOperationForms.OpenPurchases(FindOwner(), _store, _user));
        AddGrid(_store.Purchases.Select(x => new
        {
            المعرف = x.Id, الرقم = x.Number, المورد = x.Party,
            التاريخ = x.Date == DateTime.MinValue ? "" : x.Date.ToString("yyyy-MM-dd"),
            المبلغ = x.Amount, الحالة = x.Status
        }).ToArray());
    }

    private void RenderJournals()
    {
        AddAction("قيد يومية جديد", _access.AllowSave,
            () => LegacyOperationForms.OpenJournal(FindOwner(), _store));
        AddGrid(_store.Journals.Select(x => new
        {
            المعرف = x.Id, المستند = x.Number, التاريخ = x.Date.ToString("yyyy-MM-dd"),
            البيان = x.Description, الحالة = x.Status
        }).ToArray());
    }

    private void RenderAccounts()
    {
        AddGrid(_store.Accounts.Select(x => new
        {
            المعرف = x.Id, رقم_الحساب = x.Code, اسم_الحساب = x.Name,
            النوع = x.Type, الرصيد = x.Balance
        }).ToArray());
    }

    private void RenderItems()
    {
        AddGrid(_store.Items.Select(x => new
        {
            المعرف = x.Id, الكود = x.Code, الصنف = x.Name,
            الرصيد = x.StockQuantity, السعر = x.SalePrice, الضريبة = x.TaxRate
        }).ToArray());
    }

    private void RenderCustomers()
    {
        AddGrid(_store.Customers.Select(x => new
        {
            المعرف = x.Id, الكود = x.Code, العميل = x.Name,
            الهاتف = x.Phone, الرصيد = x.Balance
        }).ToArray());
    }

    private void RenderSuppliers()
    {
        AddGrid(_store.Suppliers.Select(x => new
        {
            المعرف = x.Id, الكود = x.Code, المورد = x.Name,
            الهاتف = x.Phone, الرصيد = x.Balance
        }).ToArray());
    }

    private void RenderCompanies()
    {
        AddGrid(_store.Companies.Select(x => new
        {
            المعرف = x.Id, الكود = x.Code, الاسم = x.Name,
            الرقم_الضريبي = x.TaxNumber, العملة = x.Currency,
            الحالة = x.IsActive ? "نشطة" : "متوقفة"
        }).ToArray());
    }

    private void RenderPayments()
    {
        AddGrid(_store.Payments.Select(x => new
        {
            المعرف = x.Id, المستند = x.Number, الحساب = x.PartyType,
            المبلغ = x.Amount, الطريقة = x.Method,
            التاريخ = x.Date.ToString("yyyy-MM-dd")
        }).ToArray());
    }

    private void RenderInventory()
    {
        AddGrid(_store.StockBalances.Select(x => new
        {
            ItemId = x.ItemId, الكود = x.ItemCode, الصنف = x.ItemName,
            المخزن = x.StoreName, الرصيد = x.CurrentBalance, الوحدة = x.UnitNumber
        }).ToArray());
    }

    private void RenderTax()
    {
        AddGrid(_store.Taxes.Select(x => new
        {
            المعرف = x.Id, الكود = x.Code, الضريبة = x.Name,
            النسبة = x.Rate, مبيعات = x.IsSales, مشتريات = x.IsPurchase
        }).ToArray());
    }

    private void RenderCostCenters()
    {
        AddGrid(_store.CostCenters.Select(x => new
        {
            المعرف = x.Id, الكود = x.Code, المركز = x.Name,
            الحالة = x.IsActive ? "نشط" : "متوقف"
        }).ToArray());
    }

    private void RenderReport()
    {
        var box = new TextBox
        {
            Multiline = true, Dock = DockStyle.Fill, ReadOnly = true,
            ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10F)
        };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48 };
        AddActionTo(top, "ميزان المراجعة", _access.AllowEnter, () => LoadReport("trial-balance", box));
        AddActionTo(top, "قائمة الدخل", _access.AllowEnter, () => LoadReport("income-statement", box));
        AddActionTo(top, "الميزانية", _access.AllowEnter, () => LoadReport("balance-sheet", box));
        _content.Controls.Add(top);
        _content.Controls.Add(box);
    }

    private void RenderSecurity()
    {
        var rows = _user.LegacyScreens.Select(x => new
        {
            المعرف = x.ScreenId, الشاشة = x.Name, النوع = x.ScreenTypeName,
            إدخال = x.AllowEnter, حفظ = x.AllowSave, تعديل = x.AllowEdit,
            حذف = x.AllowDelete, طباعة = x.AllowPrint, تصدير = x.AllowExport
        }).ToArray();
        AddGrid(rows);
    }

    private void RenderGeneric()
    {
        var rows = _store.OriginalScreens
            .Where(x => string.Equals(x.Screen, _access.Name, StringComparison.OrdinalIgnoreCase))
            .Select(x => new { الشاشة = x.Screen, المسار = x.File, Namespace = x.Namespace, العناصر = x.Controls })
            .ToArray();

        if (rows.Length > 0)
            AddGrid(rows);
        else
        {
            var label = new Label
            {
                Dock = DockStyle.Fill,
                Text = "هذه شاشة داخلية بديلة.

المصدر الأصلي لـ GTS غير متوفر على الجهاز، لذلك تم إنشاء هذه الصفحة داخل صقر ERP بدلًا منه، مع المحافظة على اسم الشاشة وصلاحياتها.",
                Font = new Font("Tahoma", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _content.Controls.Add(label);
        }
    }

    private void AddAction(string text, bool enabled, Action action)
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50 };
        AddActionTo(top, text, enabled, action);
        _content.Controls.Add(top);
    }

    private static void AddActionTo(Control parent, string text, bool enabled, Action action)
    {
        var button = new Button { Text = text, Width = 165, Height = 38, Enabled = enabled };
        Theme.StyleButton(button, enabled);
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "العملية", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        parent.Controls.Add(button);
    }

    private void AddGrid<T>(T[] rows)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = rows,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        Theme.StyleGrid(grid);
        _content.Controls.Add(grid);
        grid.BringToFront();
    }

    private void LoadReport(string key, TextBox output)
    {
        try { output.Text = _store.GetReport(key); }
        catch (Exception ex) { output.Text = ex.ToString(); }
    }

    private Form FindOwner() => FindForm() ?? new Form();
}
