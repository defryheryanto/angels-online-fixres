using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
        static readonly Color CAUTION_RED = ColorTranslator.FromHtml("#ff4d5e");
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
        int nativeW, nativeH, windowedW, windowedH;
        Label chip, currentState, statusLabel, banner;
        ComboBox windowMode;
        Button revertBtn;
        ShineButton fixBtn;

        public MainForm()
        {
            Text = "Angels Online FixRes";
            ClientSize = new Size(640, 550);
            FormBorderStyle = FormBorderStyle.FixedSingle;   // non-resizable
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = APP_BG;
            Font = Ui(9f);
            PhysicalResolution(out nativeW, out nativeH);
            WindowedClientSize(nativeW, nativeH, out windowedW, out windowedH);
            TryLoadIcon();
            BuildUi();
            InitDetect();
        }

        void TryLoadIcon()
        {
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); return; } catch { }
            try { string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "angel.ico"); if (File.Exists(ico)) Icon = new Icon(ico); } catch { }
        }

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hDC, int nIndex);
        const int DESKTOPVERTRES = 117, DESKTOPHORZRES = 118;

        // The monitor's TRUE physical resolution, correct even when the tool runs DPI-unaware under
        // Windows display scaling. Screen.Bounds would report the virtualized size (e.g. a 4K screen
        // at 200% looks like 1920x1080); DESKTOPHORZRES/VERTRES report the real pixels regardless.
        static void PhysicalResolution(out int w, out int h)
        {
            w = 0; h = 0;
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                w = GetDeviceCaps(hdc, DESKTOPHORZRES);
                h = GetDeviceCaps(hdc, DESKTOPVERTRES);
                ReleaseDC(IntPtr.Zero, hdc);
            }
            if (w <= 0 || h <= 0) { w = Screen.PrimaryScreen.Bounds.Width; h = Screen.PrimaryScreen.Bounds.Height; }
        }

        // Return a client size whose decorated outer window fits inside the Windows work area.
        // Screen/SystemInformation values are DPI-virtualized in this legacy .NET application,
        // so scale them back to the physical pixels used by the HIGHDPIAWARE game client.
        static void WindowedClientSize(int physicalW, int physicalH, out int w, out int h)
        {
            // A normal resizable window has a small invisible resize border outside its painted
            // frame. Compensate for its bottom edge so the visible frame sits flush with the
            // taskbar instead of leaving a thin strip of desktop above it.
            const int InvisibleBottomBorderCompensation = 6;
            Screen screen = Screen.PrimaryScreen;
            double sx = screen.Bounds.Width > 0 ? (double)physicalW / screen.Bounds.Width : 1.0;
            double sy = screen.Bounds.Height > 0 ? (double)physicalH / screen.Bounds.Height : 1.0;
            int workW = (int)Math.Round(screen.WorkingArea.Width * sx);
            int workH = (int)Math.Round(screen.WorkingArea.Height * sy);
            int frameW = (int)Math.Ceiling(SystemInformation.FrameBorderSize.Width * sx) * 2;
            int frameH = (int)Math.Ceiling(SystemInformation.FrameBorderSize.Height * sy) * 2;
            int captionH = (int)Math.Ceiling(SystemInformation.CaptionHeight * sy);
            w = Math.Max(640, workW - frameW);
            h = Math.Max(480, workH - frameH - captionH + InvisibleBottomBorderCompensation);
        }


        // Persist the chosen path in a small file next to THIS exe (per-copy), so a
        // freshly-downloaded exe never inherits another copy's saved path.
        static string ConfigPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixres-path.txt"); }
        static string WindowModeConfigPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixres-window-mode.txt"); }

        void SaveGamePath(string path)
        {
            try { File.WriteAllText(ConfigPath(), path); } catch { }
        }

        string LoadGamePath()
        {
            try { return File.Exists(ConfigPath()) ? File.ReadAllText(ConfigPath()).Trim() : null; } catch { return null; }
        }

        void SaveWindowMode(string mode)
        {
            try { File.WriteAllText(WindowModeConfigPath(), mode); } catch { }
        }

        string LoadWindowMode()
        {
            try
            {
                if (!File.Exists(WindowModeConfigPath())) return "borderless-window";
                string mode = File.ReadAllText(WindowModeConfigPath()).Trim();
                return mode == "windowed" ? "windowed" : "borderless-window";
            }
            catch { return "borderless-window"; }
        }

        void BuildUi()
        {
            var body = new Panel { Dock = DockStyle.Fill, BackColor = APP_BG, Padding = new Padding(16, 14, 16, 14) };
            var card = MakeGlassCard("Render resolution", 116);
            card.Dock = DockStyle.Top;
            currentState = new Label { Left = 14, Top = 40, Width = 588, Height = 40, Font = Semi(9.5f), ForeColor = TEXT_MUTED, BackColor = GLASS_BG, Text = "" };
            var hint = new Label { Left = 14, Top = 82, Width = 588, Height = 26, Font = Ui(8.5f), ForeColor = TEXT_MUTED, BackColor = GLASS_BG, Text = "The tool detects your monitor and applies the sharpest setting for it automatically." };
            card.Controls.Add(currentState); card.Controls.Add(hint);
            body.Controls.Add(card);

            body.Controls.Add(new Label { Left = 16, Top = 140, Width = 92, Height = 24, Font = Semi(9f), ForeColor = TEXT_MAIN, BackColor = APP_BG, Text = "Window type" });
            windowMode = new ComboBox {
                Left = 110, Top = 136, Width = 180, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = GLASS_BG_ALT, ForeColor = TEXT_MAIN, Font = Ui(9f)
            };
            windowMode.Items.Add("borderless-window");
            windowMode.Items.Add("windowed");
            windowMode.SelectedItem = LoadWindowMode();
            body.Controls.Add(windowMode);

            // Both choices use the same render/UI fix. Only the native Windows window type changes.
            bool widePanel = (nativeW > 1920 || nativeH > 1080) && nativeW * 9 != nativeH * 16;
            var fillHint = new Label {
                Left = 16, Top = 168, Width = 608, Height = 28, Font = Ui(8.5f), ForeColor = TEXT_MUTED, BackColor = APP_BG,
                Text = "Borderless fills the monitor; windowed uses the same sharp UI fix in a regular window."
                    + (widePanel ? "\r\nYour screen is wider than 16:9, so the picture is stretched a little at the sides to fill it." : "")
            };
            body.Controls.Add(fillHint);

            var updateHint = new Label {
                Left = 16, Top = 196, Width = 500, Height = 60, Font = Ui(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG,
                Text = "After a game update, just open this and press Fix UI again.\r\n\r\n" +
                       "Once applied - you do not need to keep the program open, or run it before playing."
            };
            revertBtn = MakeGlassButton("Revert fix", ACCENT_AMBER); revertBtn.SetBounds(514, 198, 110, 40); revertBtn.Click += OnRevert;
            body.Controls.Add(updateHint); body.Controls.Add(revertBtn);

            var cautionBox = new Panel { Left = 16, Top = 260, Width = 608, Height = 64, BackColor = APP_BG };
            cautionBox.Paint += (s, e) => {
                var g = e.Graphics;
                using (var pen = new Pen(ACCENT_AMBER)) g.DrawRectangle(pen, 0, 0, cautionBox.Width - 1, cautionBox.Height - 1);
                using (var bar = new SolidBrush(ACCENT_AMBER)) g.FillRectangle(bar, 0, 0, 4, cautionBox.Height);   // amber accent bar
            };
            cautionBox.Controls.Add(new Label {
                Left = 16, Top = 7, Width = 584, Height = 50, Font = Semi(9f), ForeColor = ACCENT_AMBER, BackColor = APP_BG,
                Text = "⚠  Heads up: while the fix is active, the in-game Display resolution menu is turned off. The tool " +
                       "sets how the game fills your screen, so changing it in-game would break that. To change resolution " +
                       "in-game the normal way again, click Revert fix - it restores the original game."
            });
            body.Controls.Add(cautionBox);

            // Footer: contact buttons (left), action buttons (right), primary Fix UI emphasised.
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = APP_BG_ALT };
            statusLabel = new Label { Left = 16, Top = 12, Width = 608, Height = 40, Font = Ui(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, Text = "" };
            var discordBtn = MakeContactButton("Discord: " + DISCORD_USERNAME, DISCORD_BLURPLE, DISCORD_BLURPLE_HI); discordBtn.SetBounds(16, 56, 150, 40); discordBtn.Click += OnCopyDiscord;
            var coffeeBtn = MakeContactButton("☕  Buy me a coffee", KOFI_RED, KOFI_RED_HI); coffeeBtn.SetBounds(172, 56, 156, 40); coffeeBtn.Click += OnCoffee;
            var browseBtn = MakeGlassButton("Browse...", GLASS_BORDER); browseBtn.SetBounds(340, 56, 92, 40); browseBtn.Click += OnBrowse;
            var playBtn = MakeGlassButton("Play", ACCENT_CYAN); playBtn.SetBounds(438, 56, 82, 40); playBtn.Click += OnPlay;
            fixBtn = new ShineButton(ACCENT_MINT, ACCENT_CYAN, APP_BG_ALT) { Label = "Fix UI", AccessibleName = "Fix UI", Font = Semi(10f), ForeColor = APP_BG }; fixBtn.SetBounds(526, 56, 98, 40); fixBtn.Click += OnFix;
            footer.Controls.Add(statusLabel); footer.Controls.Add(discordBtn); footer.Controls.Add(coffeeBtn); footer.Controls.Add(browseBtn); footer.Controls.Add(playBtn); footer.Controls.Add(fixBtn);

            banner = new Label { Dock = DockStyle.Top, Height = 44, BackColor = ACCENT_ROSE, ForeColor = APP_BG, Font = Semi(9.5f), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 16, 0), Visible = false, Text = "" };

            var header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = APP_BG_ALT };
            header.Controls.Add(new Label { Left = 16, Top = 14, AutoSize = true, Font = Semi(16f), ForeColor = TEXT_MAIN, BackColor = APP_BG_ALT, Text = "Angels Online FixRes" });
            header.Controls.Add(new Label { Left = 18, Top = 52, AutoSize = true, Font = Ui(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, Text = "Fill your whole screen sharply - not a blurry stretch, you can change the window mode." });
            chip = new Label { Left = 18, Top = 74, AutoSize = true, Font = Semi(9f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, Text = "" };
            header.Controls.Add(chip);
            var changelogBtn = MakeGlassButton("Changelog", ACCENT_CYAN); changelogBtn.SetBounds(516, 14, 108, 30); changelogBtn.Click += (s, e) => ShowChangelog();
            header.Controls.Add(changelogBtn);
            header.Controls.Add(new Label { Left = 516, Top = 48, Width = 108, Height = 16, Font = Ui(8.5f), ForeColor = TEXT_MUTED, BackColor = APP_BG_ALT, TextAlign = ContentAlignment.TopRight, Text = AppVersion() });

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

        // Button that owner-draws its label perfectly centred. The stock flat-button text
        // can sit a pixel or two off vertically with these fonts; base.OnPaint still draws
        // the flat background / border / hover, and we draw the (empty-Text) label centred.
        sealed class CenteredButton : Button
        {
            public string Label = "";
            public CenteredButton() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                TextRenderer.DrawText(e.Graphics, Label, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
        }

        static CenteredButton NewButton(string text)
        {
            return new CenteredButton { Label = text, Text = "", AccessibleName = text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        }

        // Dark glass button (accent shows on hover).
        Button MakeGlassButton(string text, Color accent)
        {
            var b = NewButton(text); b.BackColor = BUTTON_DARK; b.ForeColor = TEXT_MAIN; b.Font = Semi(9.5f);
            b.FlatAppearance.BorderColor = GLASS_BORDER; b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = accent; b.FlatAppearance.MouseDownBackColor = accent;
            b.MouseEnter += (s, e) => b.ForeColor = APP_BG; b.MouseLeave += (s, e) => b.ForeColor = TEXT_MAIN;
            return b;
        }

        // Filled accent button for the primary action (stands out).
        Button MakePrimaryButton(string text, Color accent)
        {
            var b = NewButton(text); b.BackColor = accent; b.ForeColor = APP_BG; b.Font = Semi(10f);
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.05f);
            return b;
        }

        // Filled brand button (Discord / Ko-fi).
        Button MakeContactButton(string text, Color bg, Color hi)
        {
            var b = NewButton(text); b.BackColor = bg; b.ForeColor = Color.White; b.Font = Semi(9f);
            b.FlatAppearance.BorderColor = hi; b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = hi; b.FlatAppearance.MouseDownBackColor = hi;
            return b;
        }

        void InitDetect()
        {
            string saved = LoadGamePath();
            if (saved != null && GameFiles.IsGameFolder(saved)) gameFolder = saved;
            else { string detected = GameFiles.AutoDetect(AppDomain.CurrentDomain.BaseDirectory); if (detected != null) gameFolder = detected; }
            RefreshStatus();
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
                currentState.Text = "Fix is active - the game renders sharp for your " + nativeW + "x" + nativeH
                    + " display in " + LoadWindowMode() + " mode.";
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

        void OnFix(object sender, EventArgs e) { DoFix(); }

        bool DoFix()
        {
            if (!EnsureFolder()) return false;
            // Plan from the actual monitor: above 1080p -> render 1920x1080 and stretch it to fill
            // the whole screen; at/below 1080p a non-layout size falls back to a clean window.
            FixCore.FillPlan plan = FixCore.PlanFill(nativeW, nativeH);
            bool windowed = windowMode != null && windowMode.SelectedIndex == 1;
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
            string summary = "Apply the resolution fix for your " + nativeW + "x" + nativeH + " display in "
                + (windowed ? "a regular window" : "borderless-window mode") + ". Both files are backed up first, so you can revert anytime.";
            if (!ConfirmAndRun("Apply the resolution fix", summary, steps, reqs, "Apply")) return false;

            try
            {
                // Fill mode: render at a shipped layout and let the engine stretch it to fill the
                // whole screen. Non-fillable monitors (odd sizes at/below 1080p) fall back to windowed.
                int renderW = plan.UseFill ? plan.RenderW : Math.Min(nativeW, 1920);
                int renderH = plan.UseFill ? plan.RenderH : Math.Min(nativeH, 1080);
                ApplyOutcome o = windowed
                    ? GameFiles.ApplyFillFix(gameFolder, renderW, renderH, nativeW, nativeH, DateTime.Now, true, windowedW, windowedH)
                    : (plan.UseFill
                        ? GameFiles.ApplyFillFix(gameFolder, plan.RenderW, plan.RenderH, plan.NativeW, plan.NativeH, DateTime.Now)
                        : GameFiles.ApplyRenderFix(gameFolder, renderW, renderH, DateTime.Now, nativeW > 1920 || nativeH > 1080));
                if (!o.Success)
                {
                    statusLabel.ForeColor = ACCENT_ROSE; statusLabel.Text = o.Message;
                    MessageBox.Show(this, o.Message, "Not applied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                statusLabel.ForeColor = ACCENT_MINT;
                statusLabel.Text = "Fixed for " + nativeW + "x" + nativeH + "."
                    + (string.IsNullOrEmpty(o.DatBackup) ? "  (kept existing clean backup)" : "  Backup: " + Path.GetFileName(o.DatBackup));
                SaveWindowMode(windowed ? "windowed" : "borderless-window");
                if (fixBtn != null) fixBtn.Flash();
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
            if (!EnsureFolder()) return;
            // If the fix is already applied, launch straight away. If not, warn (and offer to
            // apply first) - don't silently re-run the whole apply confirmation on every Play.
            if (!GameFiles.Inspect(gameFolder).Applied)
            {
                var r = MessageBox.Show(this,
                    "The resolution fix isn't applied, so the game will look blurry or stretched.\r\n\r\nApply the fix now before launching?",
                    "Fix not applied", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes && !DoFix()) return;   // No -> launch anyway
            }
            LaunchGame();
        }

        void LaunchGame()
        {
            string launcher = Path.Combine(gameFolder, GameFiles.LauncherName);
            if (!File.Exists(launcher)) { MessageBox.Show(this, "START.EXE was not found in the game folder.", "Launcher missing", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = gameFolder, UseShellExecute = true });
                // Transient: the game is launched by START.EXE (which then exits), so the tool can't
                // track it - just show a brief confirmation that clears itself instead of a stuck message.
                FlashStatus("Game launched - you can close this tool.", ACCENT_CYAN);
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not start the game: " + ex.Message, "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // Show a status message that clears itself after a few seconds (unless replaced meanwhile).
        void FlashStatus(string text, Color color, int ms = 6000)
        {
            statusLabel.ForeColor = color; statusLabel.Text = text;
            var t = new System.Windows.Forms.Timer { Interval = ms };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (!statusLabel.IsDisposed && statusLabel.Text == text) statusLabel.Text = ""; };
            t.Start();
        }

        static string AppVersion()
        {
            System.Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return "v" + v.Major + "." + v.Minor + "." + v.Build;
        }

        void ShowChangelog()
        {
            var dlg = new Form { Text = "Changelog", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, BackColor = APP_BG, ClientSize = new Size(480, 470), Font = Ui(9f) };
            try { if (Icon != null) dlg.Icon = Icon; } catch { }
            dlg.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 2, BackColor = ACCENT_CYAN });
            dlg.Controls.Add(new Label { Left = 20, Top = 16, AutoSize = true, Font = Semi(14f), ForeColor = TEXT_MAIN, Text = "What's new" });
            dlg.Controls.Add(new Label { Left = 22, Top = 46, AutoSize = true, Font = Ui(9f), ForeColor = TEXT_MUTED, Text = "Angels Online FixRes - changes since v1.0.0" });

            var rtb = new RichTextBox { Left = 16, Top = 74, Width = 448, Height = 340, BackColor = GLASS_BG, ForeColor = TEXT_MAIN, BorderStyle = BorderStyle.None, ReadOnly = true, Font = Ui(9.5f), TabStop = false };
            dlg.Controls.Add(rtb);
            ChangelogEntry(rtb, "v1.2.0", "(latest)", new[] {
                "Fullscreen fill for every big monitor: 4K, 5K, ultrawide and 16:10 screens now all run borderless fullscreen and fill the whole display, instead of some of them playing in a small window in the corner.",
                "Ultrawide / 21:9 screens fill edge to edge (the game's 16:9 picture is stretched slightly to reach the sides).",
                "The tool detects your monitor and fills the screen automatically - nothing to pick or tick.",
                "The game keeps running and rendering while you are tabbed out - no minimizing, no freezing.",
                "Correct resolution detection and scaling at any Windows display scaling (125%, 150%, 200%), so high-DPI monitors look right.",
                "While the fix is active, the in-game Display resolution menu is turned off (changing it would fight the tool's setup) - one-click Revert fix restores it.",
                "Play launches the game straight away when the fix is applied; one-click Revert restores the original game and the normal in-game resolution menu.",
                "Stability and reliability fixes.",
            });
            ChangelogEntry(rtb, "v1.1.0", null, new[] {
                "Render capped at 1920x1080 for stability (the client is unreliable above it).",
                "A clean window on monitors larger than 1080p, removing the black bars.",
                "Detects your real monitor resolution automatically.",
            });
            ChangelogEntry(rtb, "v1.0.0", null, new[] {
                "First release: makes Angels Online render sharper instead of blurry and stretched.",
            });
            rtb.SelectionStart = 0; rtb.ScrollToCaret();

            var close = MakePrimaryButton("Close", ACCENT_MINT); close.SetBounds(364, 424, 100, 32); close.Click += (s, e) => dlg.Close();
            dlg.Controls.Add(close); dlg.AcceptButton = close;
            dlg.ShowDialog(this);
        }

        void ChangelogEntry(RichTextBox rtb, string version, string tag, string[] items)
        {
            rtb.SelectionIndent = 0; rtb.SelectionHangingIndent = 0;
            rtb.SelectionFont = Semi(11.5f);
            rtb.SelectionColor = ACCENT_CYAN;
            rtb.AppendText(version + (tag != null ? "   " + tag : "") + "\r\n");
            foreach (string it in items)
            {
                rtb.SelectionIndent = 10; rtb.SelectionHangingIndent = 14;   // wrapped lines align under the text
                rtb.SelectionFont = Ui(9.5f);
                rtb.SelectionColor = TEXT_MAIN;
                rtb.AppendText("-  " + it + "\r\n");
            }
            rtb.SelectionIndent = 0; rtb.SelectionHangingIndent = 0;
            rtb.AppendText("\r\n");
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
            var dlg = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, BackColor = APP_BG, ClientSize = new Size(480, 374), Font = Ui(9f) };
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
            dlg.Controls.Add(new Label { Left = 20, Top = y, Width = 448, Height = 20, Font = Ui(9f), ForeColor = allMet ? ACCENT_MINT : ACCENT_AMBER, Text = allMet ? ("All requirements met. Click " + runLabel + " to apply.") : "Heads up: an item is not met, so this may not work. You can still proceed." });

            bool proceeded = false;
            var proceed = allMet
                ? new ShineButton(ACCENT_MINT, ACCENT_CYAN, APP_BG)
                : new ShineButton(ACCENT_AMBER, ControlPaint.Dark(ACCENT_AMBER, 0.12f), APP_BG);
            proceed.Label = runLabel; proceed.AccessibleName = runLabel; proceed.Font = Semi(10f); proceed.ForeColor = APP_BG;
            proceed.SetBounds(360, 322, 100, 36); proceed.Click += (s, e) => { proceeded = true; dlg.Close(); };
            var cancel = MakeGlassButton("Cancel", GLASS_BORDER); cancel.SetBounds(252, 322, 100, 36); cancel.Click += (s, e) => { proceeded = false; dlg.Close(); };
            dlg.Controls.Add(proceed); dlg.Controls.Add(cancel);
            proceed.BringToFront(); cancel.BringToFront();
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
