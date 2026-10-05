using System.Drawing;
using System.Windows.Forms;

namespace SaqerAccountingSystem.Desktop;

public static class LegacyOperationForms
{
    public static void OpenSales(Form owner, AppStore store, UserProfile user)
        => new LegacyInvoiceForm(owner, store, user, false).ShowDialog(owner);

    public static void OpenPurchases(Form owner, AppStore store, UserProfile user)
        => new LegacyInvoiceForm(owner, store, user, true).ShowDialog(owner);

    public static void OpenJournal(Form owner, AppStore store)
        => new LegacyJournalForm(store).ShowDialog(owner);
}

internal sealed class LegacyInvoiceForm : Form
{
    private readonly AppStore _store;
    private readonly UserProfile _user;
    private readonly bool _purchase;
    private readonly DateTimePicker _date = new();
    private readonly ComboBox _party = new();
    private readonly ComboBox _payment = new();
    private readonly ComboBox _mainAccount = new();
    private readonly ComboBox _vatAccount = new();
    private readonly ComboBox _settlementAccount = new();
    private readonly ComboBox _item = new();
    private readonly NumericUpDown _quantity = Number(1);
    private readonly NumericUpDown _price = Number(0);
    private readonly NumericUpDown _vat = Number(15);
    private readonly NumericUpDown _discount = Number(0);
    private readonly NumericUpDown _storeId = Number(1);
    private readonly DataGridView _lines = new();
    private readonly Label _total = new();
    private readonly TextBox _note = new();

    public LegacyInvoiceForm(Form owner, AppStore store, UserProfile user, bool purchase)
    {
        _store = store;
        _user = user;
        _purchase = purchase;

        Text = purchase ? "فاتورة مشتريات جديدة - قاعدة GtsDb2026" : "فاتورة مبيعات جديدة - قاعدة GtsDb2026";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(1050, 650);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        BackColor = Theme.Background;

        Build();
        LoadData();
        UpdateTotal();
    }

    private void Build()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 190,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(12)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(header);

        AddField(header, 0, 0, "التاريخ", _date);
        AddField(header, 2, 0, _purchase ? "المورد" : "العميل", _party);
        AddField(header, 0, 1, "نوع السداد", _payment);
        AddField(header, 2, 1, _purchase ? "حساب المخزون" : "حساب المبيعات", _mainAccount);
        AddField(header, 0, 2, "حساب الضريبة", _vatAccount);
        AddField(header, 2, 2, "حساب التسوية", _settlementAccount);
        AddField(header, 0, 3, "المخزن", _storeId);
        AddField(header, 2, 3, "ملاحظات", _note);

        _date.Value = DateTime.Today;
        _payment.DropDownStyle = ComboBoxStyle.DropDownList;
        _note.Dock = DockStyle.Fill;

