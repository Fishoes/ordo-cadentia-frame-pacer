// Uninstaller.cs — removes Ordo Cadentia without leaving anything behind.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using OrdoCadentia;

namespace Uninstaller
{
    internal sealed class UninstallerWindow : Form
    {
        enum Phase { Ready, Removing, Done, Failed }

        const string AppName = "Ordo Cadentia";
        const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OrdoCadentia";

        Phase _phase = Phase.Ready;
        string _folder;
        string _error = "", _tick = "";
        int _removed;

        /// <summary>Folder to remove as soon as this process exits. Null = nothing to do.</summary>
        public static string FolderToClean;

        Rectangle _titleBar, _closeBox;
        bool _dragging; Point _grab;
        float _pulse; int _tick2;
        Timer _anim;

        PrimaryButton _btnPrimary;
        GlyphButton _btnCancel;
        Toggle _optSettings;

        public UninstallerWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(620), Theme.S(520));
            BackColor = Theme.Pitch;
            Text = Txt.DesTitulo + " " + AppName;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _folder = FindFolder();

            _titleBar = new Rectangle(0, 0, ClientSize.Width, Theme.S(36));
            _closeBox = new Rectangle(ClientSize.Width - Theme.S(36), Theme.S(4), Theme.S(30), Theme.S(26));

            Build();

