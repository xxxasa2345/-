using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SaqerAccountingSystem.Desktop;

public sealed class MainForm : Form
{
    private readonly AppStore _store;
    private readonly UserProfile _user;
    private readonly Panel _content = new();
    private readonly Label _title = new();

    public MainForm(AppStore store, UserProfile user)
    {
        _store = store;
        _user = user;
        Text = "نظام صقر للمحاسبة - " + _user.FullName;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1200, 760);
        BackColor = Theme.Background;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Build();

        if (_user.Permissions.Any())
            ShowModule("dashboard");
    }

    private void Build()
    {
        var side = new Panel { Dock = DockStyle.Right, Width = 285, BackColor = Theme.Sidebar, Padding = new Padding(14) };
        Controls.Add(side);

        side.Controls.Add(new Label
        {
            Text = "صقر ERP",
            Dock = DockStyle.Top,
            Height = 58,
            ForeColor = Color.White,
            Font = new Font("Tahoma", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });

        side.Controls.Add(new Label
        {
            Text = _user.FullName + Environment.NewLine + _user.Group,
            Dock = DockStyle.Top,
            Height = 72,
            ForeColor = Color.FromArgb(198,214,219),
            Font = new Font("Tahoma", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Sidebar
        };
        side.Controls.Add(nav);

        foreach (var module in _store.Modules.Where(HasModuleAccess))
        {
            var button = new Button
            {
                Text = module.Title,
                Tag = module.Key,
                Width = 255,
                Height = 43,
                Margin = new Padding(0,3,0,3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Sidebar,
                ForeColor = Color.FromArgb(215,225,229),
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => ShowModule((string)button.Tag);
            button.MouseEnter += (_, _) => button.BackColor = Theme.SidebarActive;
            button.MouseLeave += (_, _) => button.BackColor = Theme.Sidebar;
            nav.Controls.Add(button);
        }

        var logout = new Button
        {
            Text = "تسجيل الخروج",
            Dock = DockStyle.Bottom,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31,57,67),
            ForeColor = Color.White,
            Font = new Font("Tahoma", 9F, FontStyle.Bold)
        };
        logout.FlatAppearance.BorderSize = 0;
        logout.Click += (_, _) => Close();
        side.Controls.Add(logout);

        var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        Controls.Add(main);

        // Add the fill content first; the header is brought to the front so
        // DockStyle.Fill cannot cover it and the selected screen remains visible.
        main.Controls.Add(_content);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            BackColor = Color.White,
            Padding = new Padding(28,14,28,10)
        };
        main.Controls.Add(header);

        _title.Text = "لوحة التحكم";
        _title.Font = new Font("Tahoma", 22F, FontStyle.Bold);
        _title.ForeColor = Theme.Text;
        _title.AutoSize = true;
        _title.Location = new Point(28,35);
        header.Controls.Add(_title);

        header.Controls.Add(new Label
        {
            Text = "نظام صقر للمحاسبة • مصدر البيانات: " + _store.DataSourceName + " • بيانات فعلية",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Location = new Point(28,12)
        });

        var online = new Label
        {
            Text = "● متصل",
            AutoSize = true,
            ForeColor = Theme.Primary,
            Font = new Font("Tahoma", 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        header.Controls.Add(online);
        header.Resize += (_, _) => online.Left = header.ClientSize.Width - online.Width - 28;

        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(28);
        _content.AutoScroll = true;
        _content.BackColor = Theme.Background;
        header.BringToFront();
    }

    private bool HasModuleAccess(ModuleInfo module)
        => module.Key is "dashboard" or "legacy-screens"
            ? _user.Permissions.Count > 0
            : _user.Permissions.Contains(module.Permission, StringComparer.OrdinalIgnoreCase);

    private void ShowModule(string key)
    {
        if (key != "dashboard" && key != "legacy-screens")
        {
            var moduleAccess = _store.Modules.FirstOrDefault(x => x.Key == key);
            if (moduleAccess is null || !_user.Permissions.Contains(moduleAccess.Permission, StringComparer.OrdinalIgnoreCase))
            {
                ShowError("لا تملك صلاحية فتح هذه الشاشة.");
                return;
            }
        }

        try { _store.Refresh(); }
        catch (Exception ex)
        {
            ShowError("تعذر الاتصال بقاعدة البيانات: " + ex.Message);
            return;
        }

        _content.Controls.Clear();

        var module = _store.Modules.FirstOrDefault(x => x.Key == key);
        _title.Text = module?.Title ?? key switch
        {
            "dashboard" => "لوحة التحكم",
            "legacy-screens" => "شاشات النظام الأصلية",
            _ => "النظام"
        };

        Control view = key switch
        {
            "dashboard" => Dashboard(),
            "companies" => _store.IsLegacyMode
                ? GridScreen("الفروع الفعلية", _store.Branches.Select(x => new { المعرف = x.Id, x.Name, رقم_الحساب = x.AccountNo, x.Phone, x.Fax, x.Address }).ToArray())
                : GridScreen("الشركات والفروع", _store.Companies.Select(x => new { المعرف = x.Id, x.Code, x.Name, x.TaxNumber, x.Currency, الحالة = x.IsActive ? "نشطة" : "متوقفة" }).ToArray()),
            "customers" => GridScreen("العملاء والذمم", _store.Customers.Select(x => new { المعرف = x.Id, x.Code, x.Name, x.Phone, الرصيد = x.Balance }).ToArray()),
            "suppliers" => GridScreen("الموردون والدائنون", _store.Suppliers.Select(x => new { المعرف = x.Id, x.Code, x.Name, x.Phone, الرصيد = x.Balance }).ToArray()),
            "items" => GridScreen("الأصناف", _store.Items.Select(x => new { المعرف = x.Id, x.Code, x.Name, الرصيد = x.StockQuantity, السعر = x.SalePrice }).ToArray()),
            "sales" => SalesScreen(),
            "purchases" => PurchasesScreen(),
            "sales-returns" => GridScreen("مرتجعات المبيعات - بيانات فعلية", _store.SalesReturns.Select(x => new
            {
                المعرف = x.Id, x.Number, x.Party, التاريخ = x.Date == DateTime.MinValue ? "" : x.Date.ToString("yyyy-MM-dd"),
                المبلغ = x.Amount, الحالة = x.Status
            }).ToArray()),
            "purchase-returns" => GridScreen("مرتجعات المشتريات - بيانات فعلية", _store.PurchaseReturns.Select(x => new
            {
                المعرف = x.Id, x.Number, x.Party, التاريخ = x.Date == DateTime.MinValue ? "" : x.Date.ToString("yyyy-MM-dd"),
                المبلغ = x.Amount, الحالة = x.Status
            }).ToArray()),
            "accounts" => AccountsScreen(),
            "journals" => JournalsScreen(),
            "payments" => GridScreen("الخزينة والبنوك", _store.Payments.Select(x => new { المعرف = x.Id, x.Number, x.PartyType, x.Amount, x.Method, التاريخ = x.Date.ToString("yyyy-MM-dd") }).ToArray()),
            "inventory" => _store.IsLegacyMode
                ? GridScreen("أرصدة المخزون الفعلية حسب المخزن", _store.StockBalances.Select(x => new { x.ItemId, x.ItemCode, x.ItemName, المخزن = x.StoreName, الرصيد = x.CurrentBalance, عدد_الوحدات = x.UnitNumber }).ToArray())
                : GridScreen("حركات المخزون", _store.Inventory.Select(x => new { المعرف = x.Id, x.ItemId, التاريخ = x.Date.ToString("yyyy-MM-dd"), وارد = x.QuantityIn, صادر = x.QuantityOut, x.UnitCost }).ToArray()),
            "tax" => GridScreen("الضرائب", _store.Taxes.Select(x => new { المعرف = x.Id, x.Code, x.Name, النسبة = x.Rate, مبيعات = x.IsSales, مشتريات = x.IsPurchase }).ToArray()),
            "stores" => GridScreen("المخازن الفعلية", _store.Stores.Select(x => new { المعرف = x.Id, x.Name, x.BranchId, x.Address, x.Phone }).ToArray()),
            "assets" => GridScreen("الأصول الثابتة", _store.Assets.Select(x => new { المعرف = x.Id, x.Code, x.Name, التكلفة = x.Cost, مجمع_الإهلاك = x.AccumulatedDepreciation, العمر = x.UsefulLifeMonths }).ToArray()),
            "costcenters" => GridScreen("مراكز التكلفة", _store.CostCenters.Select(x => new { المعرف = x.Id, x.Code, x.Name, الحالة = x.IsActive ? "نشط" : "متوقف" }).ToArray()),
            "budgets" => GridScreen("الموازنات", _store.Budgets.Select(x => new { المعرف = x.Id, x.AccountId, x.Year, x.Month, x.Amount }).ToArray()),
            "reports" => Reports(),
            "settings" => Settings(),
            "users" => UsersAndPermissions(),
            "legacy-screens" => LegacyScreens(),
            "original-catalog" => OriginalGtsCatalog(),
            _ => new Label { Text = "الشاشة غير موجودة.", AutoSize = true }
        };

        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
        view.BringToFront();
    }

    private Control Dashboard()
    {
        var data = _store.GetDashboard();
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, AutoScroll = true };

        var hero = new Panel
        {
            Dock = DockStyle.Top,
            Height = 125,
            BackColor = Theme.Primary,
            Padding = new Padding(24)
        };
        hero.Controls.Add(new Label
        {
            Text = "مرحبًا " + _user.FullName,
            Font = new Font("Tahoma", 23F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24,20)
        });
        hero.Controls.Add(new Label
        {
            Text = "قاعدة البيانات: GtsDb2026  •  المجموعة: " + _user.Group +
                   "  •  الفرع: " + (_user.LegacyScreens.Count > 0 ? "مربوط بالصلاحيات" : "النظام الداخلي"),
            Font = new Font("Tahoma", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(25,68)
        });
        root.Controls.Add(hero);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(0,16,0,0),
            WrapContents = true
        };
        foreach (var card in data.Cards)
            AddCard(cards, card.Title, card.Value.ToString("N0"));
        root.Controls.Add(cards);

        var info = new Label
        {
            Text = "هذه الأرقام تُقرأ مباشرة من قاعدة ماجد سوفت. افتح المبيعات أو المشتريات ثم اضغط مرتين على الفاتورة لعرض تفاصيلها.",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Tahoma", 10F, FontStyle.Bold),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleRight
        };
        root.Controls.Add(info);

        var recent = GridScreen("آخر الفواتير", data.RecentInvoices.Select(x => new
        {
            x.Number, x.Party, x.Date, المبلغ = x.Amount, الحالة = x.Status
        }).ToArray());
        recent.Dock = DockStyle.Fill;
        root.Controls.Add(recent);
        // Keep the lower grid from covering the dashboard header/cards.
        info.BringToFront();
        cards.BringToFront();
        hero.BringToFront();
        return root;
    }

    private Control SalesScreen()
    {
        var screen = GridScreen("فواتير المبيعات - بيانات فعلية", _store.Sales.Select(x => new
        {
            المعرف = x.Id, x.Number, x.Party, التاريخ = x.Date == DateTime.MinValue ? "" : x.Date.ToString("yyyy-MM-dd"),
            المبلغ = x.Amount, الحالة = x.Status
        }).ToArray());

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight };
        if (_user.Permissions.Contains("sales.create", StringComparer.OrdinalIgnoreCase))
        {
            var create = new Button { Text = "فاتورة مبيعات جديدة", Width = 165, Height = 38 };
            Theme.StyleButton(create, true);
            create.Click += (_, _) =>
            {
                LegacyOperationForms.OpenSales(this, _store, _user);
                ShowModule("sales");
            };
            actions.Controls.Add(create);
        }
        screen.Controls.Add(actions);
        actions.BringToFront();

        var grid = screen.Controls.OfType<DataGridView>().FirstOrDefault();
        if (grid is not null)
        {
            grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && int.TryParse(Convert.ToString(grid.Rows[e.RowIndex].Cells[0].Value), out var id))
                    ShowSaleDetails(id);
            };
        }
        return screen;
    }

    private Control PurchasesScreen()
    {
        var screen = GridScreen("فواتير المشتريات - بيانات فعلية", _store.Purchases.Select(x => new
        {
            المعرف = x.Id, x.Number, x.Party, التاريخ = x.Date == DateTime.MinValue ? "" : x.Date.ToString("yyyy-MM-dd"),
            المبلغ = x.Amount, الحالة = x.Status
        }).ToArray());

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight };
        if (_user.Permissions.Contains("purchases.create", StringComparer.OrdinalIgnoreCase))
        {
            var create = new Button { Text = "فاتورة مشتريات جديدة", Width = 165, Height = 38 };
            Theme.StyleButton(create, true);
            create.Click += (_, _) =>
            {
                LegacyOperationForms.OpenPurchases(this, _store, _user);
                ShowModule("purchases");
            };
            actions.Controls.Add(create);
        }
        screen.Controls.Add(actions);
        actions.BringToFront();

        var grid = screen.Controls.OfType<DataGridView>().FirstOrDefault();
        if (grid is not null)
        {
            grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && int.TryParse(Convert.ToString(grid.Rows[e.RowIndex].Cells[0].Value), out var id))
                    ShowPurchaseDetails(id);
            };
        }
        return screen;
    }

    private Control JournalsScreen()
    {
        var screen = GridScreen("القيود والأستاذ العام - بيانات فعلية", _store.Journals.Select(x => new
        {
            المعرف = x.Id, x.Number, التاريخ = x.Date.ToString("yyyy-MM-dd"), x.Description, x.Status
        }).ToArray());

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight };
        if (_user.Permissions.Contains("journals.create", StringComparer.OrdinalIgnoreCase))
        {
            var create = new Button { Text = "قيد يومية جديد", Width = 145, Height = 38 };
            Theme.StyleButton(create, true);
            create.Click += (_, _) =>
            {
                LegacyOperationForms.OpenJournal(this, _store);
                ShowModule("journals");
            };
            actions.Controls.Add(create);
        }
        screen.Controls.Add(actions);
        actions.BringToFront();
        return screen;
    }

    private Control AccountsScreen()
    {
        var screen = GridScreen("دليل الحسابات - الرصيد المحسوب من القيود", _store.Accounts.Select(x => new
        {
            المعرف = x.Id, x.Code, x.Name, x.Type, الرصيد = x.Balance
        }).ToArray());

        var grid = screen.Controls.OfType<DataGridView>().FirstOrDefault();
        if (grid is not null)
        {
            grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && int.TryParse(Convert.ToString(grid.Rows[e.RowIndex].Cells[0].Value), out var id))
                    ShowAccountLedger(id);
            };
        }
        return screen;
    }

    private Control UsersAndPermissions()
    {
        var panel = new Panel { BackColor = Theme.Background };
        var top = new Label
        {
            Dock = DockStyle.Top,
            Height = 90,
            Text = "إدارة المستخدم والصلاحيات\r\n\r\nالمستخدم: " + _user.Username +
                   "   |   المجموعة: " + _user.Group +
                   "   |   عدد الشاشات المربوطة: " + _user.LegacyScreens.Count,
            Font = new Font("Tahoma", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        panel.Controls.Add(top);

        var rows = _user.LegacyScreens.Select(x => new
        {
            المعرف = x.ScreenId,
            الشاشة = x.Name,
            النوع = x.ScreenTypeName,
            إدخال = x.AllowEnter,
            حفظ = x.AllowSave,
            تعديل = x.AllowEdit,
            حذف = x.AllowDelete,
            طباعة = x.AllowPrint,
            تصدير = x.AllowExport,
            فرع = x.AllowBranch
        }).ToArray();

        var screenPanel = GridScreen("صلاحيات المجموعة الحالية", rows);
        screenPanel.Dock = DockStyle.Fill;
        panel.Controls.Add(screenPanel);
        return panel;
    }

    private Control OriginalGtsCatalog()
    {
        var panel = new Panel { BackColor = Theme.Background };
        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 75,
            Text = "فهرس الشاشات الأصلية لـ GTS ERP\r\n" +
                   "الشاشات المفهرسة: " + _store.OriginalScreens.Count +
                   " — هذا الفهرس يطابق ملفات المصدر الأصلية التي تم تحليلها.",
            Font = new Font("Tahoma", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        panel.Controls.Add(header);

        var rows = _store.OriginalScreens.Select(x => new
        {
            الشاشة = x.Screen,
            المسار = x.File,
            Namespace = x.Namespace,
            العناصر = x.Controls
        }).ToArray();

        var gridPanel = GridScreen("شاشات GTS الأصلية", rows);
        gridPanel.Dock = DockStyle.Fill;
        panel.Controls.Add(gridPanel);
        return panel;
    }

    private Control LegacyScreens()
    {
        var panel = new Panel { BackColor = Theme.Background };

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 72,
            Text = "الشاشات الفعلية المرتبطة بحساب " + _user.Username +
                   "\r\nانقر مرتين على أي شاشة لفتح الوحدة العملية المرتبطة بها.",
            Font = new Font("Tahoma", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        panel.Controls.Add(header);

        var rows = _user.LegacyScreens.Select(x => new
        {
            المعرف = x.ScreenId,
            الشاشة = x.Name,
            الوحدة = LegacyScreenRouter.ResolveModule(x.Name) switch
            {
                "sales" => "المبيعات",
                "purchases" => "المشتريات",
                "accounts" => "الحسابات",
                "journals" => "القيود والأستاذ",
                "items" => "الأصناف",
                "customers" => "العملاء",
                "suppliers" => "الموردون",
                "payments" => "الخزينة والبنوك",
                "inventory" => "المخزون",
                "tax" => "الضريبة",
                "reports" => "التقارير",
                "users" => "المستخدمون والصلاحيات",
                "companies" => "الشركات والفروع",
                _ => "مساحة شاشة"
            },
            النوع = x.ScreenTypeName,
            رقم = x.ScreenNum,
            إدخال = x.AllowEnter,
            حفظ = x.AllowSave,
            تعديل = x.AllowEdit,
            حذف = x.AllowDelete,
            طباعة = x.AllowPrint,
            تصدير = x.AllowExport
        }).ToArray();

        var gridPanel = GridScreen("جميع الشاشات الأصلية المرتبطة بالمستخدم", rows);
        gridPanel.Dock = DockStyle.Fill;
        panel.Controls.Add(gridPanel);

        var grid = gridPanel.Controls.OfType<DataGridView>().FirstOrDefault();
        if (grid is not null)
        {
            grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _user.LegacyScreens.Count) return;
                var access = _user.LegacyScreens[e.RowIndex];
                var module = LegacyScreenRouter.ResolveModule(access.Name);

                if (string.IsNullOrWhiteSpace(module))
                {
                    ShowDetailsDialog("مساحة الشاشة: " + access.Name, new[]
                    {
                        new
                        {
                            الشاشة = access.Name,
                            المعرف = access.ScreenId,
                            الحالة = "تم إنشاء مضيف شاشة عام؛ منطق المصدر الأصلي غير موجود على الجهاز.",
                            إدخال = access.AllowEnter,
                            حفظ = access.AllowSave,
                            تعديل = access.AllowEdit,
                            حذف = access.AllowDelete,
                            طباعة = access.AllowPrint,
                            تصدير = access.AllowExport
                        }
                    });
                    return;
                }

                if (module != "dashboard")
                {
                    var info = _store.Modules.FirstOrDefault(x => x.Key == module);
                    if (info is not null && !_user.Permissions.Contains(info.Permission, StringComparer.OrdinalIgnoreCase))
                    {
                        ShowError("هذه الشاشة موجودة في User_Screens، لكن المجموعة الحالية لا تملك صلاحية الوحدة المقابلة.");
                        return;
                    }
                }

                ShowModule(module);
            };
        }

        return panel;
    }

    private void ShowSaleDetails(int id)
    {
        try
        {
            var details = _store.GetLegacySaleDetails(id);
            ShowDetailsDialog("تفاصيل فاتورة المبيعات #" + id,
                details.Select(x => new
                {
                    المعرف = x.ItemId, الكود = x.ItemCode, الصنف = x.ItemName, الكمية = x.Quantity,
                    سعر_الوحدة = x.UnitPrice, الإجمالي = x.TotalPrice, الضريبة = x.Vat, الصافي = x.NetTotalPrice
                }).ToArray());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "تفاصيل الفاتورة", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowPurchaseDetails(int id)
    {
        try
        {
            var details = _store.GetLegacyPurchaseDetails(id);
            ShowDetailsDialog("تفاصيل فاتورة المشتريات #" + id,
                details.Select(x => new
                {
                    المعرف = x.ItemId, الكود = x.ItemCode, الصنف = x.ItemName, الكمية = x.Quantity,
                    سعر_الوحدة = x.UnitPrice, الإجمالي = x.TotalPrice, الضريبة = x.Vat, الصافي = x.NetTotalPrice
                }).ToArray());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "تفاصيل الفاتورة", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowAccountLedger(int id)
    {
        try
        {
            var ledger = _store.GetLegacyAccountLedger(id);
            ShowDetailsDialog("حركة الحساب #" + id,
                ledger.Select(x => new
                {
                    المعرف = x.TranId, المستند = x.DocCode, التاريخ = x.Date?.ToString("yyyy-MM-dd"),
                    البيان = string.IsNullOrWhiteSpace(x.Description) ? x.Note : x.Description,
                    مدين = x.Debit, دائن = x.Credit, مركز_التكلفة = x.CostCenterId
                }).ToArray());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "دفتر الأستاذ", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowDetailsDialog<T>(string title, T[] rows)
    {
        using var form = new Form
        {
            Text = title,
            Width = 1050,
            Height = 650,
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        form.Controls.Add(GridScreen(title, rows));
        form.ShowDialog(this);
    }

    private static void AddCard(Control parent, string title, string value)
    {
        var p = new Panel
        {
            Width = 220,
            Height = 110,
            BackColor = Color.White,
            Margin = new Padding(0,0,12,8),
            BorderStyle = BorderStyle.FixedSingle
        };
        p.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Theme.Muted, Location = new Point(14,14) });
        p.Controls.Add(new Label { Text = value, AutoSize = true, ForeColor = Theme.Text, Font = new Font("Tahoma",17F,FontStyle.Bold), Location = new Point(14,44) });
        parent.Controls.Add(p);
    }

    private Control Reports()
    {
        var panel = new Panel { BackColor = Theme.Background };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70 };
        var output = new TextBox
        {
            Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9F), ReadOnly = true, BackColor = Color.White
        };

        panel.Controls.Add(output);
        panel.Controls.Add(top);
        top.BringToFront();

        AddReportButton(top, "ميزان المراجعة", "trial-balance", output);
        if (!_store.IsLegacyMode)
        {
            AddReportButton(top, "قائمة الدخل", "income-statement", output);
            AddReportButton(top, "الميزانية العمومية", "balance-sheet", output);
        }
        else
        {
            AddReportButton(top, "ملخص النشاط", "activity-summary", output);
            AddReportButton(top, "حركة الخزينة", "treasury", output);
            var info = new Label
            {
                AutoSize = true,
                Text = "التقارير تُقرأ مباشرة من GTS: القيود والمبيعات والمشتريات والمرتجعات والخزينة.",
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                ForeColor = Theme.Muted,
                Margin = new Padding(12, 10, 0, 0)
            };
            top.Controls.Add(info);
        }

        return panel;
    }

    private void AddReportButton(Control parent, string title, string key, TextBox output)
    {
        var button = new Button { Text = title, Width = 160, Height = 42, Margin = new Padding(4) };
        Theme.StyleButton(button);
        button.Click += (_, _) =>
        {
            try { output.Text = _store.GetReport(key); }
            catch (Exception ex) { output.Text = ex.ToString(); }
        };
        parent.Controls.Add(button);
    }

    private Control GridScreen<T>(string title, T[] rows)
    {
        var panel = new Panel { BackColor = Theme.Background };
        var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Background };
        panel.Controls.Add(top);

        top.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Tahoma", 16F, FontStyle.Bold),
            Location = new Point(0,5)
        });

        var search = new TextBox
        {
            Width = 260,
            Height = 30,
            PlaceholderText = "بحث داخل الشاشة...",
            Location = new Point(0,36)
        };
        top.Controls.Add(search);

        var refresh = new Button { Text = "تحديث", Width = 90, Height = 30, Location = new Point(270,35) };
        refresh.Click += (_, _) =>
        {
            var key = _store.Modules.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase))?.Key;
            if (!string.IsNullOrWhiteSpace(key)) ShowModule(key);
        };
        top.Controls.Add(refresh);

        var export = new Button { Text = "تصدير CSV", Width = 105, Height = 30, Location = new Point(365,35) };
        top.Controls.Add(export);

        var count = new Label
        {
            AutoSize = true,
            Text = "السجلات: " + rows.Length.ToString(),
            ForeColor = Theme.Muted,
            Location = new Point(480,41)
        };
        top.Controls.Add(count);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = rows,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        Theme.StyleGrid(grid);
        panel.Controls.Add(grid);
        top.BringToFront();
        export.Click += (_, _) => ExportGrid(grid);

        search.TextChanged += (_, _) =>
        {
            var term = search.Text.Trim();
            foreach (DataGridViewRow row in grid.Rows)
            {
                var visible = string.IsNullOrWhiteSpace(term) ||
                              row.Cells.Cast<DataGridViewCell>()
                                  .Any(c => Convert.ToString(c.Value)?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
                row.Visible = visible;
            }
            count.Text = "السجلات: " + grid.Rows.Cast<DataGridViewRow>().Count(x => x.Visible).ToString();
        };

        return panel;
    }

    private static void ExportGrid(DataGridView grid)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = "saqer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", grid.Columns.Cast<DataGridViewColumn>().Select(c => Csv(c.HeaderText))));
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.Visible) continue;
            sb.AppendLine(string.Join(",", row.Cells.Cast<DataGridViewCell>().Select(c => Csv(Convert.ToString(c.Value) ?? ""))));
        }
        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
    }

    private static string Csv(string value)
        => "\"" + value.Replace("\"", "\"\"") + "\"";

    private Control Settings()
    {
        return new Label
        {
            Text = "إعدادات الاتصال والنظام\r\n\r\n" +
                   "قاعدة البيانات القديمة: GtsDb2026\r\n" +
                   "خادم SQL Server: .\\SQLEXPRESS\r\n" +
                   "المستخدم الحالي: " + _user.Username + "\r\n" +
                   "نظام الصلاحيات: User_Login → User_Groups → User_Permission → User_Screens\r\n" +
                   "الوضع: تشغيل بيانات حقيقية من قاعدة ماجد سوفت",
            AutoSize = true,
            Font = new Font("Tahoma",13F),
            Location = new Point(20,20)
        };
    }

    private void ShowError(string message)
    {
        _content.Controls.Clear();
        _content.Controls.Add(new Label
        {
            Text = message,
            AutoSize = true,
            ForeColor = Color.Maroon,
            Location = new Point(20,20),
            Font = new Font("Tahoma", 11F)
        });
    }
}
