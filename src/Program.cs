using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AngelsFixRes
{
    // Glass-morphism dark UI matching the sibling app (angels-online-rebind).
    public class MainForm : Form
    {
        static readonly Color APP_BG = ColorTranslator.FromHtml("#08111f");
        static readonly Color APP_BG_ALT = ColorTranslator.FromHtml("#0d1b2f");
        static readonly Color GLASS_BG = ColorTranslator.FromHtml("#13243a");
        static readonly Color GLASS_BG_ALT = ColorTranslator.FromHtml("#182c45");
        static readonly Color GLASS_BORDER = ColorTranslator.FromHtml("#36506f");
        static readonly Color TEXT_MAIN = ColorTranslator.FromHtml("#f3f8ff");
        static readonly Color TEXT_MUTED = ColorTranslator.FromHtml("#9fb3c9");
        static readonly Color ACCENT_CYAN = ColorTranslator.FromHtml("#6ee7f9");
        static readonly Color ACCENT_MINT = ColorTranslator.FromHtml("#8cf5c6");
        static readonly Color ACCENT_AMBER = ColorTranslator.FromHtml("#f8d77a");
        static readonly Color ACCENT_ROSE = ColorTranslator.FromHtml("#ff8ea3");
        static readonly Color BUTTON_DARK = ColorTranslator.FromHtml("#203653");
        static readonly Color DISCORD_BLURPLE = ColorTranslator.FromHtml("#5865f2");
        static readonly Color DISCORD_BLURPLE_HI = ColorTranslator.FromHtml("#727df3");
        static readonly Color KOFI_RED = ColorTranslator.FromHtml("#ff5e5b");
        static readonly Color KOFI_RED_HI = ColorTranslator.FromHtml("#ff7d7a");

        const string DISCORD_USERNAME = "no.sorry";
        const string KOFI_URL = "https://ko-fi.com/nosorry";

        static Font Ui(float s) { return new Font("Segoe UI", s); }
        static Font Semi(float s) { return new Font("Segoe UI Semibold", s); }

        string gameFolder;
        int nativeW, nativeH;
        Label chip, currentState, statusLabel, banner;
        ComboBox resoBox;
        Button revertBtn;

        public MainForm()
        {
            Text = "Angels Online FixRes";
            ClientSize = new Size(640, 500);
            FormBorderStyle = FormBorderStyle.FixedSingle;   // non-resizable
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = APP_BG;
            Font = Ui(9f);
            nativeW = Screen.PrimaryScreen.Bounds.Width;
            nativeH = Screen.PrimaryScreen.Bounds.Height;
            TryLoadIcon();
            BuildUi();
            InitDetect();
        }

        void TryLoadIcon()
        {
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); return; } catch { }
            try { string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "angel.ico"); if (File.Exists(ico)) Icon = new Icon(ico); } catch { }
        }

        // Persist the chosen path in a small file next to THIS exe (per-copy), so a
        // freshly-downloaded exe never inherits another copy's saved path.
        static string ConfigPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixres-path.txt"); }

        void SaveGamePath(string path)
        {
            try { File.WriteAllText(ConfigPath(), path); } catch { }
        }

        string LoadGamePath()
        {
            try { return File.Exists(ConfigPath()) ? File.ReadAllText(ConfigPath()).Trim() : null; } catch { return null; }
        }

        void BuildUi()
        {
            var body = new Panel { Dock = DockStyle.Fill, BackColor = APP_BG, Padding = new Padding(16, 14, 16, 14) };
            var card = MakeGlassCard("Render resolution", 156);
            card.Dock = DockStyle.Top;
            currentState = new Label { Left = 14, Top = 40, Width = 588, Height = 40, Font = Semi(9.5f), ForeColor = TEXT_MUTED, BackColor = GLASS_BG, Text = "" };
            var fixTo = new Label { Left = 14, Top = 92, Width = 70, Height = 24, Text = "Render at:", Font = Ui(10f), ForeColor = TEXT_MAIN, BackColor = GLASS_BG, TextAlign = ContentAlignment.MiddleLeft };
            resoBox = new ComboBox { Left = 92, Top = 89, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = GLASS_BG_ALT, ForeColor = TEXT_MAIN, Font = Semi(10f) };
            var hint = new Label { Left = 14, Top = 120, Width = 588, Height = 28, Font = Ui(8.5f), ForeColor = TEXT_MUTED, BackColor = GLASS_BG, Text = "Your monitor's native resolution renders 1:1 with no stretch (sharpest, and shows more of the world)." };
            card.Controls.Add(currentState); card.Controls.Add(fixTo); card.Controls.Add(resoBox); card.Controls.Add(hint);
            body.Controls.Add(card);

            var updateHint = new Label {
                Left = 16, Top = 184, Width = 500, Height = 60, Font = Ui(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG,
                Text = "After a game update, just open this and press Fix UI again.\r\n\r\n" +
                       "Once applied - you do not need to keep the program open, or run it before playing."
            };
            revertBtn = MakeGlassButton("Revert fix", ACCENT_AMBER); revertBtn.SetBounds(514, 186, 110, 40); revertBtn.Click += OnRevert;
            body.Controls.Add(updateHint); body.Controls.Add(revertBtn);

            // Footer: contact buttons (left), action buttons (right), primary Fix UI emphasised.
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = APP_BG_ALT };
            statusLabel = new Label { Left = 16, Top = 12, Width = 608, Height = 40, Font = Ui(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, Text = "" };
            var discordBtn = MakeContactButton("Discord: " + DISCORD_USERNAME, DISCORD_BLURPLE, DISCORD_BLURPLE_HI); discordBtn.SetBounds(16, 56, 150, 40); discordBtn.Click += OnCopyDiscord;
            var coffeeBtn = MakeContactButton("☕  Buy me a coffee", KOFI_RED, KOFI_RED_HI); coffeeBtn.SetBounds(172, 56, 156, 40); coffeeBtn.Click += OnCoffee;
            var browseBtn = MakeGlassButton("Browse...", GLASS_BORDER); browseBtn.SetBounds(340, 56, 92, 40); browseBtn.Click += OnBrowse;
            var playBtn = MakeGlassButton("Play", ACCENT_CYAN); playBtn.SetBounds(438, 56, 82, 40); playBtn.Click += OnPlay;
            var fixBtn = MakePrimaryButton("Fix UI", ACCENT_MINT); fixBtn.SetBounds(526, 56, 98, 40); fixBtn.Click += OnFix;
            footer.Controls.Add(statusLabel); footer.Controls.Add(discordBtn); footer.Controls.Add(coffeeBtn); footer.Controls.Add(browseBtn); footer.Controls.Add(playBtn); footer.Controls.Add(fixBtn);

            banner = new Label { Dock = DockStyle.Top, Height = 44, BackColor = ACCENT_ROSE, ForeColor = APP_BG, Font = Semi(9.5f), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 16, 0), Visible = false, Text = "" };

            var header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = APP_BG_ALT };
            header.Controls.Add(new Label { Left = 16, Top = 14, AutoSize = true, Font = Semi(16f), ForeColor = TEXT_MAIN, BackColor = APP_BG_ALT, Text = "Angels Online FixRes" });
            header.Controls.Add(new Label { Left = 18, Top = 52, AutoSize = true, Font = Ui(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, Text = "Render the game at your monitor's native resolution - sharp, not stretched." });
            chip = new Label { Left = 18, Top = 74, AutoSize = true, Font = Semi(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, Text = "" };
            header.Controls.Add(chip);

            Controls.Add(body); Controls.Add(footer); Controls.Add(banner); Controls.Add(header);
        }

        Panel MakeGlassCard(string titleText, int height)
        {
            var panel = new Panel { BackColor = GLASS_BG, Height = height, Padding = new Padding(1) };
            panel.Paint += (s, e) => {
                var g = e.Graphics;
                using (var pen = new Pen(GLASS_BORDER)) g.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                using (var b = new SolidBrush(ACCENT_CYAN)) g.FillRectangle(b, 1, 1, panel.Width - 2, 2);
            };
            panel.Controls.Add(new Label { Left = 14, Top = 8, AutoSize = true, Font = Semi(11f), ForeColor = ACCENT_CYAN, BackColor = GLASS_BG, Text = titleText });
            return panel;
        }

        // Dark glass button (accent shows on hover).
        Button MakeGlassButton(string text, Color accent)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = BUTTON_DARK, ForeColor = TEXT_MAIN, Font = Semi(9.5f), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderColor = GLASS_BORDER; b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = accent; b.FlatAppearance.MouseDownBackColor = accent;
            b.MouseEnter += (s, e) => b.ForeColor = APP_BG; b.MouseLeave += (s, e) => b.ForeColor = TEXT_MAIN;
            return b;
        }

        // Filled accent button for the primary action (stands out).
        Button MakePrimaryButton(string text, Color accent)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = accent, ForeColor = APP_BG, Font = Semi(10f), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.05f);
            return b;
        }

        // Filled brand button (Discord / Ko-fi).
        Button MakeContactButton(string text, Color bg, Color hi)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = Semi(9f), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderColor = hi; b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = hi; b.FlatAppearance.MouseDownBackColor = hi;
            return b;
        }

        void InitDetect()
        {
            PopulateResolutions();
            string saved = LoadGamePath();
            if (saved != null && GameFiles.IsGameFolder(saved)) gameFolder = saved;
            else { string detected = GameFiles.AutoDetect(AppDomain.CurrentDomain.BaseDirectory); if (detected != null) gameFolder = detected; }
            RefreshStatus();
        }

        void PopulateResolutions()
        {
            // Show the user's real resolution (and common lower options). The
            // applied render is hard-capped at 1920x1080 in the background
            // (ApplyRenderFix), since the engine crashes above that - but we do
            // not hide the user's actual resolution from them.
            var list = new List<string> { nativeW + "x" + nativeH };
            int[][] common = { new[] { 2560, 1440 }, new[] { 1920, 1080 }, new[] { 1600, 900 }, new[] { 1366, 768 }, new[] { 1280, 720 } };
            foreach (var r in common)
            {
                string s = r[0] + "x" + r[1];
                if (r[0] <= nativeW && r[1] <= nativeH && !list.Contains(s)) list.Add(s);
            }
            resoBox.Items.Clear();
            foreach (var s in list) resoBox.Items.Add(s);
            resoBox.SelectedIndex = 0;
        }

        void SetChip(string text, Color color) { chip.Text = "● " + text; chip.ForeColor = color; }

        void RefreshStatus()
        {
            banner.Visible = false;
            if (gameFolder == null || !GameFiles.IsGameFolder(gameFolder))
            {
                SetChip("No game folder found - click Browse to select it.", ACCENT_ROSE);
                currentState.Text = ""; if (revertBtn != null) revertBtn.Enabled = false; return;
            }
            GameStatus s = GameFiles.Inspect(gameFolder);
            if (revertBtn != null) revertBtn.Enabled = s.HasBackup;
            if (!s.Recognized)
            {
                SetChip("Client not recognised - it may have updated. Cannot patch safely.", ACCENT_ROSE);
                currentState.Text = "The tool did not recognise this client build, so it will not touch it."; currentState.ForeColor = ACCENT_ROSE;
                return;
            }
            SetChip("Game folder:  " + gameFolder, ACCENT_MINT);
            bool highRes = nativeW > 1920 || nativeH > 1080;
            if (s.Applied)
            {
                currentState.Text = "Fix is active - the game renders sharp for your " + nativeW + "x" + nativeH + " display.";
                currentState.ForeColor = ACCENT_MINT;
            }
            else
            {
                currentState.Text = highRes
                    ? "Not fixed yet - click Fix UI to fix the game for your " + nativeW + "x" + nativeH + " display."
                    : "Not fixed yet - the game renders at 1280x720 and stretches it to your " + nativeW + "x" + nativeH + " screen (blurry).";
                currentState.ForeColor = ACCENT_AMBER;
            }
            if (GameFiles.DatLocked(gameFolder))
            {
                banner.Text = "Angels Online is running - close it before applying (the client file is locked while the game is open).";
                banner.Visible = true;
            }
        }

        void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select your Angels Online Global folder (contains angel.dat and midage.ini)";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (GameFiles.IsGameFolder(dlg.SelectedPath)) { gameFolder = dlg.SelectedPath; SaveGamePath(gameFolder); RefreshStatus(); }
                    else MessageBox.Show(this, "That folder does not contain angel.dat and midage.ini.\r\nPlease select your Angels Online Global game folder.", "Not a game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        bool EnsureFolder()
        {
            if (gameFolder != null && GameFiles.IsGameFolder(gameFolder)) return true;
            MessageBox.Show(this, "Please select your game folder first using Browse...", "No game folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        int[] SelectedRes()
        {
            string[] p = ((string)resoBox.SelectedItem).Split('x');
            return new[] { int.Parse(p[0]), int.Parse(p[1]) };
        }

        void OnFix(object sender, EventArgs e) { DoFix(); }

        bool DoFix()
        {
            if (!EnsureFolder()) return false;
            int[] r = SelectedRes();
            // Windowed only matters when the monitor itself is above the engine ceiling.
            bool forceWindowed = nativeW > 1920 || nativeH > 1080;
            GameStatus s = GameFiles.Inspect(gameFolder);
            bool locked = GameFiles.DatLocked(gameFolder);
            var reqs = new List<KeyValuePair<string, bool>> {
                new KeyValuePair<string, bool>("Game folder found (angel.dat + midage.ini)", true),
                new KeyValuePair<string, bool>("Client build recognised", s.Recognized),
                new KeyValuePair<string, bool>("Angels Online is closed", !locked),
            };
            var steps = new[] {
                "Back up angel.dat and midage.ini (timestamped .bak).",
                "Apply the render fix so the game draws its sharpest for your display.",
                "You can undo it anytime with the Revert fix button.",
            };
            string summary = "Apply the resolution fix for your " + r[0] + "x" + r[1] + " display. Both files are backed up first, so you can revert anytime.";
            if (!ConfirmAndRun("Apply the resolution fix", summary, steps, reqs, "Apply")) return false;

            try
            {
                ApplyOutcome o = GameFiles.ApplyRenderFix(gameFolder, r[0], r[1], DateTime.Now, forceWindowed);
                if (!o.Success)
                {
                    statusLabel.ForeColor = ACCENT_ROSE; statusLabel.Text = o.Message;
                    MessageBox.Show(this, o.Message, "Not applied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                statusLabel.ForeColor = ACCENT_MINT;
                statusLabel.Text = "Fixed for " + r[0] + "x" + r[1] + ".  Backup: " + Path.GetFileName(o.DatBackup);
                RefreshStatus();
                return true;
            }
            catch (IOException) { RefreshStatus(); MessageBox.Show(this, "Could not write the client - please close the game first, then try again.", "File in use", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (UnauthorizedAccessException) { MessageBox.Show(this, "Permission denied. Try running this tool as administrator.", "Permission denied", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            return false;
        }

        void OnPlay(object sender, EventArgs e)
        {
            if (!DoFix()) return;
            string launcher = Path.Combine(gameFolder, GameFiles.LauncherName);
            if (!File.Exists(launcher)) { MessageBox.Show(this, "START.EXE was not found in the game folder.", "Launcher missing", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = gameFolder, UseShellExecute = true });
                statusLabel.ForeColor = ACCENT_CYAN; statusLabel.Text = "Launching the game...";
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not start the game: " + ex.Message, "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OnCopyDiscord(object sender, EventArgs e)
        {
            try { Clipboard.SetText(DISCORD_USERNAME); statusLabel.ForeColor = TEXT_MUTED; statusLabel.Text = "Copied Discord username '" + DISCORD_USERNAME + "' to the clipboard - message me about any bug or issue."; } catch { }
        }

        void OnCoffee(object sender, EventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(KOFI_URL) { UseShellExecute = true }); statusLabel.ForeColor = TEXT_MUTED; statusLabel.Text = "Thank you! Opening ko-fi.com/nosorry in your browser."; }
            catch (Exception ex) { MessageBox.Show(this, "Could not open the link: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OnRevert(object sender, EventArgs e)
        {
            if (!EnsureFolder()) return;
            if (!GameFiles.Inspect(gameFolder).HasBackup)
            {
                MessageBox.Show(this, "There is no FixRes backup to revert to yet. Apply the fix first - a backup is made automatically.", "Nothing to revert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Revert Angels Online to the most recent FixRes backup (undo the last fix)? You can re-apply anytime.", "Revert fix", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            try
            {
                ApplyOutcome o = GameFiles.RevertLast(gameFolder);
                statusLabel.ForeColor = o.Success ? ACCENT_MINT : ACCENT_ROSE;
                statusLabel.Text = o.Success ? ("Reverted from " + Path.GetFileName(o.DatBackup) + ". Relaunch the game to see it.") : o.Message;
                RefreshStatus();
            }
            catch (IOException) { MessageBox.Show(this, "Could not restore - please close the game first, then try again.", "File in use", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        bool ConfirmAndRun(string title, string summary, string[] steps, List<KeyValuePair<string, bool>> requirements, string runLabel)
        {
            bool allMet = requirements.TrueForAll(r => r.Value);
            var dlg = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, BackColor = APP_BG, ClientSize = new Size(480, 360), Font = Ui(9f) };
            try { if (Icon != null) dlg.Icon = Icon; } catch { }
            dlg.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 2, BackColor = ACCENT_CYAN });
            int y = 18;
            dlg.Controls.Add(new Label { Left = 20, Top = y, AutoSize = true, Font = Semi(14f), ForeColor = TEXT_MAIN, Text = title }); y += 34;
            dlg.Controls.Add(new Label { Left = 20, Top = y, Width = 440, Height = 44, Font = Ui(9.5f), ForeColor = TEXT_MUTED, Text = summary }); y += 52;
            dlg.Controls.Add(new Label { Left = 20, Top = y, AutoSize = true, Font = Semi(10f), ForeColor = ACCENT_CYAN, Text = "What will happen" }); y += 24;
            int n = 1;
            foreach (var step in steps) { dlg.Controls.Add(new Label { Left = 26, Top = y, Width = 434, Height = 18, Font = Ui(9f), ForeColor = TEXT_MAIN, Text = (n++) + ". " + step }); y += 20; }
            y += 6;
            dlg.Controls.Add(new Label { Left = 20, Top = y, AutoSize = true, Font = Semi(10f), ForeColor = ACCENT_CYAN, Text = "Requirements" }); y += 24;
            foreach (var rq in requirements)
            {
                dlg.Controls.Add(new Label { Left = 26, Top = y, Width = 20, Height = 18, Font = Semi(10f), Text = rq.Value ? "✓" : "✗", ForeColor = rq.Value ? ACCENT_MINT : ACCENT_ROSE });
                dlg.Controls.Add(new Label { Left = 48, Top = y, Width = 412, Height = 18, Font = Ui(9f), ForeColor = TEXT_MAIN, Text = rq.Key });
                y += 22;
            }
            y += 6;
            dlg.Controls.Add(new Label { Left = 20, Top = y, Width = 440, Height = 34, Font = Ui(9f), ForeColor = allMet ? ACCENT_MINT : ACCENT_AMBER, Text = allMet ? ("All requirements met. Click " + runLabel + " to apply.") : "Heads up: an item is not met, so this may not work. You can still proceed." });

            bool proceeded = false;
            var proceed = MakePrimaryButton(runLabel, allMet ? ACCENT_MINT : ACCENT_AMBER); proceed.SetBounds(360, 316, 100, 34); proceed.Click += (s, e) => { proceeded = true; dlg.Close(); };
            var cancel = MakeGlassButton("Cancel", GLASS_BORDER); cancel.SetBounds(252, 316, 100, 34); cancel.Click += (s, e) => { proceeded = false; dlg.Close(); };
            dlg.Controls.Add(proceed); dlg.Controls.Add(cancel);
            dlg.ShowDialog(this);
            return proceeded;
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