            _anim = new Timer { Interval = 60 };
            _anim.Tick += (s, e) =>
            {
                _tick2++;
                _pulse = (float)((Math.Sin(_tick2 * 0.10) + 1) / 2);
                Invalidate(new Rectangle(0, Theme.S(44), ClientSize.Width, Theme.S(130)));
                if (_phase == Phase.Removing) Invalidate();
            };
            _anim.Start();
        }

        /// <summary>Where the program lives: the uninstaller's own folder, or the registry's.</summary>
        static string FindFolder()
        {
            string minha = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            if (File.Exists(Path.Combine(minha, "OrdoCadentia.exe"))) return minha;

            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    if (k != null)
                    {
                        var v = k.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) return v.TrimEnd('\\');
                    }
            }
            catch { }
            return minha;
        }

        void Build()
        {
            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            _optSettings = new Toggle
            {
                Title = Txt.DesOptAjustes,
                Description = Txt.DesOptAjustesDesc,
                On = false,
                Height = Theme.S(44),
                Bounds = new Rectangle(m, Theme.S(316), larg, Theme.S(44))
            };
            Controls.Add(_optSettings);

            _btnPrimary = new PrimaryButton
            {
                Text = Txt.DesBtDesinstalar,
                Subtext = Txt.DesBtSub,
                Bounds = new Rectangle(m, ClientSize.Height - Theme.S(96), larg, Theme.S(58))
            };
            _btnPrimary.Click += (s, e) => PrimaryButton();
            Controls.Add(_btnPrimary);

            _btnCancel = new GlyphButton
            {
                Text = Txt.DlgCancelar,
                Bounds = new Rectangle(m, ClientSize.Height - Theme.S(32), larg, Theme.S(26))
            };
            _btnCancel.Click += (s, e) => Close();
            Controls.Add(_btnCancel);
        }

        void PrimaryButton()
        {
            if (_phase == Phase.Done) { Close(); return; }
            if (_phase == Phase.Failed) { _phase = Phase.Ready; RefreshPhase(); return; }
            if (_phase == Phase.Removing) return;
            Remove();
        }

        // ---------------------------------------------------------------- remove
        void Remove()
        {
            if (Process.GetProcessesByName("OrdoCadentia").Length > 0)
            {
                MessageBox.Show(this, Txt.DesJaAberto, Txt.DesTitulo + " " + AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _phase = Phase.Removing;
            RefreshPhase();
            Application.DoEvents();

            try
            {
                // 1. undo any flag left in the user's registry
                Step(Txt.DesPassoRegistro);
                int sobras = Engine.CleanFullscreenLeftovers();
                if (sobras > 0) _removed += sobras;

                // 2. shortcuts
                Step(Txt.DesPassoAtalhos);
                DeleteFile(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    AppName + ".lnk"));

                string pastaMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
                if (Directory.Exists(pastaMenu))
                {
                    try { Directory.Delete(pastaMenu, true); _removed++; } catch { }
                }

                // 3. uninstall registry entry
                Step(Txt.DesPassoLista);
                try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); _removed++; }
                catch { }

                // 4. settings, if the user asked
                if (_optSettings.On)
                {
                    Step(Txt.DesPassoAjustes);
                    try
                    {
                        if (Directory.Exists(Settings.Folder)) { Directory.Delete(Settings.Folder, true); _removed++; }
                    }
                    catch { }
                }

                // 5. program files
                Step(Txt.DesPassoArquivos);
                foreach (var nome in new[] { "OrdoCadentia.exe",
                                             "LEIA-ME.txt", "README.txt", "LEEME.txt" })
                    DeleteFile(Path.Combine(_folder, nome));

                _phase = Phase.Done;
                RefreshPhase();

                // 6. The folder (and the uninstaller inside it) can only go
                //    after THIS process dies. Scheduling it now would be a race
                //    against the user: if they took their time closing the window, rmdir
                //    would fire with the executable still locked and the folder would stay.
                //    Main fires it instead, on the way out.
                FolderToClean = _folder;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _phase = Phase.Failed;
                RefreshPhase();
            }
        }

        void Step(string texto)
        {
            _tick = texto;
            Invalidate();
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);
        }

        void DeleteFile(string caminho)
        {
            try { if (File.Exists(caminho)) { File.Delete(caminho); _removed++; } }
            catch { }
        }

        /// <summary>
        /// Asks Windows to delete the folder after we exit. Tries three times,
        /// with growing waits: antivirus software or Explorer itself sometimes
        /// still hold the executable for a moment after the process dies.
        /// </summary>
        public static void ScheduleCleanup(string pasta)
        {
            try
            {
                if (string.IsNullOrEmpty(pasta)) return;
                pasta = pasta.TrimEnd('\\');
                if (!Directory.Exists(pasta)) return;

                // Safety catch: we only delete a folder that really is ours.
                if (!File.Exists(Path.Combine(pasta, "Desinstalar.exe"))) return;

                string comando = string.Format(
                    "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{0}\" 2>nul & " +
                    "if exist \"{0}\" ( ping 127.0.0.1 -n 4 >nul & rmdir /s /q \"{0}\" 2>nul ) & " +
                    "if exist \"{0}\" ( ping 127.0.0.1 -n 6 >nul & rmdir /s /q \"{0}\" 2>nul )",
                    pasta);

                Process.Start(new ProcessStartInfo("cmd.exe", comando)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
        }

        void RefreshPhase()
        {
            bool pronto = _phase == Phase.Ready;
            _optSettings.Visible = pronto;
            _btnCancel.Visible = pronto;

            _btnPrimary.Enabled = _phase != Phase.Removing;
            _btnPrimary.Danger = _phase == Phase.Done;
            _btnPrimary.Text = _phase == Phase.Done ? Txt.InsBtFechar
                                : (_phase == Phase.Failed ? Txt.InsBtTentarDeNovo
                                : (_phase == Phase.Removing ? Txt.DesBtRemovendo : Txt.DesBtDesinstalar));
            _btnPrimary.Subtext = pronto ? Txt.DesBtSub : "";
            Invalidate();
        }

        // ---------------------------------------------------------------- painting
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var tudo = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (var pincel = new SolidBrush(Theme.Pitch)) g.FillRectangle(pincel, tudo);

            using (var pincel = new LinearGradientBrush(
                       new Rectangle(0, 0, ClientSize.Width, Math.Max(1, _titleBar.Height)),
                       Color.FromArgb(255, 24, 15, 15), Theme.Pitch, 90f))
                g.FillRectangle(pincel, _titleBar);
            Theme.Text(g, Txt.DesTitulo, Theme.Tiny, Theme.Dust, Theme.S(12), Theme.S(11));
            Theme.TextCentered(g, "✕", Theme.Body, Theme.BoneDim, _closeBox);

            int s = Theme.S(76);
            Theme.Sigil(g, new RectangleF((ClientSize.Width - s) / 2f, Theme.S(50), s, s),
                        _phase == Phase.Done ? 0.1f : _pulse,
                        _phase == Phase.Done ? Theme.Dust : Theme.Crimson);

            Theme.TextCentered(g, Theme.Product, Theme.Title, Theme.Bone,
                new RectangleF(0, Theme.S(136), ClientSize.Width, Theme.S(26)));

            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            switch (_phase)
            {
                case Phase.Ready:
                    Theme.TextCentered(g, Txt.DesVaiEmbora, Theme.Body, Theme.BoneDim,
                        new RectangleF(m, Theme.S(178), larg, Theme.S(20)));

                    string[] itens =
                    {
                        Txt.DesItem1, Txt.DesItem2, Txt.DesItem3, Txt.DesItem4
                    };
                    float y = Theme.S(210);
                    foreach (var item in itens)
                    {
                        using (var pincel = new SolidBrush(Theme.Crimson))
                            g.FillRectangle(pincel, m + Theme.S(4), (int)y + Theme.S(6), Theme.S(4), Theme.S(4));
                        Theme.Text(g, item, Theme.Tiny, Theme.BoneDim, m + Theme.S(16), y);
                        y += Theme.S(22);
                    }

                    var caixa = new Rectangle(m, Theme.S(372), larg, Theme.S(30));
                    Theme.Card(g, caixa, Theme.Abyss, Theme.Line, 2);
                    using (var fmt = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisPath,
                        FormatFlags = StringFormatFlags.NoWrap
                    })
                    using (var pincel = new SolidBrush(Theme.Dust))
                        g.DrawString(_folder, Theme.Tiny, pincel,
                            new RectangleF(caixa.X + Theme.S(9), caixa.Y,
                                           caixa.Width - Theme.S(16), caixa.Height), fmt);
                    break;

                case Phase.Removing:
                    Theme.TextCentered(g, _tick, Theme.Body, Theme.Bone,
                        new RectangleF(m, Theme.S(210), larg, Theme.S(22)));
                    var trilho = new Rectangle(m + larg / 4, Theme.S(250), larg / 2, Theme.S(4));
                    using (var pincel = new SolidBrush(Theme.Panel)) g.FillRectangle(pincel, trilho);
                    for (int i = 0; i < 18; i++)
                    {
                        float f = ((_tick2 * 0.03f) + i / 18f) % 1f;
                        int alfa = (int)(40 + 200 * Math.Max(0, 1 - Math.Abs(f - 0.5) * 3));
                        using (var pincel = new SolidBrush(Color.FromArgb(alfa, Theme.CrimsonBright)))
                            g.FillRectangle(pincel, trilho.X + (int)(f * trilho.Width), trilho.Y,
                                            Theme.S(10), trilho.Height);
                    }
                    break;

                case Phase.Done:
                    Theme.TextCentered(g, Txt.DesRemovido, Theme.TitleSmall, Theme.BoneDim,
                        new RectangleF(m, Theme.S(196), larg, Theme.S(22)));
                    Theme.TextCentered(g, string.Format(Txt.DesRemovidoN, _removed),
                        Theme.Tiny, Theme.Dust,
                        new RectangleF(m, Theme.S(226), larg, Theme.S(18)));
                    Theme.TextCentered(g, Txt.DesNadaAlterado, Theme.Tiny, Theme.Dust,
                        new RectangleF(m, Theme.S(256), larg, Theme.S(18)));
                    if (!_optSettings.On)
                        Theme.TextCentered(g, Txt.DesAjustesMantidos, Theme.Tiny, Theme.Dust,
                            new RectangleF(m, Theme.S(280), larg, Theme.S(18)));
                    break;

                case Phase.Failed:
                    Theme.TextCentered(g, Txt.NaoDeuCerto, Theme.TitleSmall, Theme.CrimsonBright,
                        new RectangleF(m, Theme.S(196), larg, Theme.S(22)));
                    var cx = new Rectangle(m, Theme.S(230), larg, Theme.S(80));
                    Theme.Card(g, cx, Theme.Abyss, Color.FromArgb(120, Theme.Crimson), 2);
                    using (var fmt = new StringFormat { Trimming = StringTrimming.Word })
                    using (var pincel = new SolidBrush(Theme.BoneDim))
                        g.DrawString(_error, Theme.Tiny, pincel,
                            new RectangleF(cx.X + Theme.S(10), cx.Y + Theme.S(8),
                                           cx.Width - Theme.S(20), cx.Height - Theme.S(16)), fmt);
                    break;
            }

            Theme.Atmosphere(g, tudo, e.ClipRectangle);
            using (var caneta = new Pen(Color.FromArgb(255, 58, 26, 26)))
                g.DrawRectangle(caneta, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // ---------------------------------------------------------------- input
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            if (_closeBox.Contains(e.Location)) { Close(); return; }
            if (_titleBar.Contains(e.Location))
            {
                _dragging = true;
                _grab = e.Location;
                Capture = true;      // senao um arraste rapido solta a janela
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
                Location = new Point(Location.X + e.X - _grab.X, Location.Y + e.Y - _grab.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging) { _dragging = false; Capture = false; }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _phase != Phase.Removing) Close();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_phase == Phase.Removing) { e.Cancel = true; return; }
            if (_anim != null) { _anim.Stop(); _anim.Dispose(); }
            base.OnFormClosing(e);
        }
    }

    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(int valor);

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDpiAwareness(2); }
            catch { try { SetProcessDPIAware(); } catch { } }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                Theme.Init(g.DpiX / 96f);

            // Uninstall in the same language the program was installed in.
            Txt.LoadFromDisk(Settings.Folder);

            // Windows calls it this way when the user uninstalls from the Apps
            // panel and asks for no user interface.
            bool silencioso = false;
            foreach (var a in args)
                if (a.Equals("/silencioso", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("/quiet", StringComparison.OrdinalIgnoreCase))
                    silencioso = true;

            if (silencioso) { RemoveSilently(); return; }

            Application.Run(new UninstallerWindow());

            // The window has closed: now it is safe to schedule deletion of the folder
            // this executable lives in, because it exits right after.
            if (UninstallerWindow.FolderToClean != null)
                UninstallerWindow.ScheduleCleanup(UninstallerWindow.FolderToClean);
        }

        static void RemoveSilently()
        {
            const string AppName = "Ordo Cadentia";
            const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OrdoCadentia";
            try
            {
                Engine.CleanFullscreenLeftovers();

                string lnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
                if (File.Exists(lnk)) File.Delete(lnk);

                string menu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
                if (Directory.Exists(menu)) Directory.Delete(menu, true);

                Registry.CurrentUser.DeleteSubKeyTree(Chave, false);

                string pasta = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                if (File.Exists(Path.Combine(pasta, "Desinstalar.exe")))
                {
                    foreach (var nome in new[] { "OrdoCadentia.exe",
                                                 "LEIA-ME.txt", "README.txt", "LEEME.txt" })
                    {
                        string f = Path.Combine(pasta, nome);
                        if (File.Exists(f)) File.Delete(f);
                    }
                    UninstallerWindow.ScheduleCleanup(pasta);
                }
            }
            catch { }
        }
    }
}
