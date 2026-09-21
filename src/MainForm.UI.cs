using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipLite
{
    internal partial class MainForm : Form
    {
        float scale = 1f;
        int D(int px) { return (int)Math.Round(px * scale); }

        SplitContainer splitMain, splitTop;
        TextBox txtSearch;
        TreeView tree;
        TreeNode nodeRoot, nodeInbox, nodeSafe, nodeTrash, nodeSearch;
        ListBox list;
        ToolStrip editorBar, tabBar;
        ToolStripButton btnPaste, btnCopy, btnSave, btnWrap, btnDelete, btnOptions, tabText, tabHtml, tabBinary;
        ToolStripDropDownButton btnCase;
        ListHeader listHeader;
        RichTextBox txtEditor, txtRaw;
        PictureBox picImage;
        Label lblSource, lblCaptured, lblStats;
        ToolStripDropDownButton btnPasteMode;
        int pasteMode;
        public static readonly string[] PasteModeKeys = { "Paste Ctrl+V", "Paste Shift+Ins", "Copy only" };

        void SetPasteMode(int mode)
        {
            pasteMode = Math.Max(0, Math.Min(PasteModeKeys.Length - 1, mode));
            btnPasteMode.Text = Loc.T(PasteModeKeys[pasteMode]);
            for (int i = 0; i < btnPasteMode.DropDownItems.Count; i++)
            {
                var item = (ToolStripMenuItem)btnPasteMode.DropDownItems[i];
                item.Text = Loc.T(PasteModeKeys[i]);
                item.Checked = i == pasteMode;
            }
        }
        ContextMenuStrip listMenu, treeMenu, trayMenu;
        NotifyIcon tray;
        readonly Dictionary<string, Bitmap> listIcons = new Dictionary<string, Bitmap>();

        void BuildUi()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f; // CreateGraphics() would create the handle too early

            Text = "ClipLite";
            Icon = Theme.AppIcon();
            ShowInTaskbar = false; // a tray app: the notification area icon is the only entry point
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(D(320), D(360));
            Size = new Size(D(460), D(740));
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;

            foreach (var k in new[] { "text", "rich", "image", "files" }) listIcons[k] = Theme.Icon(k, D(16));

            // ---------- status bar ----------
            var status = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, ColumnCount = 2, RowCount = 2, Height = D(50),
                BackColor = Theme.Bg, Padding = new Padding(D(6), D(2), D(6), D(2))
            };
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, D(170)));
            status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            lblSource = MakeLabel("", Theme.Text);
            var pasteStrip = new ToolStrip { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(0), Margin = new Padding(0), CanOverflow = false };
            Theme.Apply(pasteStrip);
            btnPasteMode = new ToolStripDropDownButton { Alignment = ToolStripItemAlignment.Right, DisplayStyle = ToolStripItemDisplayStyle.Text };
            Theme.Apply(btnPasteMode.DropDown);
            for (int i = 0; i < PasteModeKeys.Length; i++)
            {
                int mode = i;
                btnPasteMode.DropDownItems.Add(new ToolStripMenuItem(Loc.T(PasteModeKeys[i]), null, (s, e) => SetPasteMode(mode)));
            }
            Loc.Bind(() => SetPasteMode(pasteMode));
            pasteStrip.Items.Add(btnPasteMode);
            lblCaptured = MakeLabel("", Theme.TextDim);
            lblStats = MakeLabel("", Theme.TextDim);
            lblCaptured.TextAlign = ContentAlignment.MiddleRight;
            status.Controls.Add(lblSource, 0, 0);
            status.Controls.Add(pasteStrip, 1, 0);
            status.Controls.Add(lblStats, 0, 1);
            status.Controls.Add(lblCaptured, 1, 1);

            // ---------- main split: top (tree | list) / bottom (editor) ----------
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Theme.Border,
                SplitterWidth = D(4), FixedPanel = FixedPanel.Panel2
            };
            splitMain.Panel1.BackColor = Theme.Bg;
            splitMain.Panel2.BackColor = Theme.Bg;

            splitTop = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = Theme.Border,
                SplitterWidth = D(4), FixedPanel = FixedPanel.Panel1
            };
            splitTop.Panel1.BackColor = Theme.Panel;
            splitTop.Panel2.BackColor = Theme.Surface;
            splitMain.Panel1.Controls.Add(splitTop);

            // left: search + tree
            var searchBox = new Panel { Dock = DockStyle.Top, Height = D(30), BackColor = Theme.Panel, Padding = new Padding(D(6), D(6), D(6), D(2)) };
            var searchInner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(D(6), D(3), D(4), 0) };
            txtSearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text };
            searchInner.Controls.Add(txtSearch);
            searchBox.Controls.Add(searchInner);

            var icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(D(16), D(16)) };
            foreach (var k in new[] { "root", "inbox", "safe", "trash", "search" }) icons.Images.Add(k, Theme.Icon(k, D(16)));
            tree = new TreeView
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Panel, ForeColor = Theme.Text,
                ImageList = icons, ShowLines = false, HideSelection = false, FullRowSelect = true,
                ItemHeight = D(22), Indent = D(16), AllowDrop = true
            };
            nodeRoot = new TreeNode { ImageKey = "root", SelectedImageKey = "root" };
            nodeInbox = new TreeNode { ImageKey = "inbox", SelectedImageKey = "inbox", Tag = ClipStore.InBox };
            nodeSafe = new TreeNode { ImageKey = "safe", SelectedImageKey = "safe", Tag = ClipStore.Safe };
            nodeTrash = new TreeNode { ImageKey = "trash", SelectedImageKey = "trash", Tag = ClipStore.Trash };
            nodeSearch = new TreeNode { ImageKey = "search", SelectedImageKey = "search", Tag = "Search" };
            Loc.Bind(() =>
            {
                nodeRoot.Text = Loc.T("My Clips");
                nodeInbox.Text = Loc.T("InBox");
                nodeSafe.Text = Loc.T("Safe");
                nodeTrash.Text = Loc.T("Trash Can");
                nodeSearch.Text = SearchNodeText();
            });
            nodeRoot.Nodes.AddRange(new[] { nodeInbox, nodeSafe, nodeTrash, nodeSearch });
            tree.Nodes.Add(nodeRoot);
            nodeRoot.Expand();
            splitTop.Panel1.Controls.Add(tree);
            splitTop.Panel1.Controls.Add(searchBox);
            tree.BringToFront();

            // right: clip list
            list = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = D(22), IntegralHeight = false,
                SelectionMode = SelectionMode.MultiExtended
            };
            listHeader = new ListHeader(scale) { Dock = DockStyle.Top, Height = D(24), Font = Theme.UiFont };
            Loc.Bind(listHeader.Invalidate);
            splitTop.Panel2.Controls.Add(list);
            splitTop.Panel2.Controls.Add(listHeader);
            list.BringToFront();

            // bottom: editor
            editorBar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(D(16), D(16)), Padding = new Padding(D(4), D(2), 0, D(2)) };
            Theme.Apply(editorBar);
            btnPaste = ToolButton("paste", "Paste into previous window (Enter)");
            btnCopy = ToolButton("copy", "Copy to clipboard (Ctrl+C)");
            btnSave = ToolButton("save", "Save edits (Ctrl+S)");
            btnWrap = ToolButton("wrap", "Word wrap");
            btnWrap.CheckOnClick = true;
            btnDelete = ToolButton("delete", "Delete (Del)");

            btnCase = new ToolStripDropDownButton { Image = Theme.Icon("case", D(16)), DisplayStyle = ToolStripItemDisplayStyle.Image, Margin = new Padding(D(1)) };
            Theme.Apply(btnCase.DropDown);
            Loc.Bind(() => btnCase.ToolTipText = Loc.T("Change case"));
            foreach (CaseMode modeRaw in Enum.GetValues(typeof(CaseMode)))
            {
                var mode = modeRaw;
                var item = new ToolStripMenuItem("", null, (s, e) => ApplyCase(mode)) { ShortcutKeyDisplayString = TextCase.Shortcut(mode) };
                Loc.Bind(() => item.Text = Loc.T(TextCase.Label(mode)));
                btnCase.DropDownItems.Add(item);
            }
            btnOptions = ToolButton("options", "Options… (Ctrl+,)");
            btnOptions.Alignment = ToolStripItemAlignment.Right;
            editorBar.Items.AddRange(new ToolStripItem[] { btnPaste, btnCopy, new ToolStripSeparator(), btnSave, btnWrap, btnCase, new ToolStripSeparator(), btnDelete, btnOptions });

            tabBar = new ToolStrip { Dock = DockStyle.Bottom, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(D(4), 0, 0, 0) };
            Theme.Apply(tabBar);
            tabText = TabButton("Text");
            tabHtml = TabButton("HTML");
            tabBinary = TabButton("Binary");
            tabBar.Items.AddRange(new ToolStripItem[] { tabText, tabHtml, tabBinary });

            txtEditor = new RichTextBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
                Font = Theme.MonoFont, DetectUrls = false, AcceptsTab = true, HideSelection = false, WordWrap = false
            };
            txtRaw = new RichTextBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.TextDim,
                Font = Theme.MonoFont, DetectUrls = false, ReadOnly = true, WordWrap = false, Visible = false
            };
            picImage = new PictureBox { Dock = DockStyle.Fill, BackColor = Theme.Surface, SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
            var editorHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(D(6), D(4), 0, 0) };
            editorHost.Controls.AddRange(new Control[] { txtEditor, txtRaw, picImage });

            splitMain.Panel2.Controls.Add(editorHost);
            splitMain.Panel2.Controls.Add(editorBar);
            splitMain.Panel2.Controls.Add(tabBar);
            editorHost.BringToFront();

            Controls.Add(splitMain);
            Controls.Add(status);
            splitMain.BringToFront();

            // ---------- menus ----------
            listMenu = new ContextMenuStrip();
            treeMenu = new ContextMenuStrip();
            trayMenu = new ContextMenuStrip();
            Theme.Apply(listMenu);
            Theme.Apply(treeMenu);
            Theme.Apply(trayMenu);
            list.ContextMenuStrip = listMenu;

            tray = new NotifyIcon { Icon = Theme.TrayIcon(), Text = "ClipLite", ContextMenuStrip = trayMenu, Visible = true };
        }

        Label MakeLabel(string text, Color color)
        {
            return new Label
            {
                Text = text, ForeColor = color, BackColor = Color.Transparent, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false, Margin = new Padding(0)
            };
        }

        ToolStripButton ToolButton(string icon, string tip)
        {
            var b = new ToolStripButton { Image = Theme.Icon(icon, D(16)), DisplayStyle = ToolStripItemDisplayStyle.Image, Margin = new Padding(D(1)) };
            Loc.Bind(() => b.ToolTipText = Loc.T(tip));
            return b;
        }

        ToolStripButton TabButton(string text)
        {
            var b = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Text, Padding = new Padding(D(6), D(2), D(6), D(2)) };
            Loc.Bind(() => b.Text = Loc.T(text));
            return b;
        }

        void ApplyNativeDarkMode()
        {
            Native.DarkTitleBar(Handle);
            foreach (var c in new Control[] { tree, list, txtEditor, txtRaw })
                Native.DarkControl(c.Handle);
            foreach (var c in new Control[] { tree, list })
                Native.SendMessage(c.Handle, 0x0127, (IntPtr)0x10001, IntPtr.Zero); // WM_CHANGEUISTATE: hide focus rectangles
            Shown += (s, e) => list.Focus();
            Loc.Bind(SetSearchCue);
            Native.SendMessage(tree.Handle, 0x1100 + 40, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Theme.Border)); // TVM_SETLINECOLOR
        }

        void SetSearchCue()
        {
            if (!txtSearch.IsHandleCreated) return;
            var p = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(Loc.T("Search clips…  (Ctrl+F)"));
            try { Native.SendMessage(txtSearch.Handle, 0x1501, (IntPtr)1, p); } // EM_SETCUEBANNER copies the string
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(p); }
        }

        void DrawClipItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            var clip = (Clip)list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color bg = selected ? (list.Focused ? Theme.Selection : Theme.SelectionInactive) : Theme.Surface;
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);

            var icon = listIcons[clip.Kind];
            int pad = D(6);
            e.Graphics.DrawImage(icon, e.Bounds.Left + pad, e.Bounds.Top + (e.Bounds.Height - icon.Height) / 2);

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            int dateWidth = listHeader.DateWidth;
            int left = e.Bounds.Left + pad * 2 + icon.Width;

            string title = string.IsNullOrEmpty(clip.Title) ? Loc.T("(empty)") : clip.Title;
            var titleRect = new Rectangle(left, e.Bounds.Top, Math.Max(0, e.Bounds.Right - dateWidth - left), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, title, list.Font, titleRect, Theme.Text, flags);

            var dateRect = new Rectangle(e.Bounds.Right - dateWidth + D(8), e.Bounds.Top, dateWidth - D(8) - pad, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, DateText(clip), list.Font, dateRect,
                selected ? Theme.Text : Theme.TextDim, flags);
        }

        static string DateText(Clip clip)
        {
            var t = clip.Time.ToLocalTime();
            return t.ToString("dd.MM.yy HH:mm", Loc.Culture);
        }

        void SelectTab(ToolStripButton tab)
        {
            foreach (var t in new[] { tabText, tabHtml, tabBinary }) t.Checked = t == tab;
            ShowCurrentClip();
        }
    }
}
