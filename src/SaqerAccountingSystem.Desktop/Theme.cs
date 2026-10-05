using System.Drawing;
using System.Windows.Forms;

namespace SaqerAccountingSystem.Desktop;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(243, 246, 249);
    public static readonly Color Sidebar = Color.FromArgb(18, 34, 43);
    public static readonly Color SidebarActive = Color.FromArgb(28, 55, 65);
    public static readonly Color Primary = Color.FromArgb(11, 107, 97);
    public static readonly Color Gold = Color.FromArgb(213, 168, 75);
    public static readonly Color Text = Color.FromArgb(23, 33, 43);
    public static readonly Color Muted = Color.FromArgb(113, 128, 143);
    public static readonly Color Border = Color.FromArgb(225, 232, 237);

    public static void StyleButton(Button b, bool primary = false)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = primary ? Primary : Color.FromArgb(237, 245, 243);
        b.ForeColor = primary ? Color.White : Primary;
        b.Font = new Font("Tahoma", 10F, FontStyle.Bold);
        b.Height = 42;
        b.Cursor = Cursors.Hand;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.AutoGenerateColumns = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(247, 249, 250),
            ForeColor = Muted,
            Font = new Font("Tahoma", 9F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleRight
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Tahoma", 9F),
            ForeColor = Text,
            SelectionBackColor = Color.FromArgb(224, 241, 237),
            SelectionForeColor = Text,
            Padding = new Padding(5)
        };
    }
}
