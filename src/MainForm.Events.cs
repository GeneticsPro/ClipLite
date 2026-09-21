using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipLite
{
    internal partial class MainForm
    {
        TreeNode treeMenuNode;

        /// <summary>The nodes a clip can be dropped on.</summary>
        TreeNode[] CollectionNodes { get { return new[] { nodeInbox, nodeSafe, nodeTrash }; } }

        bool IsCollectionNode(TreeNode node) { return node != null && Array.IndexOf(CollectionNodes, node) >= 0; }

        /// <summary>A menu item whose caption follows the current language.</summary>
        static ToolStripMenuItem Mi(string key, string shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem("", null, onClick) { ShortcutKeyDisplayString = shortcut };
            Loc.Bind(() => item.Text = Loc.T(key));
            return item;
        }

        void BuildMenus()
        {
            // clip list
            var miPaste = Mi("Paste", "Enter", (s, e) => UseSelected(true));
            var miCopy = Mi("Copy to clipboard", "Ctrl+C", (s, e) => UseSelected(false));
            var miRename = Mi("Rename…", "F2", (s, e) => RenameSelected());
            var miToInbox = Mi("Move to InBox", null, (s, e) => MoveSelected(ClipStore.InBox));
            var miToSafe = Mi("Move to Safe", null, (s, e) => MoveSelected(ClipStore.Safe));
            var miToTrash = Mi("Move to Trash", "Del", (s, e) => MoveSelected(ClipStore.Trash));
            var miDelete = Mi("Delete permanently", "Shift+Del", (s, e) => DeleteSelected(true));
            var miCase = new ToolStripMenuItem();
            Loc.Bind(() => miCase.Text = Loc.T("Change case"));
            foreach (CaseMode modeRaw in Enum.GetValues(typeof(CaseMode)))
            {
                var mode = modeRaw;
                miCase.DropDownItems.Add(Mi(TextCase.Label(mode), TextCase.Shortcut(mode), (s, e) => ApplyCase(mode)));
            }
            Theme.Apply(miCase.DropDown);
            listMenu.Items.AddRange(new ToolStripItem[] { miPaste, miCopy, new ToolStripSeparator(), miRename, miCase, miToInbox, miToSafe, miToTrash, new ToolStripSeparator(), miDelete });
            listMenu.Opening += (s, e) =>
            {
                var sel = list.SelectedItems.Cast<Clip>().ToList();
                if (sel.Count == 0) { e.Cancel = true; return; }
                miPaste.Enabled = miCopy.Enabled = miRename.Enabled = sel.Count >= 1;
                miCase.Enabled = sel.Count == 1 && txtEditor.Visible && !txtEditor.ReadOnly;
                miToInbox.Visible = sel.Any(c => c.Collection != ClipStore.InBox);
                miToSafe.Visible = sel.Any(c => c.Collection != ClipStore.Safe);
                miToTrash.Visible = sel.Any(c => c.Collection != ClipStore.Trash);
            };

            // tree
            var miEmptyTrash = Mi("Empty Trash", null, (s, e) => EmptyTrash());
            treeMenu.Items.AddRange(new ToolStripItem[] { miEmptyTrash, new ToolStripSeparator(), Mi("Options…", "Ctrl+,", (s, e) => ShowOptionsDialog()) });
            treeMenu.Opening += (s, e) => miEmptyTrash.Visible = treeMenuNode == nodeTrash;

            // tray
            miShow = Mi("Show ClipLite", HotkeyText, (s, e) => ShowMain(false));
            miShow.Font = new Font(miShow.Font, FontStyle.Bold);
            miPause = Mi("Pause capture", null, (s, e) =>
            {
                paused = !paused;
                miPause.Checked = paused;
                tray.Text = paused ? Loc.T("ClipLite (paused)") : "ClipLite";
            });
            miAutostart = Mi("Run at Windows startup", null, (s, e) =>
            {
                try { Autostart.Enabled = !Autostart.Enabled; } catch { }
            });
            var miFolder = Mi("Open data folder", null, (s, e) =>
            {
                try { Process.Start("explorer.exe", "\"" + dataDir + "\""); } catch { }
            });
            var miOptions = Mi("Options…", null, (s, e) => ShowOptionsDialog());
            var miSettings = Mi("Edit settings.ini (restart to apply)", null, (s, e) =>
            {
                SaveLayout();
                try { Process.Start("notepad.exe", "\"" + System.IO.Path.Combine(dataDir, "settings.ini") + "\""); } catch { }
            });
            var miExit = Mi("Exit", null, (s, e) => ExitApp());
            trayMenu.Items.AddRange(new ToolStripItem[] { miShow, new ToolStripSeparator(), miPause, miAutostart, miOptions, new ToolStripSeparator(), miFolder, miSettings, new ToolStripSeparator(), miExit });
            trayMenu.Opening += (s, e) =>
            {
                try { miAutostart.Checked = Autostart.Enabled; } catch { }
                miShow.ShortcutKeyDisplayString = HotkeyText;
            };
        }

        void WireEvents()
        {
            captureTimer = new Timer { Interval = 120 };
            captureTimer.Tick += (s, e) => CaptureNow();
            searchTimer = new Timer { Interval = 200 };
            searchTimer.Tick += (s, e) => RunSearch();
            // short debounce: holding an arrow key walks the list without rewriting the clipboard each step
            selectTimer = new Timer { Interval = 150 };
            selectTimer.Tick += (s, e) => { selectTimer.Stop(); LoadSelectionToClipboard(); };
            trimTimer = new Timer { Interval = 1500 };
            trimTimer.Tick += (s, e) =>
            {
                trimTimer.Stop();
                if (Visible) return;
                SetImage(null);
                Native.TrimMemory();
            };

            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleMain(false); };

            // ----- tree -----
            tree.BeforeCollapse += (s, e) => { if (e.Node == nodeRoot) e.Cancel = true; };
            tree.AfterSelect += (s, e) =>
            {
                if (e.Node == nodeRoot) { tree.SelectedNode = nodeInbox; return; }
                SaveEditorIfDirty();
                currentView = (string)e.Node.Tag;
                suppressSelection = true;
                list.ClearSelected();
                suppressSelection = false;
                RefreshList();
                if (list.Items.Count > 0) list.SelectedIndex = 0;
                if (currentView == SearchView && txtSearch.Text.Length == 0) txtSearch.Focus();
            };
            tree.NodeMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                treeMenuNode = e.Node;
                treeMenu.Show(tree, e.Location);
            };

            // drag clips from the list onto InBox / Safe / Trash
            tree.DragOver += (s, e) =>
            {
                var node = tree.GetNodeAt(tree.PointToClient(new Point(e.X, e.Y)));
                bool ok = draggingClips != null && IsCollectionNode(node);
                e.Effect = ok ? DragDropEffects.Move : DragDropEffects.None;
                foreach (var n in CollectionNodes) n.BackColor = ok && n == node ? Theme.Hover : Theme.Panel;
            };
            tree.DragLeave += (s, e) => { foreach (var n in CollectionNodes) n.BackColor = Theme.Panel; };
            tree.DragDrop += (s, e) =>
            {
                foreach (var n in CollectionNodes) n.BackColor = Theme.Panel;
                var node = tree.GetNodeAt(tree.PointToClient(new Point(e.X, e.Y)));
                if (draggingClips == null || !IsCollectionNode(node)) return;
                SaveEditorIfDirty();
                foreach (var c in draggingClips) store.Move(c, (string)node.Tag);
                draggingClips = null;
                RefreshList();
            };

            // ----- list -----
            listHeader.SortChanged += (s, e) =>
            {
                RefreshList();
                if (list.SelectedIndices.Count > 0) list.TopIndex = list.SelectedIndices[0];
            };
            list.DrawItem += DrawClipItem;
            list.SelectedIndexChanged += (s, e) => ShowCurrentClip();
            list.DoubleClick += (s, e) => UseSelected(true);
            list.GotFocus += (s, e) => list.Invalidate();
            list.LostFocus += (s, e) => list.Invalidate();
            list.Resize += (s, e) => list.Invalidate();
            list.MouseDown += (s, e) =>
            {
                int i = list.IndexFromPoint(e.Location);
                if (e.Button == MouseButtons.Right && i >= 0 && !list.GetSelected(i))
                {
                    list.ClearSelected();
                    list.SelectedIndex = i;
                }
                dragArmed = e.Button == MouseButtons.Left && i >= 0;
                dragStart = e.Location;
            };
            list.MouseUp += (s, e) =>
            {
                bool click = dragArmed; // still armed = released without starting a drag
                dragArmed = false;
                if (click && e.Button == MouseButtons.Left && list.IndexFromPoint(e.Location) >= 0) ScheduleClipboardLoad();
            };
            list.KeyUp += (s, e) =>
            {
                switch (e.KeyCode)
                {
                    case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown: case Keys.Home: case Keys.End:
                        ScheduleClipboardLoad();
                        break;
                }
            };
            list.MouseMove += (s, e) =>
            {
                if (!dragArmed || e.Button != MouseButtons.Left) return;
                var ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - dragStart.X) < ds.Width && Math.Abs(e.Y - dragStart.Y) < ds.Height) return;
                dragArmed = false;
                draggingClips = list.SelectedItems.Cast<Clip>().ToArray();
                if (draggingClips.Length == 0) { draggingClips = null; return; }
                list.DoDragDrop("ClipLite", DragDropEffects.Move);
                draggingClips = null;
            };
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; UseSelected(true); }
                else if (e.KeyCode == Keys.C && e.Control) { e.SuppressKeyPress = true; UseSelected(false); }
                else if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; DeleteSelected(e.Shift); }
                else if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; RenameSelected(); }
                else if (e.KeyCode == Keys.A && e.Control)
                {
                    e.SuppressKeyPress = true;
                    suppressSelection = true;
                    for (int i = 0; i < list.Items.Count; i++) list.SetSelected(i, true);
                    suppressSelection = false;
                    ShowCurrentClip();
                }
            };

            // ----- search -----
            txtSearch.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    RunSearch();
                    list.Focus();
                    if (list.SelectedIndices.Count == 0 && list.Items.Count > 0) list.SelectedIndex = 0;
                }
                else if (e.KeyCode == Keys.Escape && txtSearch.Text.Length > 0)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    txtSearch.Text = "";
                    RunSearch();
                }
            };

            // ----- editor -----
            txtEditor.TextChanged += (s, e) =>
            {
                if (loadingEditor || shownClip == null || txtEditor.ReadOnly) return;
                editorDirty = true;
                btnSave.Enabled = true;
            };
            // the editor works in plain text both ways: no Consolas-formatted RTF leaks out, nothing rich comes in
            txtEditor.KeyDown += (s, e) =>
            {
                if ((e.Control && !e.Alt && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) { e.SuppressKeyPress = true; EditorPaste(); }
                else if ((e.Control && !e.Alt && e.KeyCode == Keys.C) || (e.Control && e.KeyCode == Keys.Insert)) { e.SuppressKeyPress = true; EditorCopy(txtEditor); }
                else if ((e.Control && !e.Alt && e.KeyCode == Keys.X) || (e.Shift && e.KeyCode == Keys.Delete)) { e.SuppressKeyPress = true; EditorCut(); }
            };
            txtRaw.KeyDown += (s, e) =>
            {
                if ((e.Control && e.KeyCode == Keys.C) || (e.Control && e.KeyCode == Keys.Insert)) { e.SuppressKeyPress = true; EditorCopy(txtRaw); }
            };
            BuildEditorMenus();
            btnOptions.Click += (s, e) => ShowOptionsDialog();
            btnPaste.Click += (s, e) => UseSelected(true);
            btnCopy.Click += (s, e) => UseSelected(false);
            btnSave.Click += (s, e) => SaveEditorIfDirty();
            btnDelete.Click += (s, e) => DeleteSelected(false);
            btnWrap.CheckedChanged += (s, e) => { txtEditor.WordWrap = txtRaw.WordWrap = btnWrap.Checked; };
            tabText.Click += (s, e) => SelectTab(tabText);
            tabHtml.Click += (s, e) => SelectTab(tabHtml);
            tabBinary.Click += (s, e) => SelectTab(tabBinary);

            // ----- window-wide keys -----
            KeyDown += (s, e) =>
            {
                if (e.Control && e.Alt && CaseShortcut(e.KeyCode)) { e.SuppressKeyPress = true; return; }
                if (e.KeyCode == Keys.Escape)
                {
                    if (txtSearch.Focused && txtSearch.Text.Length > 0) return; // search box clears itself first
                    e.SuppressKeyPress = true;
                    HideMain();
                }
                else if (e.Control && !e.Alt && e.KeyCode == Keys.F) { e.SuppressKeyPress = true; txtSearch.Focus(); txtSearch.SelectAll(); }
                else if (e.Control && !e.Alt && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; SaveEditorIfDirty(); }
                else if (e.Control && !e.Alt && e.KeyCode == Keys.Oemcomma) { e.SuppressKeyPress = true; ShowOptionsDialog(); }
            };
            Deactivate += (s, e) => SaveEditorIfDirty();
        }

        void ScheduleClipboardLoad()
        {
            selectTimer.Stop();
            selectTimer.Start();
        }

        // ================= editor copy / paste =================

        void BuildEditorMenus()
        {
            var menu = new ContextMenuStrip();
            Theme.Apply(menu);
            var miUndo = Mi("Undo", "Ctrl+Z", (s, e) => txtEditor.Undo());
            var miCut = Mi("Cut", "Ctrl+X", (s, e) => EditorCut());
            var miCopy = Mi("Copy", "Ctrl+C", (s, e) => EditorCopy(txtEditor));
            var miPaste = Mi("Paste", "Ctrl+V", (s, e) => EditorPaste());
            var miDelete = Mi("Delete", "Del", (s, e) => { if (!txtEditor.ReadOnly) txtEditor.SelectedText = ""; });
            var miSelectAll = Mi("Select All", "Ctrl+A", (s, e) => txtEditor.SelectAll());
            var miCase = new ToolStripMenuItem();
            Loc.Bind(() => miCase.Text = Loc.T("Change case"));
            foreach (CaseMode modeRaw in Enum.GetValues(typeof(CaseMode)))
            {
                var mode = modeRaw;
                miCase.DropDownItems.Add(Mi(TextCase.Label(mode), TextCase.Shortcut(mode), (s, e) => ApplyCase(mode)));
            }
            Theme.Apply(miCase.DropDown);
            menu.Items.AddRange(new ToolStripItem[] { miUndo, new ToolStripSeparator(), miCut, miCopy, miPaste, miDelete, new ToolStripSeparator(), miSelectAll, new ToolStripSeparator(), miCase });
            menu.Opening += (s, e) =>
            {
                bool editable = !txtEditor.ReadOnly, selection = txtEditor.SelectionLength > 0;
                miUndo.Enabled = editable && txtEditor.CanUndo;
                miCut.Enabled = miDelete.Enabled = editable && selection;
                miCopy.Enabled = selection;
                miPaste.Enabled = editable && ClipboardHasText();
                miSelectAll.Enabled = txtEditor.TextLength > 0;
                miCase.Enabled = editable && txtEditor.TextLength > 0;
            };
            txtEditor.ContextMenuStrip = menu;

            var rawMenu = new ContextMenuStrip();
            Theme.Apply(rawMenu);
            var miRawCopy = Mi("Copy", "Ctrl+C", (s, e) => EditorCopy(txtRaw));
            var miRawAll = Mi("Select All", "Ctrl+A", (s, e) => txtRaw.SelectAll());
            rawMenu.Items.AddRange(new ToolStripItem[] { miRawCopy, miRawAll });
            rawMenu.Opening += (s, e) => miRawCopy.Enabled = txtRaw.SelectionLength > 0;
            txtRaw.ContextMenuStrip = rawMenu;
        }

        void EditorCopy(RichTextBox box)
        {
            var text = CopySelection(box);
            if (text != null) AddToHistory(text);
        }

        void EditorCut()
        {
            if (txtEditor.ReadOnly || txtEditor.SelectionLength == 0) return;
            var text = CopySelection(txtEditor);
            if (text == null) return; // the clipboard refused the text: deleting it now would lose it
            txtEditor.SelectedText = "";
            AddToHistory(text);
        }

        /// <summary>Puts the selection on the clipboard as plain text. Returns null if nothing was copied.</summary>
        string CopySelection(RichTextBox box)
        {
            if (box.SelectionLength == 0) return null;
            var text = box.SelectedText.Replace("\r\n", "\n").Replace("\n", "\r\n");
            ignoreUntil = Environment.TickCount + 400;
            try { Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), true, 10, 50); }
            catch (Exception ex)
            {
                Sounds.Play(settings, SoundEvent.Ignore);
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(dataDir, "crash.log"), DateTime.Now + "  editor copy failed: " + ex + "\r\n\r\n"); } catch { }
                return null;
            }
            ownSeq = Native.GetClipboardSequenceNumber();
            return text;
        }

        /// <summary>
        /// A copy made in the editor joins the history like a copy from any other application.
        /// The list is refreshed in place: selection and editor stay put, so cut-and-paste inside a clip keeps working.
        /// </summary>
        void AddToHistory(string text)
        {
            bool duplicate;
            Clip clip;
            var data = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, byte[]>>
            {
                new System.Collections.Generic.KeyValuePair<string, byte[]>(Fmt.Text, System.Text.Encoding.UTF8.GetBytes(text))
            };
            try { clip = store.Add(data, "CLIPLITE", ClipStore.MakeTitle(text), out duplicate); }
            catch { return; }
            clipboardClip = clip;
            clipboardSeq = ownSeq;
            Sounds.Play(settings, duplicate ? SoundEvent.Ignore : SoundEvent.Capture);
            if (currentView == SearchView) return;
            if (Visible) RefreshList(false); else listDirty = true;
        }

        void EditorPaste()
        {
            if (txtEditor.ReadOnly || !ClipboardHasText()) return;
            try { txtEditor.SelectedText = Clipboard.GetText(); } catch { }
        }

        static bool ClipboardHasText()
        {
            try { return Clipboard.ContainsText(); } catch { return false; }
        }

        /// <summary>ClipMate's Ctrl+Alt+L/U/M/S/I case shortcuts. Returns false for any other key.</summary>
        bool CaseShortcut(Keys key)
        {
            switch (key)
            {
                case Keys.L: ApplyCase(CaseMode.Lower); return true;
                case Keys.U: ApplyCase(CaseMode.Upper); return true;
                case Keys.M: ApplyCase(CaseMode.Mixed); return true;
                case Keys.S: ApplyCase(CaseMode.Sentence); return true;
                case Keys.I: ApplyCase(CaseMode.Invert); return true;
                default: return false;
            }
        }
    }
}
