using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
        MinimumSize = new Size(1100, 720);
        BackColor = Theme.Background;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Build();
        ShowModule("dashboard");
    }

    private void Build()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = 260, BackColor = Theme.Sidebar, Padding = new Padding(14) };
        Controls.Add(side);

        side.Controls.Add(new Label { Text = "صقر ERP", Dock = DockStyle.Top, Height = 55, ForeColor = Color.White, Font = new Font("Tahoma", 20F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });

        side.Controls.Add(new Label
        {
            Text = _user.FullName + Environment.NewLine + _user.Group,
            Dock = DockStyle.Top, Height = 68, ForeColor = Color.FromArgb(198,214,219),
            Font = new Font("Tahoma", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter
        });

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, BackColor = Theme.Sidebar
        };
        side.Controls.Add(nav);

        foreach (var module in _store.Modules.Where(x => _user.Permissions.Contains(x.Permission, StringComparer.OrdinalIgnoreCase)))
        {
            var button = new Button
            {
                Text = module.Title, Tag = module.Key, Width = 232, Height = 40, Margin = new Padding(0,3,0,3),
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Sidebar, ForeColor = Color.FromArgb(215,225,229),
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => ShowModule((string)button.Tag);
            button.MouseEnter += (_, _) => button.BackColor = Theme.SidebarActive;
            button.MouseLeave += (_, _) => button.BackColor = Theme.Sidebar;
            nav.Controls.Add(button);
        }

        var logout = new Button
        {
            Text = "تسجيل الخروج", Dock = DockStyle.Bottom, Height = 45, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31,57,67), ForeColor = Color.White, Font = new Font("Tahoma", 9F, FontStyle.Bold)
        };
        logout.FlatAppearance.BorderSize = 0;
        logout.Click += (_, _) => Close();
        side.Controls.Add(logout);

        var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        Controls.Add(main);

        var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.White, Padding = new Padding(28,14,28,10) };
        main.Controls.Add(header);
        _title.Text = "لوحة التحكم";
        _title.Font = new Font("Tahoma", 22F, FontStyle.Bold);
        _title.ForeColor = Theme.Text;
        _title.AutoSize = true;
        _title.Location = new Point(28,35);
        header.Controls.Add(_title);
        header.Controls.Add(new Label { Text = "نظام صقر للمحاسبة • SQL Server", AutoSize = true, ForeColor = Theme.Muted, Location = new Point(28,12) });

        var online = new Label
        {
            Text = "● متصل", AutoSize = true, ForeColor = Theme.Primary,
            Font = new Font("Tahoma", 9F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        header.Controls.Add(online);
        header.Resize += (_, _) => online.Left = header.ClientSize.Width - online.Width - 28;

        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(28);
        _content.AutoScroll = true;
        main.Controls.Add(_content);
    }

    private void ShowModule(string key)
    {
        try { _store.Refresh(); }
        catch (Exception ex)
        {
            _content.Controls.Clear();
            _content.Controls.Add(new Label { Text = "تعذر الاتصال بقاعدة البيانات: " + ex.Message, AutoSize = true, ForeColor = Color.Maroon, Location = new Point(20,20) });
            return;
        }

        _content.Controls.Clear();
        var module = _store.Modules.FirstOrDefault(x => x.Key == key);
        _title.Text = module?.Title ?? "النظام";

        Control view = key switch
        {
            "dashboard" => Dashboard(),
            "companies" => Grid("الشركات والفروع", _store.Companies.Select(x => new { x.Code, x.Name, x.TaxNumber, x.Currency, الحالة = x.IsActive ? "نشطة" : "متوقفة" }).ToArray(), key),
            "customers" => Grid("العملاء", _store.Customers.Select(x => new { x.Code, x.Name, x.Phone, x.Balance }).ToArray(), key),
            "suppliers" => Grid("الموردون", _store.Suppliers.Select(x => new { x.Code, x.Name, x.Phone, x.Balance }).ToArray(), key),
            "items" => Grid("الأصناف", _store.Items.Select(x => new { x.Code, x.Name, المخزون = x.StockQuantity, السعر = x.SalePrice }).ToArray(), key),
            "sales" => Grid("فواتير المبيعات", _store.Sales.Select(x => new { x.Number, x.Party, التاريخ = x.Date.ToString("yyyy-MM-dd"), المبلغ = x.Amount, الحالة = x.Status }).ToArray(), key),
            "purchases" => Grid("فواتير المشتريات", _store.Purchases.Select(x => new { x.Number, x.Party, التاريخ = x.Date.ToString("yyyy-MM-dd"), المبلغ = x.Amount, الحالة = x.Status }).ToArray(), key),
            "accounts" => Grid("دليل الحسابات", _store.Accounts.Select(x => new { x.Code, x.Name, x.Type, الرصيد = x.Balance }).ToArray(), key),
            "journals" => Grid("القيود والأستاذ العام", _store.Journals.Select(x => new { x.Number, التاريخ = x.Date.ToString("yyyy-MM-dd"), x.Description, x.Status }).ToArray(), key),
            "payments" => Grid("الخزينة والبنوك", _store.Payments.Select(x => new { x.Number, x.PartyType, x.Amount, x.Method, التاريخ = x.Date.ToString("yyyy-MM-dd") }).ToArray(), key),
            "inventory" => Grid("حركات المخزون", _store.Inventory.Select(x => new { x.ItemId, التاريخ = x.Date.ToString("yyyy-MM-dd"), وارد = x.QuantityIn, صادر = x.QuantityOut, x.UnitCost }).ToArray(), key),
            "tax" => Grid("الضرائب", _store.Taxes.Select(x => new { x.Code, x.Name, النسبة = x.Rate, مبيعات = x.IsSales, مشتريات = x.IsPurchase }).ToArray(), key),
            "assets" => Grid("الأصول الثابتة", _store.Assets.Select(x => new { x.Code, x.Name, التكلفة = x.Cost, مجمع_الإهلاك = x.AccumulatedDepreciation, العمر = x.UsefulLifeMonths }).ToArray(), key),
            "costcenters" => Grid("مراكز التكلفة", _store.CostCenters.Select(x => new { x.Code, x.Name, الحالة = x.IsActive ? "نشط" : "متوقف" }).ToArray(), key),
            "budgets" => Grid("الموازنات", _store.Budgets.Select(x => new { x.AccountId, x.Year, x.Month, x.Amount }).ToArray(), key),
            "reports" => Reports(),
            "settings" => Settings(),
            _ => new Label { Text = "الشاشة غير موجودة.", AutoSize = true }
        };

        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
    }

    private Control Dashboard()
    {
        var data = _store.GetDashboard();
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, AutoScroll = true };

        var hero = new Panel { Dock = DockStyle.Top, Height = 115, BackColor = Theme.Primary, Padding = new Padding(24) };
        hero.Controls.Add(new Label
        {
            Text = "مرحبًا " + _user.FullName,
            Font = new Font("Tahoma", 23F, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(24,20)
        });
        hero.Controls.Add(new Label
        {
            Text = "البيانات من SQL Server • القيد المزدوج • الترحيل • التدقيق",
            Font = new Font("Tahoma", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(216,234,230), AutoSize = true, Location = new Point(25,65)
        });
        root.Controls.Add(hero);

        var cards = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 145, Padding = new Padding(0,16,0,0), WrapContents = true };
        foreach (var card in data.Cards)
            AddCard(cards, card.Title, card.Value.ToString("N2") + " ر.س");
        root.Controls.Add(cards);

        var recent = Grid("آخر الفواتير", data.RecentInvoices.Select(x => new
        {
            x.Number, x.Party, x.Date, المبلغ = x.Amount, الحالة = x.Status
        }).ToArray(), "dashboard");
        recent.Dock = DockStyle.Fill;
        root.Controls.Add(recent);
        return root;
    }

    private static void AddCard(Control parent, string title, string value)
    {
        var p = new Panel { Width = 240, Height = 110, BackColor = Color.White, Margin = new Padding(0,0,12,8), BorderStyle = BorderStyle.FixedSingle };
        p.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Theme.Muted, Location = new Point(14,14) });
        p.Controls.Add(new Label { Text = value, AutoSize = true, ForeColor = Theme.Text, Font = new Font("Tahoma",17F,FontStyle.Bold), Location = new Point(14,44) });
        parent.Controls.Add(p);
    }

    private Control Reports()
    {
        var panel = new Panel { BackColor = Theme.Background };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70 };
        panel.Controls.Add(top);

        var output = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, Font = new Font("Consolas",9F), ReadOnly = true };
        panel.Controls.Add(output);

        AddReportButton(top, "ميزان المراجعة", "trial-balance", output);
        AddReportButton(top, "قائمة الدخل", "income-statement", output);
        AddReportButton(top, "الميزانية العمومية", "balance-sheet", output);
        return panel;
    }

    private void AddReportButton(Control parent, string title, string key, TextBox output)
    {
        var button = new Button { Text = title, Width = 150, Height = 42, Margin = new Padding(4) };
        Theme.StyleButton(button);
        button.Click += (_, _) =>
        {
            try { output.Text = _store.GetReport(key); }
            catch (Exception ex) { output.Text = ex.ToString(); }
        };
        parent.Controls.Add(button);
    }

    private static Control Grid<T>(string title, T[] rows, string key)
    {
        var panel = new Panel { BackColor = Theme.Background };
        var top = new Panel { Dock = DockStyle.Top, Height = 55 };
        panel.Controls.Add(top);
        top.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Tahoma",16F,FontStyle.Bold), Location = new Point(0,5) });

        var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = rows };
        Theme.StyleGrid(grid);
        panel.Controls.Add(grid);
        return panel;
    }

    private static Control Settings()
    {
        return new Label
        {
            Text = "إعدادات النظام\r\n\r\n• محرك البيانات: SQL Server\r\n• ORM: Entity Framework Core\r\n• مصادقة: جلسات مخزنة في قاعدة البيانات\r\n• صلاحيات: Groups / Permissions\r\n• محاسبة: قيد مزدوج وترحيل المستندات\r\n• مخزون: حركات وارد/صادر وتحديث الرصيد\r\n• تدقيق: Audit Log للعمليات",
            AutoSize = true, Font = new Font("Tahoma",13F), Location = new Point(20,20)
        };
    }
}
