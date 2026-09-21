using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ClipLite
{
    /// <summary>
    /// Settings window: language, startup, hotkey, paste mode, collection limits and the
    /// per-event sounds. Language changes preview live and are rolled back on Cancel.
    /// </summary>
    internal class OptionsDialog : Form
    {
        class SoundRow
        {
            public SoundEvent Ev;
            public RadioButton Off, Default, Custom;
            public ToolStripDropDownButton Preset;
            public Label PathLabel;
            public string PresetName;
            public string CustomPath = "";
        }

        readonly Settings settings;
        readonly float scale;
        readonly List<Action> texts = new List<Action>();
        readonly SoundRow[] rows;
        readonly string originalLanguage;

        string language;
        int pasteMode;
        TextBox txtHotkey, txtInBox, txtTrash;
        CheckBox chkAutostart, chkLoadOnSelect;
        ToolStripDropDownButton btnLanguage, btnPasteMode;

        int D(int px) { return (int)Math.Round(px * scale); }

        public OptionsDialog(Settings settings)
        {
            this.settings = settings;
            originalLanguage = language = settings.Get("Language", Loc.Auto);
            pasteMode = settings.GetInt("PasteMode", 0);
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            KeyPreview = true;
            Txt(() => Text = Loc.T("ClipLite — Options"));

            int y = D(12);
            y = BuildGeneral(y);
            y += D(10);

            Section(y, "Sounds");
            y += D(28);
            var events = new[] { SoundEvent.Capture, SoundEvent.Erase, SoundEvent.Ignore };
            var titles = new[] { "New Data Captured From Clipboard", "Clipboard Erased By Another Application", "Clipboard Data Ignored/Rejected" };
            rows = new SoundRow[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                rows[i] = BuildSoundRow(events[i], titles[i], y);
                y += D(86);
            }

            var ok = MakeButton(new Rectangle(D(432), y + D(8), D(84), D(28)));
            var cancel = MakeButton(new Rectangle(D(524), y + D(8), D(84), D(28)));
            Txt(() => ok.Text = Loc.T("OK"));
            Txt(() => cancel.Text = Loc.T("Cancel"));
            ok.Click += (s, e) => { Save(); DialogResult = DialogResult.OK; };
            cancel.Click += (s, e) => { Relang(originalLanguage); DialogResult = DialogResult.Cancel; };
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(ok);
            Controls.Add(cancel);
            ClientSize = new Size(D(620), y + D(48));

            HandleCreated += (s, e) => Native.DarkTitleBar(Handle);
            Shown += (s, e) => { Activate(); ok.Focus(); };
        }

        // ================= general =================

        int BuildGeneral(int y)
        {
            Section(y, "General");
            y += D(30);

            var lblLang = MakeLabel(new Rectangle(D(12), y + D(4), D(140), D(20)), "Language:");
            btnLanguage = MakePicker(new Rectangle(D(156), y, D(180), D(26)), delegate
            {
                var items = new List<ToolStripItem>();
                foreach (var codeRaw in Loc.Languages)
                {
                    var code = codeRaw;
                    var item = new ToolStripMenuItem(Loc.LanguageName(code), null, (s, e) => Relang(code));
                    item.Checked = code == language;
                    items.Add(item);
                }
                return items;
            }, null);
            Txt(() => btnLanguage.Text = Loc.LanguageName(language));
            y += D(32);

            chkAutostart = new CheckBox
            {
                Location = new Point(D(12), y), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Bg,
                UseMnemonic = false, Checked = Autostart.Enabled
            };
            Txt(() => chkAutostart.Text = Loc.T("Run at Windows startup"));
            Controls.Add(chkAutostart);
            y += D(28);

            chkLoadOnSelect = new CheckBox
            {
                Location = new Point(D(12), y), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Bg,
                UseMnemonic = false, Checked = settings.Get("LoadOnSelect", "1") == "1"
            };
            Txt(() => chkLoadOnSelect.Text = Loc.T("Put the selected clip on the clipboard"));
            Controls.Add(chkLoadOnSelect);
            y += D(28);

            MakeLabel(new Rectangle(D(12), y + D(4), D(140), D(20)), "Global hotkey:");
            txtHotkey = MakeTextBox(new Rectangle(D(156), y, D(180), D(24)), settings.Get("Hotkey", "Ctrl+Alt+V"));
            y += D(30);

            MakeLabel(new Rectangle(D(12), y + D(4), D(140), D(20)), "Paste mode:");
            btnPasteMode = MakePicker(new Rectangle(D(156), y, D(180), D(26)), delegate
            {
                var items = new List<ToolStripItem>();
                for (int i = 0; i < MainForm.PasteModeKeys.Length; i++)
                {
                    int mode = i;
                    var item = new ToolStripMenuItem(Loc.T(MainForm.PasteModeKeys[i]), null, (s, e) =>
                    {
                        pasteMode = mode;
                        btnPasteMode.Text = Loc.T(MainForm.PasteModeKeys[mode]);
                    });
                    item.Checked = i == pasteMode;
                    items.Add(item);
                }
                return items;
            }, null);
            Txt(() => btnPasteMode.Text = Loc.T(MainForm.PasteModeKeys[pasteMode]));
            y += D(32);

            MakeLabel(new Rectangle(D(12), y + D(4), D(140), D(20)), "InBox limit:");
            txtInBox = MakeTextBox(new Rectangle(D(156), y, D(70), D(24)), settings.GetInt("InBoxLimit", 1000).ToString());
            MakeLabel(new Rectangle(D(262), y + D(4), D(130), D(20)), "Trash limit:");
            txtTrash = MakeTextBox(new Rectangle(D(398), y, D(70), D(24)), settings.GetInt("TrashLimit", 1000).ToString());
            foreach (var tb in new[] { txtInBox, txtTrash })
                tb.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            return y + D(30);
        }

        // ================= sounds =================

        SoundRow BuildSoundRow(SoundEvent ev, string title, int y)
        {
            var row = new SoundRow { Ev = ev };
            Section(y, title);

            // radios live in their own container so each row is a separate group
            var group = new Panel { Location = new Point(D(12), y + D(24)), Size = new Size(D(596), D(56)), BackColor = Theme.Bg };
            row.Off = MakeRadio(new Point(0, D(4)), "Off");
            row.Default = MakeRadio(new Point(D(62), D(4)), "Default:");
            row.Custom = MakeRadio(new Point(D(300), D(4)), "Custom:");

            row.Preset = MakePicker(new Rectangle(D(162), D(2), D(130), D(26)), delegate
            {
                var items = new List<ToolStripItem>();
                foreach (var nameRaw in Sounds.Presets)
                {
                    var name = nameRaw;
                    var item = new ToolStripMenuItem(Loc.T(name), null, (s, e) =>
                    {
                        SetPreset(row, name);
                        row.Default.Checked = true;
                        Sounds.PlaySetting("default:" + name);
                    });
                    item.Checked = name == row.PresetName;
                    items.Add(item);
                }
                return items;
            }, group);

            var browse = MakeButton(new Rectangle(D(390), D(2), D(96), D(26)));
            var test = MakeButton(new Rectangle(D(494), D(2), D(96), D(26)));
            Txt(() => browse.Text = Loc.T("Browse…"));
            Txt(() => test.Text = Loc.T("▶  Test"));
            browse.Click += (s, e) => BrowseCustom(row);
            test.Click += (s, e) => Sounds.PlaySetting(SettingOf(row));

            row.PathLabel = new Label
            {
                AutoSize = false, AutoEllipsis = true, Location = new Point(D(300), D(32)), Size = new Size(D(290), D(20)),
                ForeColor = Theme.TextDim, UseMnemonic = false, BackColor = Theme.Bg
            };

            group.Controls.AddRange(new Control[] { row.Off, row.Default, row.Custom, browse, test, row.PathLabel });
            Controls.Add(group);

            // load current setting
            var setting = Sounds.GetSetting(settings, ev);
            SetPreset(row, Sounds.DefaultPreset(ev));
            if (setting == "off") row.Off.Checked = true;
            else if (setting.StartsWith("custom:"))
            {
                row.CustomPath = setting.Substring(7);
                row.Custom.Checked = true;
            }
            else
            {
                var name = Sounds.PresetOf(setting);
                if (Sounds.IsPreset(name)) SetPreset(row, name);
                row.Default.Checked = true;
            }
            UpdatePathLabel(row);
            return row;
        }

        void SetPreset(SoundRow row, string name)
        {
            row.PresetName = name;
            row.Preset.Text = Loc.T(name);
        }

        void BrowseCustom(SoundRow row)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = Loc.T("WAV sounds (*.wav)|*.wav");
                var media = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
                dlg.InitialDirectory = row.CustomPath.Length > 0 ? Path.GetDirectoryName(row.CustomPath) : media;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                row.CustomPath = dlg.FileName;
                row.Custom.Checked = true;
                UpdatePathLabel(row);
                Sounds.PlaySetting("custom:" + row.CustomPath);
            }
        }

        void UpdatePathLabel(SoundRow row)
        {
            row.PathLabel.Text = row.CustomPath.Length > 0 ? Path.GetFileName(row.CustomPath) : "";
        }

        string SettingOf(SoundRow row)
        {
            if (row.Off.Checked) return "off";
            if (row.Custom.Checked && row.CustomPath.Length > 0) return "custom:" + row.CustomPath;
            return "default:" + row.PresetName;
        }

        // ================= save =================

        void Save()
        {
            settings.Set("Language", language);
            settings.Set("Hotkey", txtHotkey.Text.Trim().Length > 0 ? txtHotkey.Text.Trim() : "Ctrl+Alt+V");
            settings.Set("PasteMode", pasteMode);
            settings.Set("LoadOnSelect", chkLoadOnSelect.Checked ? "1" : "0");
            settings.Set("InBoxLimit", ParseLimit(txtInBox.Text, 1000));
            settings.Set("TrashLimit", ParseLimit(txtTrash.Text, 1000));
            foreach (var row in rows) settings.Set(Sounds.Key(row.Ev), SettingOf(row));
            settings.Save();
            try { Autostart.Enabled = chkAutostart.Checked; } catch { }
        }

        static int ParseLimit(string text, int def)
        {
            int v;
            return int.TryParse(text.Trim(), out v) && v >= 10 ? Math.Min(v, 100000) : def;
        }

        // ================= language preview =================

        void Relang(string code)
        {
            language = code;
            Loc.Use(code);
            foreach (var a in texts) a();
            if (rows != null) foreach (var row in rows) SetPreset(row, row.PresetName);
            Loc.Retranslate(); // the main window previews the change behind the dialog
        }

        void Txt(Action apply) { texts.Add(apply); apply(); }

        // ================= widgets =================

        void Section(int y, string key)
        {
            var header = new Label
            {
                AutoSize = false, Location = new Point(D(12), y), Size = new Size(D(596), D(20)),
                ForeColor = Theme.Text, Font = new Font(Theme.UiFont, FontStyle.Bold), UseMnemonic = false, BackColor = Theme.Bg
            };
            Txt(() => header.Text = Loc.T(key));
            var line = new Panel { BackColor = Theme.Border, Location = new Point(D(12), y + D(21)), Size = new Size(D(596), 1) };
            Controls.Add(header);
            Controls.Add(line);
        }

        Label MakeLabel(Rectangle bounds, string key)
        {
            var l = new Label { Bounds = bounds, AutoSize = false, ForeColor = Theme.Text, BackColor = Theme.Bg, UseMnemonic = false };
            Txt(() => l.Text = Loc.T(key));
            Controls.Add(l);
            return l;
        }

        TextBox MakeTextBox(Rectangle bounds, string text)
        {
            var tb = new TextBox
            {
                Bounds = bounds, Text = text, BackColor = Theme.Surface, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(tb);
            return tb;
        }

        RadioButton MakeRadio(Point location, string key)
        {
            var r = new RadioButton { Location = location, AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Bg, UseMnemonic = false };
            Txt(() => r.Text = Loc.T(key));
            return r;
        }

        Button MakeButton(Rectangle bounds)
        {
            var b = new Button
            {
                Bounds = bounds, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Text,
                UseVisualStyleBackColor = false, UseMnemonic = false
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.FlatAppearance.MouseDownBackColor = Theme.Selection;
            return b;
        }

        /// <summary>A dark-themed stand-in for a combo box; the menu is rebuilt on open so the ticks stay right.</summary>
        ToolStripDropDownButton MakePicker(Rectangle bounds, Func<List<ToolStripItem>> build, Control parent)
        {
            var strip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.None, AutoSize = false, CanOverflow = false,
                Location = bounds.Location, Size = bounds.Size, Padding = new Padding(0)
            };
            Theme.Apply(strip);
            var button = new ToolStripDropDownButton
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text, AutoSize = false,
                Size = new Size(bounds.Width - D(4), bounds.Height - D(2)), TextAlign = ContentAlignment.MiddleLeft
            };
            Theme.Apply(button.DropDown);
            button.DropDownOpening += (s, e) =>
            {
                button.DropDownItems.Clear();
                button.DropDownItems.AddRange(build().ToArray());
            };
            strip.Items.Add(button);
            (parent ?? (Control)this).Controls.Add(strip);
            return button;
        }
    }
}
