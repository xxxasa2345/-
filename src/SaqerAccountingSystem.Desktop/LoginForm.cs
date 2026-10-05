using System;
using System.Drawing;
using System.Windows.Forms;

namespace SaqerAccountingSystem.Desktop;

public sealed class LoginForm : Form
{
    private readonly AppStore _store = new();
    private TextBox _username = null!;
    private TextBox _password = null!;
    private Label _error = null!;

    public LoginForm()
    {
        Text = "نظام صقر للمحاسبة";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 620);
        MinimumSize = new Size(900, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Theme.Background;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildUi();
    }

    private void BuildUi()
    {
        var intro = new Panel
        {
            Dock = DockStyle.Left,
            Width = 500,
            BackColor = Theme.Sidebar,
            Padding = new Padding(60)
        };
        Controls.Add(intro);

        var seal = new Label
        {
            Text = "ص",
            AutoSize = false,
            Width = 82,
            Height = 82,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Theme.Gold,
            ForeColor = Theme.Sidebar,
            Font = new Font("Tahoma", 32F, FontStyle.Bold),
            Location = new Point(60, 95)
        };
        intro.Controls.Add(seal);

        var title = new Label
        {
            Text = "نظام صقر للمحاسبة",
            ForeColor = Color.White,
            Font = new Font("Tahoma", 28F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(60, 205)
        };
        intro.Controls.Add(title);

        var desc = new Label
        {
            Text = "تطبيق محاسبي مكتبي لإدارة الشركات، العملاء، الموردين، المبيعات، المشتريات، المخزون والقيود والتقارير.",
            ForeColor = Color.FromArgb(200, 215, 220),
            Font = new Font("Tahoma", 12F),
            MaximumSize = new Size(370, 120),
            AutoSize = true,
            Location = new Point(60, 260)
        };
        intro.Controls.Add(desc);

        var card = new Panel
        {
            Width = 350,
            Height = 420,
            BackColor = Color.White,
            Location = new Point(575, 90),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(28)
        };
        Controls.Add(card);

        var heading = new Label
        {
            Text = "تسجيل الدخول",
            Font = new Font("Tahoma", 22F, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(28, 30)
        };
        card.Controls.Add(heading);

        AddLabel(card, "اسم المستخدم", 28, 90);
        _username = AddTextBox(card, "مدير", 28, 120);

        AddLabel(card, "كلمة المرور", 28, 175);
        _password = AddTextBox(card, "", 28, 205);
        _password.UseSystemPasswordChar = true;

        var login = new Button
        {
            Text = "دخول النظام",
            Location = new Point(28, 265),
            Width = 292
        };
        Theme.StyleButton(login, true);
        login.Click += (_, _) => Login();
        card.Controls.Add(login);

        var info = new Label
        {
            Text = "يتم التحقق من الحسابات والصلاحيات من قاعدة GtsDb2026.",
            Font = new Font("Tahoma", 9F),
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(28, 325)
        };
        card.Controls.Add(info);

        _error = new Label
        {
            AutoSize = false,
            Width = 292,
            Height = 36,
            ForeColor = Color.Maroon,
            Location = new Point(28, 375),
            TextAlign = ContentAlignment.MiddleCenter
        };
        card.Controls.Add(_error);

        AcceptButton = login;
        ActiveControl = _username;
    }

    private static void AddLabel(Control parent, string text, int x, int y)
        => parent.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Tahoma", 9F, FontStyle.Bold),
            ForeColor = Theme.Text,
            Location = new Point(x, y)
        });

    private static TextBox AddTextBox(Control parent, string value, int x, int y)
    {
        var t = new TextBox
        {
            Text = value,
            Width = 292,
            Height = 34,
            Font = new Font("Tahoma", 10F),
            Location = new Point(x, y)
        };
        parent.Controls.Add(t);
        return t;
    }

    private void Login()
    {
        var user = _store.Authenticate(_username.Text.Trim(), _password.Text);
        if (user is null)
        {
            _error.Text = "اسم المستخدم أو كلمة المرور غير صحيحة.";
            return;
        }

        Hide();
        using var main = new MainForm(_store, user);
        main.ShowDialog(this);
        Show();
    }
}
