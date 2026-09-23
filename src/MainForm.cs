using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ClipLite
{
    internal partial class MainForm : Form
    {
        const int HotkeyId = 1;
        const string SearchView = "Search";

        readonly string dataDir;
        readonly Settings settings;
        readonly ClipStore store;
        readonly uint myPid = (uint)Process.GetCurrentProcess().Id;

        bool allowVisible, exiting, paused, listDirty, suppressSelection, loadingEditor, editorDirty, dragArmed;
        uint ownSeq, clipboardSeq;
        Clip clipboardClip; // the clip the clipboard currently holds, valid while the sequence number is clipboardSeq
        int ignoreUntil;
        IntPtr lastTarget, fgHook;
        Native.WinEventDelegate fgDelegate;
        Timer captureTimer, searchTimer, trimTimer, selectTimer;
        string pendingSource = "";
        uint pendingPid;
        string currentView = ClipStore.InBox;
        List<Clip> searchResults = new List<Clip>();
        Clip shownClip;
        Clip capturedWhileHidden; // newest capture made while the window was hidden: it becomes active on show
        Clip[] draggingClips;
        Point dragStart;
        ToolStripMenuItem miPause, miAutostart, miShow;

        public MainForm(string dataDir, bool startVisible)
        {
            this.dataDir = dataDir;
            Directory.CreateDirectory(dataDir);
            settings = new Settings(Path.Combine(dataDir, "settings.ini"));
            Loc.Init(settings);
            store = new ClipStore(Path.Combine(dataDir, "clips"), settings);
            allowVisible = startVisible;

            BuildUi();
            BuildMenus();
            WireEvents();
            RestoreLayout();

            store.LoadAll();
            tabText.Checked = true;
            tree.SelectedNode = nodeInbox;
        }

        // ================= window lifecycle =================

        protected override void SetVisibleCore(bool value)
        {
            if (!allowVisible)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyNativeDarkMode();
            Native.AddClipboardFormatListener(Handle);
            if (!allowVisible) trimTimer.Start(); // started to tray: release startup memory
            fgDelegate = OnForegroundChanged;
            fgHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, fgDelegate, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (!RegisterHotkey())
                tray.ShowBalloonTip(4000, "ClipLite", Loc.T("Hotkey {0} is taken by another app. Change it in Options.", HotkeyText), ToolTipIcon.Warning);
            else if (settings.Get("Welcomed", "") == "")
            {
                tray.ShowBalloonTip(4000, "ClipLite", Loc.T("Running in the tray. Press {0} to open.", HotkeyText), ToolTipIcon.Info);
                settings.Set("Welcomed", "1");
                settings.Save();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_CLIPBOARDUPDATE) OnClipboardChanged();
            else if (m.Msg == Native.WM_HOTKEY && (int)m.WParam == HotkeyId) ToggleMain(true);
            else if (m.Msg == Native.WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == Native.SC_MINIMIZE)
            {
                // no taskbar button, so a minimized window would sit as a stub above the taskbar:
                // minimize hides to the tray instead, the same as Close
                HideMain();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideMain();
                return;
            }
            base.OnFormClosing(e);
        }

        string HotkeyText { get { return settings.Get("Hotkey", "Ctrl+Alt+V"); } }

        bool RegisterHotkey()
        {
            Native.UnregisterHotKey(Handle, HotkeyId);
            uint mods = Native.MOD_NOREPEAT;
            Keys key = Keys.None;
            foreach (var raw in HotkeyText.Split('+'))
            {
                var t = raw.Trim();
                switch (t.ToLowerInvariant())
                {
                    case "ctrl": case "control": mods |= Native.MOD_CONTROL; break;
                    case "alt": mods |= Native.MOD_ALT; break;
                    case "shift": mods |= Native.MOD_SHIFT; break;
                    case "win": mods |= Native.MOD_WIN; break;
                    default:
                        try { key = (Keys)Enum.Parse(typeof(Keys), t, true); } catch { }
                        break;
                }
            }
            return key != Keys.None && Native.RegisterHotKey(Handle, HotkeyId, mods, (uint)key);
        }

        void OnForegroundChanged(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero || Native.ProcessId(hwnd) == myPid) return;
            var cls = Native.ClassName(hwnd);
            if (cls == "Shell_TrayWnd" || cls == "NotifyIconOverflowWindow" || cls == "TopLevelWindowForOverflowXamlIsland") return;
            lastTarget = hwnd;
        }

        public void ShowFromOtherInstance()
        {
            ShowMain(false);
        }

        void ToggleMain(bool fromHotkey)
        {
            if (Visible && Native.GetForegroundWindow() == Handle) HideMain();
            else ShowMain(fromHotkey);
        }

        void ShowMain(bool selectNewest)
        {
            allowVisible = true;
            trimTimer.Stop();
            if (listDirty) RefreshList();
            // like ClipMate, a clip captured while the window was closed is the active one when it opens
            Clip activate = null;
            if (currentView == ClipStore.InBox)
            {
                var inbox = store.Lists[ClipStore.InBox];
                if (capturedWhileHidden != null) activate = capturedWhileHidden;
                else if (selectNewest && inbox.Count > 0) activate = inbox[0];
            }
            capturedWhileHidden = null;
            if (activate != null) SelectClip(activate);
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            Native.SetForegroundWindow(Handle);
            list.Focus();
            if (list.SelectedIndices.Count == 0 && list.Items.Count > 0) list.SelectedIndex = 0;
            ShowCurrentClip();
        }

        void HideMain()
        {
            SaveEditorIfDirty();
            SaveLayout();
            Hide();
            trimTimer.Stop();
            trimTimer.Start();
        }

        void ExitApp()
        {
            exiting = true;
            try
            {
                SaveEditorIfDirty();
                SaveLayout();
            }
            catch { } // failing to save window layout must never keep the app from exiting
            finally
            {
                tray.Visible = false;
                tray.Dispose(); // removes the icon now instead of leaving a ghost until hovered
                Native.RemoveClipboardFormatListener(Handle);
                Native.UnregisterHotKey(Handle, HotkeyId);
                if (fgHook != IntPtr.Zero) Native.UnhookWinEvent(fgHook);
                Application.Exit();
            }
        }

        // ================= capture =================

        void OnClipboardChanged()
        {
            if (paused || Native.GetClipboardSequenceNumber() == ownSeq || Environment.TickCount < ignoreUntil) return;
            var owner = Native.GetClipboardOwner();
            pendingPid = owner == IntPtr.Zero ? 0 : Native.ProcessId(owner);
            var fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero || Native.ProcessId(fg) == myPid) fg = owner;
            var proc = Native.ProcessName(fg);
            var title = Native.WindowTitle(fg);
            pendingSource = proc.ToUpperInvariant() + (title.Length > 0 ? " - " + title : "");
            captureTimer.Stop();
            captureTimer.Start();
        }

        void CaptureNow()
        {
            captureTimer.Stop();
            if (pendingPid == myPid) return;
            string title;
            CaptureStatus status;
            List<KeyValuePair<string, byte[]>> data;
            try { data = ClipboardIO.Capture(out title, out status); }
            catch { return; }

            if (status == CaptureStatus.Empty) { Sounds.Play(settings, SoundEvent.Erase); return; }
            if (status == CaptureStatus.Ignored) { Sounds.Play(settings, SoundEvent.Ignore); return; }
            if (data == null || data.Count == 0) return;

            bool duplicate;
            Clip clip;
            try { clip = store.Add(data, pendingSource, title, out duplicate); }
            catch { return; }
            clipboardClip = clip; // the clipboard holds this clip's original, richer data
            clipboardSeq = Native.GetClipboardSequenceNumber();
            // copying the same thing twice stores nothing new, so it gets the "rejected" sound
            Sounds.Play(settings, duplicate ? SoundEvent.Ignore : SoundEvent.Capture);

            if (currentView == SearchView) return;
            if (!Visible) { listDirty = true; capturedWhileHidden = clip; return; }
            RefreshList();
            // the clip that just moved to the top of the InBox becomes the active one
            if (currentView == ClipStore.InBox) SelectClip(clip);
        }

        void SelectClip(Clip clip)
        {
            int i = list.Items.IndexOf(clip);
            if (i < 0) return;
            suppressSelection = true;
            list.ClearSelected();
            suppressSelection = false;
            list.SelectedIndex = i;
            list.TopIndex = Math.Max(0, Math.Min(i, list.Items.Count - 1));
        }

        // ================= list / view =================

        List<Clip> RawItems()
        {
            return currentView == SearchView ? searchResults : store.Lists[currentView];
        }

        /// <summary>The view's clips in the order the header asks for. The store's own lists stay
        /// newest-first — trimming to the collection limits depends on that.</summary>
        List<Clip> ViewItems()
        {
            var items = new List<Clip>(RawItems());
            items.Sort(CompareClips);
            return items;
        }

        int CompareClips(Clip a, Clip b)
        {
            int r;
            if (listHeader.Column == SortColumn.Title)
            {
                r = string.Compare(a.Title ?? "", b.Title ?? "", Loc.Culture, CompareOptions.IgnoreCase);
                if (r == 0) r = a.Seq.CompareTo(b.Seq);
            }
            else
            {
                r = a.Time.CompareTo(b.Time);
                if (r == 0) r = a.Seq.CompareTo(b.Seq);
            }
            return listHeader.Ascending ? r : -r;
        }

        void RefreshList() { RefreshList(true); }

        /// <param name="reloadEditor">false keeps the editor exactly as it is (caret, selection, unsaved edits).</param>
        void RefreshList(bool reloadEditor)
        {
            listDirty = false;
            if (currentView == SearchView)
                searchResults.RemoveAll(c => !store.Lists[c.Collection].Contains(c));

            var selected = list.SelectedItems.Cast<Clip>().ToList();
            int top = list.TopIndex;
            suppressSelection = true;
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(ViewItems().Cast<object>().ToArray());
            foreach (var c in selected)
            {
                int i = list.Items.IndexOf(c);
                if (i >= 0) list.SetSelected(i, true);
            }
            if (list.Items.Count > 0) list.TopIndex = Math.Min(top, list.Items.Count - 1);
            list.EndUpdate();
            suppressSelection = false;
            nodeSearch.Text = SearchNodeText();
            if (reloadEditor) ShowCurrentClip();
        }

        string SearchNodeText()
        {
            return searchResults.Count > 0 || (txtSearch != null && txtSearch.Text.Length > 0)
                ? Loc.T("Search Results ({0})", searchResults.Count)
                : Loc.T("Search Results");
        }

        void SelectIndexAfterRemoval(int index)
        {
            if (list.Items.Count == 0) return;
            suppressSelection = true;
            list.ClearSelected();
            suppressSelection = false;
            list.SelectedIndex = Math.Max(0, Math.Min(index, list.Items.Count - 1));
            // removal is a user action (Delete / move): like ClipMate, the clip that takes the
            // selection goes to the clipboard, otherwise Ctrl+V still pastes the removed clip
            ScheduleClipboardLoad();
        }

        void RunSearch()
        {
            searchTimer.Stop();
            var q = txtSearch.Text.Trim();
            if (q.Length == 0)
            {
                searchResults.Clear();
                if (currentView == SearchView) tree.SelectedNode = nodeInbox;
                else nodeSearch.Text = SearchNodeText();
                return;
            }
            var cmp = StringComparison.OrdinalIgnoreCase;
            searchResults = ClipStore.Collections.SelectMany(c => store.Lists[c])
                .Where(c => c.Title.IndexOf(q, cmp) >= 0 || c.Preview.IndexOf(q, cmp) >= 0 || c.Source.IndexOf(q, cmp) >= 0)
                .ToList();
            if (tree.SelectedNode != nodeSearch) tree.SelectedNode = nodeSearch;
            else RefreshList();
        }

        // ================= editor =================

        void ShowCurrentClip()
        {
            if (suppressSelection) return;
            SaveEditorIfDirty();

            Clip clip = list.SelectedIndices.Count > 0 ? (Clip)list.Items[list.SelectedIndices[0]] : null;
            shownClip = clip;
            loadingEditor = true;
            txtEditor.Visible = false;
            txtRaw.Visible = false;
            picImage.Visible = false;
            SetImage(null);
            btnSave.Enabled = false;
            btnPaste.Enabled = btnCopy.Enabled = btnDelete.Enabled = clip != null;

            if (clip == null)
            {
                txtEditor.Text = "";
                txtEditor.ReadOnly = true;
                txtEditor.Visible = true;
                lblSource.Text = "";
                lblCaptured.Text = "";
                lblStats.Text = Loc.T("{0} clips", RawItems().Count);
                loadingEditor = false;
                editorDirty = false;
                return;
            }

            lblSource.Text = clip.Source;
            lblCaptured.Text = "#" + clip.Seq + "  ·  " + clip.Time.ToLocalTime().ToString("dd.MM HH:mm");
            string text = store.ReadString(clip, Fmt.Text);

            if (tabText.Checked)
            {
                if (clip.Has(Fmt.Png))
                {
                    var b = store.Read(clip, Fmt.Png);
                    if (b != null)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(b))
                            using (var img = Image.FromStream(ms))
                                SetImage(new Bitmap(img));
                        }
                        catch { }
                    }
                    picImage.Visible = true;
                }
                else
                {
                    txtEditor.Text = text ?? store.ReadString(clip, Fmt.Files) ?? "";
                    txtEditor.ReadOnly = text == null;
                    txtEditor.SelectionStart = 0;
                    txtEditor.Visible = true;
                }
            }
            else if (tabHtml.Checked)
            {
                txtRaw.Text = store.ReadString(clip, Fmt.Html) ?? store.ReadString(clip, Fmt.Rtf) ?? Loc.T("(no HTML or RTF format in this clip)");
                txtRaw.Visible = true;
            }
            else
            {
                txtRaw.Text = HexDump(clip);
                txtRaw.Visible = true;
            }

            var sb = new StringBuilder();
            sb.Append(Loc.T("{0} Bytes", clip.TotalBytes.ToString("N0")));
            if (text != null)
            {
                int words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
                sb.Append(", ").Append(Loc.T("{0} Chars", text.Length.ToString("N0")))
                  .Append(", ").Append(Loc.T("{0} Words", words.ToString("N0")));
            }
            lblStats.Text = sb.ToString();
            loadingEditor = false;
            editorDirty = false;
        }

        void SetImage(Image img)
        {
            var old = picImage.Image;
            picImage.Image = img;
            if (old != null) old.Dispose();
        }

        string HexDump(Clip clip)
        {
            const int limit = 16 * 1024;
            var sb = new StringBuilder();
            foreach (var f in clip.Formats)
            {
                var bytes = store.Read(clip, f.Name);
                if (bytes == null) continue;
                sb.AppendLine("[" + f.Name + "]  " + f.Length.ToString("N0") + " bytes");
                int n = Math.Min(bytes.Length, limit);
                for (int off = 0; off < n; off += 16)
                {
                    sb.Append(off.ToString("X8")).Append("  ");
                    int len = Math.Min(16, n - off);
                    for (int i = 0; i < 16; i++) sb.Append(i < len ? bytes[off + i].ToString("X2") + " " : "   ");
                    sb.Append(' ');
                    for (int i = 0; i < len; i++)
                    {
                        byte ch = bytes[off + i];
                        sb.Append(ch >= 32 && ch < 127 ? (char)ch : '.');
                    }
                    sb.AppendLine();
                }
                if (bytes.Length > limit) sb.AppendLine("…");
                sb.AppendLine();
                if (sb.Length > 200000) break;
            }
            return sb.ToString();
        }

        void SaveEditorIfDirty()
        {
            if (!editorDirty || shownClip == null) return;
            editorDirty = false;
            btnSave.Enabled = false;
            if (!store.Lists[shownClip.Collection].Contains(shownClip)) return;
            bool onClipboard = shownClip == clipboardClip && Native.GetClipboardSequenceNumber() == clipboardSeq;
            bool selected = list.SelectedIndices.Count == 1 && list.Items[list.SelectedIndices[0]] == shownClip
                && settings.Get("LoadOnSelect", "1") == "1";
            try { store.UpdateText(shownClip, txtEditor.Text); }
            catch { return; }
            list.Invalidate();
            // like ClipMate: the edited clip is the active one, so Ctrl+V in another app pastes the new text
            if (onClipboard || selected) LoadToClipboard(shownClip);
        }

        // ================= actions =================

        void UseSelected(bool paste)
        {
            selectTimer.Stop();
            var clip = list.SelectedIndices.Count > 0 ? (Clip)list.Items[list.SelectedIndices[0]] : null;
            if (clip == null) return;
            SaveEditorIfDirty();
            // never send the paste keystroke after a failed write: it would paste the old clipboard
            if (!LoadToClipboard(clip)) return;

            if (!paste || pasteMode == 2) return;
            HideMain();
            ClipboardIO.PasteInto(lastTarget, pasteMode == 1 ? "ShiftIns" : "CtrlV");
        }

        /// <summary>Puts a stored clip on the Windows clipboard without capturing it back.</summary>
        bool LoadToClipboard(Clip clip)
        {
            var data = store.ReadAll(clip);
            if (data.Count == 0) return false;
            ignoreUntil = Environment.TickCount + 400;
            uint seq = ClipboardIO.Put(data);
            if (seq == 0) return false;
            ownSeq = seq;
            clipboardClip = clip;
            clipboardSeq = seq;
            return true;
        }

        /// <summary>
        /// ClipMate behaviour: the clip the user picks in the list becomes the clipboard content,
        /// so a plain Ctrl+V in any application pastes it.
        /// </summary>
        void LoadSelectionToClipboard()
        {
            if (!Visible || settings.Get("LoadOnSelect", "1") != "1" || list.SelectedIndices.Count != 1) return;
            var clip = (Clip)list.Items[list.SelectedIndices[0]];
            // already there — rewriting would strip formats we don't store (Excel cells, etc.)
            if (clip == clipboardClip && Native.GetClipboardSequenceNumber() == clipboardSeq) return;
            SaveEditorIfDirty();
            LoadToClipboard(clip);
        }

        void DeleteSelected(bool forcePermanent)
        {
            var clips = list.SelectedItems.Cast<Clip>().ToList();
            if (clips.Count == 0) return;
            int index = list.SelectedIndices[0];
            bool permanent = forcePermanent || clips.All(c => c.Collection == ClipStore.Trash);
            if (permanent)
            {
                var msg = clips.Count == 1 ? Loc.T("Delete this clip permanently?") : Loc.T("Delete {0} clips permanently?", clips.Count);
                if (MessageBox.Show(this, msg, "ClipLite", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                foreach (var c in clips) store.Delete(c);
            }
            else
            {
                foreach (var c in clips) store.Move(c, ClipStore.Trash);
            }
            editorDirty = false;
            RefreshList();
            SelectIndexAfterRemoval(index);
        }

        void MoveSelected(string collection)
        {
            var clips = list.SelectedItems.Cast<Clip>().ToList();
            if (clips.Count == 0) return;
            int index = list.SelectedIndices[0];
            SaveEditorIfDirty();
            foreach (var c in clips) store.Move(c, collection);
            RefreshList();
            if (currentView != SearchView) SelectIndexAfterRemoval(index);
        }

        void RenameSelected()
        {
            var clip = list.SelectedIndices.Count > 0 ? (Clip)list.Items[list.SelectedIndices[0]] : null;
            if (clip == null) return;
            var name = Prompt(Loc.T("Rename clip"), clip.Title);
            if (name == null || name.Trim().Length == 0) return;
            SaveEditorIfDirty();
            try { store.Rename(clip, name.Trim()); } catch { }
            list.Invalidate();
        }

        void EmptyTrash()
        {
            var trash = store.Lists[ClipStore.Trash].ToList();
            if (trash.Count == 0) return;
            if (MessageBox.Show(this, Loc.T("Permanently delete {0} clips from Trash?", trash.Count), "ClipLite", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            foreach (var c in trash) store.Delete(c);
            RefreshList();
        }

        string Prompt(string title, string value)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.BackColor = Theme.Bg;
                f.Font = Theme.UiFont;
                f.ClientSize = new Size(D(380), D(44));
                f.Padding = new Padding(D(10));
                f.KeyPreview = true;
                var tb = new TextBox { Dock = DockStyle.Top, Text = value, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
                f.Controls.Add(tb);
                f.HandleCreated += (s, e) => Native.DarkTitleBar(f.Handle);
                f.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.OK; }
                    else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.Cancel; }
                };
                f.Shown += (s, e) => tb.SelectAll();
                return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }

        void ShowOptionsDialog()
        {
            bool applied;
            using (var d = new OptionsDialog(settings))
            {
                if (Visible) { d.StartPosition = FormStartPosition.CenterParent; applied = d.ShowDialog(this) == DialogResult.OK; }
                else applied = d.ShowDialog() == DialogResult.OK;
            }
            if (applied)
            {
                SetPasteMode(settings.GetInt("PasteMode", 0));
                store.ApplyLimits(settings.GetInt("InBoxLimit", 1000), settings.GetInt("TrashLimit", 1000));
                if (!RegisterHotkey())
                    tray.ShowBalloonTip(4000, "ClipLite", Loc.T("Hotkey {0} could not be registered — another application is using it.", HotkeyText), ToolTipIcon.Warning);
            }
            // either way the language may have changed under us (Cancel rolls it back)
            if (Visible) RefreshList(); else listDirty = true;
        }

        // ================= change case =================

        /// <summary>Recases the editor — the selection when there is one, otherwise the whole clip.</summary>
        void ApplyCase(CaseMode mode)
        {
            if (shownClip == null || !txtEditor.Visible || txtEditor.ReadOnly) return;
            int start = txtEditor.SelectionStart, length = txtEditor.SelectionLength;
            if (length > 0)
            {
                var converted = TextCase.Apply(txtEditor.SelectedText, mode);
                if (converted == txtEditor.SelectedText) return;
                txtEditor.SelectedText = converted;
                txtEditor.Select(start, converted.Length);
            }
            else
            {
                var converted = TextCase.Apply(txtEditor.Text, mode);
                if (converted == txtEditor.Text) return;
                txtEditor.Text = converted;
                txtEditor.Select(Math.Min(start, txtEditor.TextLength), 0);
            }
            txtEditor.Focus();
        }

        // ================= layout persistence =================

        void RestoreLayout()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            var r = new Rectangle(wa.Right - Width - D(20), wa.Bottom - Height - D(20), Width, Height);
            var parts = settings.Get("Bounds", "").Split(',');
            int x, y, w, h;
            if (parts.Length == 4 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y) && int.TryParse(parts[2], out w) && int.TryParse(parts[3], out h))
            {
                var saved = new Rectangle(x, y, w, h);
                if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(saved))) r = saved;
            }
            Bounds = r;
            PerformLayout();
            try
            {
                splitTop.SplitterDistance = settings.GetInt("TreeWidth", D(150));
                int editorH = settings.GetInt("EditorHeight", D(170));
                splitMain.SplitterDistance = Math.Max(D(80), splitMain.Height - editorH - splitMain.SplitterWidth);
            }
            catch { }
            SetPasteMode(settings.GetInt("PasteMode", 0));
            btnWrap.Checked = settings.Get("WordWrap", "1") == "1";
            txtEditor.WordWrap = txtRaw.WordWrap = btnWrap.Checked;

            var col = settings.Get("SortColumn", "Date") == "Title" ? SortColumn.Title : SortColumn.Date;
            listHeader.SetSort(col, settings.Get("SortAscending", ListHeader.DefaultAscending(col) ? "1" : "0") == "1");
        }

        void SaveLayout()
        {
            if (WindowState == FormWindowState.Normal && Visible)
            {
                settings.Set("Bounds", Left + "," + Top + "," + Width + "," + Height);
                settings.Set("TreeWidth", splitTop.SplitterDistance);
                settings.Set("EditorHeight", splitMain.Panel2.Height);
            }
            settings.Set("PasteMode", pasteMode);
            settings.Set("WordWrap", btnWrap.Checked ? "1" : "0");
            settings.Set("SortColumn", listHeader.Column);
            settings.Set("SortAscending", listHeader.Ascending ? "1" : "0");
            settings.Save();
        }
    }
}