        var linePanel = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(12, 5, 12, 5) };
        Controls.Add(linePanel);

        AddInline(linePanel, "الصنف", _item, 0, 0, 270);
        AddInline(linePanel, "الكمية", _quantity, 280, 0, 95);
        AddInline(linePanel, "السعر", _price, 385, 0, 120);
        AddInline(linePanel, "الضريبة %", _vat, 515, 0, 100);
        AddInline(linePanel, "الخصم", _discount, 625, 0, 110);

        var add = new Button { Text = "إضافة السطر", Width = 120, Height = 32, Left = 750, Top = 24 };
        Theme.StyleButton(add);
        add.Click += (_, _) => AddLine();
        linePanel.Controls.Add(add);

        var remove = new Button { Text = "حذف السطر", Width = 120, Height = 32, Left = 880, Top = 24 };
        Theme.StyleButton(remove);
        remove.Click += (_, _) => RemoveLine();
        linePanel.Controls.Add(remove);

        var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 65) };
        Controls.Add(gridPanel);

        _lines.Dock = DockStyle.Fill;
        _lines.AllowUserToAddRows = false;
        _lines.AllowUserToDeleteRows = false;
        _lines.ReadOnly = true;
        _lines.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _lines.MultiSelect = false;
        _lines.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _lines.Columns.Add("itemId", "ID");
        _lines.Columns.Add("item", "الصنف");
        _lines.Columns.Add("qty", "الكمية");
        _lines.Columns.Add("price", "السعر");
        _lines.Columns.Add("vat", "الضريبة");
        _lines.Columns.Add("discount", "الخصم");
        _lines.Columns.Add("store", "المخزن");
        Theme.StyleGrid(_lines);
        gridPanel.Controls.Add(_lines);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(12) };
        Controls.Add(bottom);

        _total.AutoSize = true;
        _total.Font = new Font("Tahoma", 13F, FontStyle.Bold);
        _total.Location = new Point(12, 16);
        bottom.Controls.Add(_total);

        var save = new Button
        {
            Text = "حفظ وترحيل العملية",
            Width = 190,
            Height = 38,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        Theme.StyleButton(save, true);
        save.Click += (_, _) => Save();
        bottom.Controls.Add(save);
        bottom.Resize += (_, _) => save.Left = bottom.ClientSize.Width - save.Width - 12;

        var cancel = new Button { Text = "إلغاء", Width = 100, Height = 38, Left = 202 };
        Theme.StyleButton(cancel);
        cancel.Click += (_, _) => Close();
        bottom.Controls.Add(cancel);

        _item.SelectedIndexChanged += (_, _) =>
        {
            if (_item.SelectedItem is ItemRow item)
            {
                _price.Value = Clamp(item.SalePrice);
                _vat.Value = Clamp(15m);
            }
        };
    }

    private void LoadData()
    {
        _payment.Items.Add(new PaymentChoice(1, "نقدي"));
        _payment.Items.Add(new PaymentChoice(2, "آجل"));
        _payment.Items.Add(new PaymentChoice(3, "شبكة / بنك"));
        _payment.SelectedIndex = 0;

        if (_purchase)
        {
            _party.DataSource = _store.Suppliers.ToArray();
            _party.DisplayMember = nameof(SupplierRow.Name);
            _party.ValueMember = nameof(SupplierRow.Id);
        }
        else
        {
            _party.DataSource = _store.Customers.ToArray();
            _party.DisplayMember = nameof(CustomerRow.Name);
            _party.ValueMember = nameof(CustomerRow.Id);
        }
        _party.DropDownStyle = ComboBoxStyle.DropDownList;

        _item.DataSource = _store.Items.ToArray();
        _item.DisplayMember = nameof(ItemRow.Name);
        _item.ValueMember = nameof(ItemRow.Id);
        _item.DropDownStyle = ComboBoxStyle.DropDownList;

        _mainAccount.DataSource = _store.Accounts.ToArray();
        _mainAccount.DisplayMember = nameof(AccountRow.Name);
        _mainAccount.ValueMember = nameof(AccountRow.Id);
        _vatAccount.DataSource = _store.Accounts.ToArray();
        _vatAccount.DisplayMember = nameof(AccountRowDisplay.Name);
        _vatAccount.ValueMember = nameof(AccountRowDisplay.Id);
        _settlementAccount.DataSource = _store.Accounts.ToArray();
        _settlementAccount.DisplayMember = nameof(AccountRowDisplay.Name);
        _settlementAccount.ValueMember = nameof(AccountRowDisplay.Id);

        SelectAccount(_mainAccount, _purchase ? new[]{"مخزون","مشتريات"} : new[]{"مبيعات","إيراد"});
        SelectAccount(_vatAccount, new[]{"ضريبة","vat"});
        SelectAccount(_settlementAccount, new[]{"صندوق","بنك","نقدية"});
        _storeId.Value = 1;
    }

    private void AddLine()
    {
        if (_item.SelectedItem is not ItemRow item) return;
        var quantity = _quantity.Value;
        if (quantity <= 0)
        {
            MessageBox.Show("الكمية يجب أن تكون أكبر من صفر.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _lines.Rows.Add(
            item.Id,
            item.Name,
            quantity.ToString("0.###"),
            _price.Value.ToString("0.00"),
            _vat.Value.ToString("0.##"),
            _discount.Value.ToString("0.00"),
            _storeId.Value.ToString("0"));

        UpdateTotal();
    }

    private void RemoveLine()
    {
        if (_lines.SelectedRows.Count > 0)
            _lines.Rows.RemoveAt(_lines.SelectedRows[0].Index);
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        decimal total = 0;
        foreach (DataGridViewRow row in _lines.Rows)
        {
            var qty = ToDecimal(row.Cells["qty"].Value);
            var price = ToDecimal(row.Cells["price"].Value);
            var vat = ToDecimal(row.Cells["vat"].Value);
            var discount = ToDecimal(row.Cells["discount"].Value);
            var subtotal = Math.Max(0, qty * price - discount);
            total += subtotal + subtotal * vat / 100m;
        }
        _total.Text = $"الإجمالي: {total:N2}";
    }

    private void Save()
    {
        if (_lines.Rows.Count == 0)
        {
            MessageBox.Show("أضف صنفًا واحدًا على الأقل.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var lines = _lines.Rows.Cast<DataGridViewRow>().Select(row =>
                new LegacyInvoiceLineWriteClientRequest(
                    Convert.ToInt32(row.Cells["itemId"].Value),
                    Convert.ToInt32(row.Cells["store"].Value),
                    FindItem(Convert.ToInt32(row.Cells["itemId"].Value))?.Id == 0 ? 0 : (FindItem(Convert.ToInt32(row.Cells["itemId"].Value))?.Id ?? 0),
                    "الصغرى",
                    ToDecimal(row.Cells["qty"].Value),
                    ToDecimal(row.Cells["price"].Value),
                    ToDecimal(row.Cells["vat"].Value),
                    ToDecimal(row.Cells["discount"].Value)))
                .ToList();

            if (lines.Any(x => x.Quantity <= 0)) throw new InvalidOperationException("توجد كمية غير صحيحة.");

            var partyId = _party.SelectedItem is SupplierRow s ? s.Id :
                          _party.SelectedItem is CustomerRow c ? c.Id : (int?)null;
            var branch = _user.LegacyScreens.Count > 0 ? FindBranchFromScreens() : 1;
            var paymentType = _payment.SelectedItem is PaymentChoice p ? p.Id : 1;
            var mainAccount = SelectedId(_mainAccount);
            var vatAccount = SelectedId(_vatAccount);
            var settlement = paymentType == 2 ? 0 : SelectedId(_settlementAccount);

            LegacyWriteResultRow result;
            if (_purchase)
            {
                result = _store.CreateLegacyPurchase(new LegacyPurchaseWriteClientRequest(
                    _date.Value.Date, paymentType, branch, partyId,
                    (_party.SelectedItem as SupplierRow)?.Name, null, null, DateTime.Now.Year,
                    settlement, mainAccount, vatAccount, lines, _note.Text.Trim()));
            }
            else
            {
                result = _store.CreateLegacySale(new LegacySaleWriteClientRequest(
                    _date.Value.Date, paymentType, branch, partyId,
                    (_party.SelectedItem as CustomerRow)?.Name, null, null, DateTime.Now.Year,
                    settlement, mainAccount, vatAccount, lines, _note.Text.Trim()));
            }

            MessageBox.Show(
                $"تم حفظ العملية فعليًا في GtsDb2026.\r\nرقم المستند: {result.Number}\r\nرقم القيد: {result.Journal?.Id}",
                "تم الحفظ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "تعذر حفظ العملية", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private ItemRow? FindItem(int id) => _store.Items.FirstOrDefault(x => x.Id == id);

    private int FindBranchFromScreens() => 1;

    private int SelectedId(ComboBox combo)
        => combo.SelectedValue is int id ? id : combo.SelectedItem is AccountRow a ? a.Id : 0;

    private void SelectAccount(ComboBox combo, string[] terms)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            var item = combo.Items[i];
            var name = item is AccountRow a ? $"{a.Code} {a.Name}" : item.ToString() ?? "";
            if (terms.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static decimal Clamp(decimal value) => Math.Max(0m, Math.Min(100000000m, value));

    private static decimal ToDecimal(object? value)
        => decimal.TryParse(Convert.ToString(value), out var result) ? result : 0m;

    private static NumericUpDown Number(decimal value)
        => new() { Minimum = 0, Maximum = 100000000, DecimalPlaces = 3, Increment = 1, Value = value, Width = 100 };

    private static void AddField(TableLayoutPanel panel, int labelCol, int row, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Right, Font = new Font("Tahoma", 9F, FontStyle.Bold) }, labelCol, row);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, labelCol + 1, row);
    }

    private static void AddInline(Control parent, string label, Control control, int x, int y, int width)
    {
        parent.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, y) });
        control.Width = width;
        control.Location = new Point(x, y + 24);
        parent.Controls.Add(control);
    }

    private sealed record PaymentChoice(int Id, string Name);
}

