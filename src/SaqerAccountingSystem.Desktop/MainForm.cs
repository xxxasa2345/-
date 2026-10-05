using System;
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

        Text = $"نظام صقر للمحاسبة - {_user.FullName}";
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
        var side = new Panel
        {
            Dock = DockStyle.Left,
            Width = 250,
            BackColor = Theme.Sidebar,
            Padding = new Padding(14)
        };
        Controls.Add(side);

        var brand = new Label
        {
            Text = "صقر ERP",
            Dock = DockStyle.Top,
            Height = 55,
            ForeColor = Color.White,
            Font = new Font("Tahoma", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        side.Controls.Add(brand);

        var user = new Label
        {
            Text = $"{_user.FullName}\r
{_user.Group}",
            Dock = DockStyle.Top,
            Height = 68,
            ForeColor = Color.FromArgb(198, 214, 219),
            Font = new Font("Tahoma", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        side.Controls.Add(user);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Sidebar
        };
        side.Controls.Add(nav);

        foreach (var m in _store.Modules.Where(x => _user.Permissions.Contains(x.Permission)))
        {
            var b = new Button
            {
                Text = m.Title,
                Tag = m.Key,
                Width = 220,
                Height = 42,
                Margin = new Padding(0, 3, 0, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Sidebar,
                ForeColor = Color.FromArgb(215, 225, 229),
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => ShowModule((string)b.Tag);
            b.MouseEnter += (_, _) => b.BackColor = Theme.SidebarActive;
            b.MouseLeave += (_, _) => b.BackColor = Theme.Sidebar;
            nav.Controls.Add(b);
        }

        var logout = new Button
        {
            Text = "تسجيل الخروج",
            Dock = DockStyle.Bottom,
            Height = 45,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31, 57, 67),
            ForeColor = Color.White,
            Font = new Font("Tahoma", 9F, FontStyle.Bold)
        };
        logout.FlatAppearance.BorderSize = 0;
        logout.Click += (_, _) => Close();
        side.Controls.Add(logout);

        var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        Controls.Add(main);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = Color.White,
            Padding = new Padding(28, 14, 28, 10)
        };
        main.Controls.Add(header);

        _title.Text = "لوحة التحكم";
        _title.Font = new Font("Tahoma", 22F, FontStyle.Bold);
        _title.ForeColor = Theme.Text;
        _title.AutoSize = true;
        _title.Location = new Point(28, 35);
        header.Controls.Add(_title);

        var caption = new Label
        {
            Text = "نظام صقر للمحاسبة",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Location = new Point(28, 12)
        };
        header.Controls.Add(caption);

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
        main.Controls.Add(_content);
    }

    private void ShowModule(string key)
    {
        _content.Controls.Clear();
        var module = _store.Modules.First(x => x.Key == key);
        _title.Text = module.Title;

        Control view = key switch
        {
            "dashboard" => Dashboard(),
            "companies" => Grid("الشركات والفروع", new[]
            {
                new { الرمز="HQ", الاسم="شركة صقر القابضة", الفرع="الرئيسي", الحالة="نشطة" },
                new { الرمز="JED", الاسم="فرع جدة", الفرع="جدة", الحالة="نشطة" },
                new { الرمز="RYD", الاسم="فرع الرياض", الفرع="الرياض", الحالة="نشطة" }
            }),
            "customers" => Grid("العملاء", _store.Customers.Select(x => new { x.Code, x.Name, x.Phone, x.Balance }).ToArray()),
            "suppliers" => Grid("الموردون", new[]
            {
                new { الكود="S-2001", الاسم="شركة التوريد الحديثة", الهاتف="0110000001", الرصيد=-32600m },
                new { الكود="S-2002", الاسم="مورد التجزئة", الهاتف="0110000002", الرصيد=-12800m }
            }),
            "items" => Grid("الأصناف", _store.Items.Select(x => new { x.Code, x.Name, المخزون=x.Stock, السعر=x.SalePrice }).ToArray()),
            "sales" => Grid("المبيعات", _store.Sales.Select(x => new { x.Number, x.Party, التاريخ=x.Date.ToString("yyyy-MM-dd"), المبلغ=x.Amount, x.Status }).ToArray()),
            "purchases" => Grid("المشتريات", _store.Purchases.Select(x => new { x.Number, x.Party, التاريخ=x.Date.ToString("yyyy-MM-dd"), المبلغ=x.Amount, x.Status }).ToArray()),
            "accounts" => Grid("دليل الحسابات", _store.Accounts.Select(x => new { x.Code, x.Name, النوع=x.Type, الرصيد=x.Balance }).ToArray()),
            "inventory" => Grid("المخزون", _store.Items.Select(x => new { الصنف=x.Name, الكود=x.Code, الرصيد=x.Stock, قيمة=x.Stock*x.SalePrice }).ToArray()),
            "journals" => Grid("القيود اليومية", new[]
            {
                new { الرقم="TRX-1024", التاريخ="2026-10-05", البيان="مبيعات نقدية", المدين=18500m, الدائن=18500m, الحالة="معتمد" },
                new { الرقم="TRX-1023", التاريخ="2026-10-05", البيان="شراء آجل", المدين=27400m, الدائن=27400m, الحالة="مسودة" }
            }),
            "reports" => Grid("التقارير المالية", new[]
            {
                new { التقرير="قائمة الدخل", الفترة="أكتوبر 2026", القيمة=486200m },
                new { التقرير="الميزانية العمومية", الفترة="حتى 05 أكتوبر 2026", القيمة=902400m },
                new { التقرير="ميزان المراجعة", الفترة="حتى 05 أكتوبر 2026", القيمة=1649000m }
            }),
            "settings" => Settings(),
            _ => new Label { Text = "الشاشة غير موجودة." }
        };

        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
    }

    private Control Dashboard()
    {
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, AutoScroll = true };

        var hero = new Panel { Dock = DockStyle.Top, Height = 115, BackColor = Theme.Primary, Padding = new Padding(24) };
        hero.Controls.Add(new Label
        {
            Text = $"مرحبًا {_user.FullName}",
            Font = new Font("Tahoma", 23F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 20)
        });
        hero.Controls.Add(new Label
        {
            Text = "Login → Group → Permission → Screen → Operation",
            Font = new Font("Tahoma", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(216, 234, 230),
            AutoSize = true,
            Location = new Point(25, 65)
        });
        root.Controls.Add(hero);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 145,
            Padding = new Padding(0, 16, 0, 0),
            WrapContents = true
        };
        AddCard(cards, "مبيعات اليوم", "128,450 ر.س");
        AddCard(cards, "مشتريات اليوم", "74,200 ر.س");
        AddCard(cards, "ذمم العملاء", "318,600 ر.س");
        AddCard(cards, "قيمة المخزون", "164,900 ر.س");
        root.Controls.Add(cards);

        var recent = Grid("آخر العمليات", new[]
        {
            new { الوقت="08:45", العملية="اعتماد القيد TRX-1024", المستخدم=_user.FullName },
            new { الوقت="09:10", العملية="إنشاء فاتورة INV-2048", المستخدم=_user.FullName },
            new { الوقت="09:25", العملية="تسجيل دفعة من عميل", المستخدم=_user.FullName },
            new { الوقت="10:05", العملية="تحديث المخزون", المستخدم=_user.FullName }
        });
        recent.Dock = DockStyle.Fill;
        root.Controls.Add(recent);
        return root;
    }

    private static void AddCard(Control parent, string title, string value)
    {
        var p = new Panel
        {
            Width = 240,
            Height = 110,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 8),
            BorderStyle = BorderStyle.FixedSingle
        };
        p.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Theme.Muted, Location = new Point(14, 14) });
        p.Controls.Add(new Label { Text = value, AutoSize = true, ForeColor = Theme.Text, Font = new Font("Tahoma", 17F, FontStyle.Bold), Location = new Point(14, 44) });
        parent.Controls.Add(p);
    }

    private static Control Grid<T>(string title, T[] rows)
    {
        var panel = new Panel { BackColor = Theme.Background };
        var top = new Panel { Dock = DockStyle.Top, Height = 55 };
        panel.Controls.Add(top);
        top.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Tahoma", 16F, FontStyle.Bold), Location = new Point(0, 5) });
        var add = new Button { Text = "إضافة جديد", Width = 120, Location = new Point(155, 0) };
        Theme.StyleButton(add);
        add.Click += (_, _) => MessageBox.Show("سيتم ربط الحفظ بقاعدة البيانات الحالية في مرحلة ربط SQL Server.", "صقر");
        top.Controls.Add(add);

        var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = rows };
        Theme.StyleGrid(grid);
        panel.Controls.Add(grid);
        return panel;
    }

    private static Control Settings()
    {
        return new Label
        {
            Text = "إعدادات النظام\r\n\r\n• قاعدة البيانات\r\n• الشركات والفروع\r\n• المستخدمون والمجموعات\r\n• الصلاحيات والعمليات\r\n• الطباعة والتقارير",
            AutoSize = true,
            Font = new Font("Tahoma", 13F),
            Location = new Point(20, 20)
        };
    }
}