internal sealed class LegacyJournalForm : Form
{
    private readonly AppStore _store;
    private readonly DateTimePicker _date = new() { Value = DateTime.Today, Width = 150 };
    private readonly TextBox _note = new();
    private readonly DataGridView _grid = new();
    private readonly Label _balance = new();

    public LegacyJournalForm(AppStore store)
    {
        _store = store;
        Text = "قيد يومية جديد - قاعدة GtsDb2026";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1000, 650);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(12) };
        top.Controls.Add(new Label { Text = "التاريخ", AutoSize = true, Margin = new Padding(0,8,4,0) });
        top.Controls.Add(_date);
        top.Controls.Add(new Label { Text = "البيان", AutoSize = true, Margin = new Padding(20,8,4,0) });
        _note.Width = 340;
        top.Controls.Add(_note);

        var add = new Button { Text = "إضافة سطر", Width = 105, Height = 32 };
        Theme.StyleButton(add);
        add.Click += (_, _) => AddRow();
        top.Controls.Add(add);

        var remove = new Button { Text = "حذف", Width = 85, Height = 32 };
        Theme.StyleButton(remove);
        remove.Click += (_, _) => RemoveRow();
        top.Controls.Add(remove);
        Controls.Add(top);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "accountId", HeaderText = "حساب" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "accountName", HeaderText = "اسم الحساب" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "debit", HeaderText = "مدين" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "credit", HeaderText = "دائن" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "description", HeaderText = "البيان" });
        Theme.StyleGrid(_grid);
        Controls.Add(_grid);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(12) };
        _balance.AutoSize = true;
        _balance.Location = new Point(12, 16);
        _balance.Font = new Font("Tahoma", 11F, FontStyle.Bold);
        bottom.Controls.Add(_balance);

        var save = new Button { Text = "حفظ القيد", Width = 125, Height = 38, Anchor = AnchorStyles.Right | AnchorStyles.Top };
        Theme.StyleButton(save, true);
        save.Click += (_, _) => Save();
        bottom.Controls.Add(save);
        bottom.Resize += (_, _) => save.Left = bottom.ClientSize.Width - save.Width - 12;
        Controls.Add(bottom);

        AddRow();
        _grid.CellEndEdit += (_, _) => UpdateBalance();
        UpdateBalance();
    }

    private void AddRow()
    {
        var row = _grid.Rows.Add();
        var combo = new DataGridViewComboBoxCell();
        combo.DataSource = _store.Accounts.Select(a => new AccountChoice(a.Id, $"{a.Code} - {a.Name}")).ToList();
        combo.DisplayMember = "Name";
        combo.ValueMember = "Id";
        _grid.Rows[row].Cells["accountId"] = combo;
        _grid.Rows[row].Cells["debit"].Value = "0";
        _grid.Rows[row].Cells["credit"].Value = "0";
    }

    private void RemoveRow()
    {
        if (_grid.SelectedRows.Count > 0) _grid.Rows.RemoveAt(_grid.SelectedRows[0].Index);
        UpdateBalance();
    }

    private void UpdateBalance()
    {
        decimal debit=0, credit=0;
        foreach (DataGridViewRow r in _grid.Rows)
        {
            debit += ToDecimal(r.Cells["debit"].Value);
            credit += ToDecimal(r.Cells["credit"].Value);
        }
        _balance.Text = $"المدين: {debit:N2}   الدائن: {credit:N2}   الفرق: {(debit-credit):N2}";
    }

    private void Save()
    {
        try
        {
            var lines = new List<LegacyJournalLineWriteClientRequest>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (r.IsNewRow) continue;
                var accountId = r.Cells["accountId"].Value is AccountChoice choice ? choice.Id : Convert.ToInt32(r.Cells["accountId"].Value);
                var debit = ToDecimal(r.Cells["debit"].Value);
                var credit = ToDecimal(r.Cells["credit"].Value);
                if (accountId <= 0 || (debit == 0 && credit == 0)) continue;
                lines.Add(new LegacyJournalLineWriteClientRequest(accountId, debit, credit,
                    Convert.ToString(r.Cells["description"].Value), null, null));
            }

            var result = _store.CreateLegacyJournal(new LegacyJournalWriteClientRequest(
                _date.Value.Date, null, _note.Text.Trim(), null, null, null, DateTime.Now.Year, lines));

            MessageBox.Show($"تم حفظ القيد فعليًا.\r\nرقم القيد: {result.Number}", "تم الحفظ",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "تعذر حفظ القيد", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static decimal ToDecimal(object? value)
        => decimal.TryParse(Convert.ToString(value), out var result) ? result : 0m;

    private sealed record AccountChoice(int Id, string Name);
}
