// Installer.cs — the Ordo Cadentia installer.
// Installs into %LOCALAPPDATA%\Programs: no UAC prompt, and no decision to make
// beyond the language. One screen, one button.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using OrdoCadentia;

namespace Installer
{
    internal sealed class InstallerWindow : Form
    {
        enum Phase { Ready, Installing, Done, Failed }

        const string AppName = "Ordo Cadentia";
        const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OrdoCadentia";

        /// <summary>Where the program keeps its settings. Defined here because the
        /// installer does not compile the Engine, yet has to write the language.</summary>
        static string SettingsFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrdoCadentia");
            }
        }

        // All three instruction files travel inside the installer and all three are
        // written out, so switching language later never leaves the wrong text behind.
        static readonly string[] InstructionFiles = { "LEIA-ME.txt", "README.txt", "LEEME.txt" };

        Phase _phase = Phase.Ready;
        string _dest;
        string _error = "";
        string _tick = "";

        Rectangle _titleBar, _closeBox;
        bool _dragging; Point _grab;
        float _pulse; int _tick2;
        Timer _anim;

        PrimaryButton _btnPrimary;
        GlyphButton _btnChange, _btnReadMe, _btnClose;
        Toggle _optDesktop, _optStartMenu, _optInstructions;
        ChipSelector _chipsLanguage;

        static readonly Language[] LanguageOrder = { Language.Pt, Language.En, Language.Es };

        public InstallerWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(660), Theme.S(620));
            BackColor = Theme.Pitch;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _dest = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", AppName);

            _titleBar = new Rectangle(0, 0, ClientSize.Width, Theme.S(36));
            _closeBox = new Rectangle(ClientSize.Width - Theme.S(36), Theme.S(4), Theme.S(30), Theme.S(26));

            Build();
            ApplyLanguage();
            LayOut();
            RefreshPhase();

            _anim = new Timer { Interval = 60 };
            _anim.Tick += (s, e) =>
            {
                _tick2++;
                _pulse = (float)((Math.Sin(_tick2 * 0.10) + 1) / 2);
                Invalidate(new Rectangle(0, Theme.S(40), ClientSize.Width, Theme.S(110)));
                if (_phase == Phase.Installing)
                    Invalidate(new Rectangle(0, Theme.S(240), ClientSize.Width, Theme.S(140)));
            };
            _anim.Start();
        }

        void Build()
        {
            _chipsLanguage = new ChipSelector { ChipHeight = Theme.S(34) };
            _chipsLanguage.Picked += (s, e) =>
            {
                var f = _chipsLanguage.SelectedChip;
                if (f == null || LanguageOrder[f.Value] == Txt.Current) return;
                Txt.Current = LanguageOrder[f.Value];
                ApplyLanguage();
                LayOut();
                RefreshPhase();
            };
            Controls.Add(_chipsLanguage);

            _btnChange = new GlyphButton();
            _btnChange.Click += (s, e) => PickFolder();
            Controls.Add(_btnChange);

            _optDesktop = NewOption();
            _optStartMenu = NewOption();
            _optInstructions = NewOption();

            _btnPrimary = new PrimaryButton();
            _btnPrimary.Click += (s, e) => PrimaryButton();
            Controls.Add(_btnPrimary);

            _btnReadMe = new GlyphButton { Visible = false };
            _btnReadMe.Click += (s, e) => OpenInstructions();
            Controls.Add(_btnReadMe);

            _btnClose = new GlyphButton { Visible = false };
            _btnClose.Click += (s, e) => Close();
            Controls.Add(_btnClose);
        }

        Toggle NewOption()
        {
            var i = new Toggle { On = true, Height = Theme.S(44) };
            Controls.Add(i);
            return i;
        }

        /// <summary>Fills in all fixed text. Runs at build time and on every language switch.</summary>
        void ApplyLanguage()
        {
            Text = Txt.InsTitulo + " " + AppName;

            // The chips always name each language in that language.
            int sel = Array.IndexOf(LanguageOrder, Txt.Current);
            _chipsLanguage.Set(new[]
            {
                new Chip { Value = 0, Label = "PORTUGUÊS", Note = "Brasil" },
                new Chip { Value = 1, Label = "ENGLISH",   Note = "English" },
                new Chip { Value = 2, Label = "ESPAÑOL",   Note = "Español" }
            }, sel < 0 ? 0 : sel);

            _btnChange.Text = Txt.InsMudar;
            _optDesktop.Title = Txt.InsOptArea;
            _optDesktop.Description = Txt.InsOptAreaDesc;
            _optStartMenu.Title = Txt.InsOptMenu;
            _optStartMenu.Description = Txt.InsOptMenuDesc;
            _optInstructions.Title = Txt.InsOptLeiaMe;
            _optInstructions.Description = Txt.InsOptLeiaMeDesc;
            _btnReadMe.Text = Txt.InsBtLerLeiaMe;
            _btnClose.Text = Txt.InsBtFechar;

            foreach (Control c in Controls) c.Invalidate();
            Invalidate();
        }

        void LayOut()
        {
            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            // Three chips sharing the full width, with the selector's own 5 px gaps
            // between them. Left to its default minimum they would huddle on the
            // left and leave a dead strip on the right.
            _chipsLanguage.MinWidth = (larg - 2 * 5) / 3;
            _chipsLanguage.Bounds = new Rectangle(m, Theme.S(202), larg, Theme.S(34));
            _chipsLanguage.Relayout();

            int yCaixa = Theme.S(292);
            _btnChange.Bounds = new Rectangle(ClientSize.Width - m - Theme.S(88), yCaixa,
                                             Theme.S(88), Theme.S(30));
            int y = yCaixa + Theme.S(46);

            foreach (var opcao in new[] { _optDesktop, _optStartMenu, _optInstructions })
            {
                opcao.Bounds = new Rectangle(m, y, larg, Theme.S(44));
                y += Theme.S(44) + Theme.S(6);
            }

            _btnPrimary.Bounds = new Rectangle(m, ClientSize.Height - Theme.S(92), larg, Theme.S(62));
            _btnReadMe.Bounds = new Rectangle(m, ClientSize.Height - Theme.S(92),
                                              larg / 2 - Theme.S(6), Theme.S(40));
            _btnClose.Bounds = new Rectangle(m + larg / 2 + Theme.S(6), ClientSize.Height - Theme.S(92),
                                              larg / 2 - Theme.S(6), Theme.S(40));
        }

        void PickFolder()
        {
            using (var d = new FolderBrowserDialog
            {
                Description = Txt.InsEscolherPasta,
                SelectedPath = Directory.Exists(_dest)
                    ? _dest
                    : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    // If the user points at a generic folder, create the program's own subfolder
                    // inside it — nobody wants loose files on their Desktop.
                    string escolhido = d.SelectedPath;
                    _dest = Path.GetFileName(escolhido.TrimEnd('\\')).Equals(AppName,
                                   StringComparison.OrdinalIgnoreCase)
                             ? escolhido
                             : Path.Combine(escolhido, AppName);
                    Invalidate();
                }
        }

        void PrimaryButton()
        {
            if (_phase == Phase.Done) { OpenProgram(); return; }
            if (_phase == Phase.Failed) { _phase = Phase.Ready; RefreshPhase(); return; }
            if (_phase == Phase.Installing) return;
            Install();
        }

        // ---------------------------------------------------------------- install
        void Install()
        {
            if (AlreadyRunning())
            {
                MessageBox.Show(this, Txt.InsJaAberto, Txt.InsTitulo + " " + AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _phase = Phase.Installing;
            RefreshPhase();
            Application.DoEvents();

            try
            {
                Step(Txt.InsPassoPasta);
                Directory.CreateDirectory(_dest);

                Step(Txt.InsPassoGravando);
                Extract("OrdoCadentia.exe", Path.Combine(_dest, "OrdoCadentia.exe"));
                Extract("Desinstalar.exe", Path.Combine(_dest, "Desinstalar.exe"));
                foreach (var doc in InstructionFiles)
                    Extract(doc, Path.Combine(_dest, doc));

                // The language chosen here is the one the program will open in.
                Txt.SaveToDisk(SettingsFolder, Txt.Current);

                string exe = Path.Combine(_dest, "OrdoCadentia.exe");

                if (_optDesktop.On)
                {
                    Step(Txt.InsPassoAtalhoArea);
                    CreateShortcut(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                     AppName + ".lnk"),
                        exe, _dest, Txt.InsDescricaoAtalho);
                }

                if (_optStartMenu.On)
                {
                    Step(Txt.InsPassoAtalhoMenu);
                    string pasta = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
                    Directory.CreateDirectory(pasta);
                    CreateShortcut(Path.Combine(pasta, AppName + ".lnk"), exe, _dest,
                                Txt.InsDescricaoAtalho);
                    CreateShortcut(Path.Combine(pasta, Txt.DesTitulo + " " + AppName + ".lnk"),
                                Path.Combine(_dest, "Desinstalar.exe"), _dest,
                                Txt.InsDescricaoDesinstalar);
                    CreateShortcut(Path.Combine(pasta, Txt.InsAtalhoInstrucoes + ".lnk"),
                                Path.Combine(_dest, Txt.InstructionFile), _dest,
                                Txt.InsOptLeiaMeDesc);
                }

                Step(Txt.InsPassoRegistrando);
                RegisterInWindows(exe);

                Step(Txt.InsPassoPronto);
                _phase = Phase.Done;
                RefreshPhase();

                if (_optInstructions.On) OpenInstructions();
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
            System.Threading.Thread.Sleep(180);   // um instante para o passo ser legivel
        }

        static bool AlreadyRunning()
        {
            try { return Process.GetProcessesByName("OrdoCadentia").Length > 0; }
            catch { return false; }
        }

        static void Extract(string recurso, string destino)
        {
            var asm = Assembly.GetExecutingAssembly();
            string nome = asm.GetManifestResourceNames()
                             .FirstOrDefault(n => n.EndsWith(recurso, StringComparison.OrdinalIgnoreCase));
            if (nome == null)
                throw new FileNotFoundException("recurso ausente no instalador: " + recurso);

            using (var origem = asm.GetManifestResourceStream(nome))
            using (var saida = new FileStream(destino, FileMode.Create, FileAccess.Write))
                origem.CopyTo(saida);
        }

        /// <summary>Creates a .lnk through WScript.Shell, with no extra libraries.</summary>
        static void CreateShortcut(string caminhoLnk, string alvo, string pastaTrabalho, string descricao)
        {
            Type tipo = Type.GetTypeFromProgID("WScript.Shell");
            if (tipo == null) return;

            object shell = Activator.CreateInstance(tipo);
            try
            {
                object lnk = tipo.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                                               null, shell, new object[] { caminhoLnk });
                Type tl = lnk.GetType();
                tl.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { alvo });
                tl.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk,
                                new object[] { pastaTrabalho });
                tl.InvokeMember("Description", BindingFlags.SetProperty, null, lnk,
                                new object[] { descricao });
                if (alvo.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    tl.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk,
                                    new object[] { alvo + ",0" });
                tl.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            finally
            {
                if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }

        void RegisterInWindows(string exe)
        {
            long bytes = 0;
            try
            {
                foreach (var f in Directory.GetFiles(_dest)) bytes += new FileInfo(f).Length;
            }
            catch { }

            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                if (k == null) return;
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Theme.Version);
                k.SetValue("Publisher", AppName);
                k.SetValue("DisplayIcon", exe);
                k.SetValue("InstallLocation", _dest);
                k.SetValue("UninstallString", "\"" + Path.Combine(_dest, "Desinstalar.exe") + "\"");
                k.SetValue("QuietUninstallString",
                           "\"" + Path.Combine(_dest, "Desinstalar.exe") + "\" /silencioso");
                k.SetValue("EstimatedSize", (int)Math.Max(1, bytes / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        void OpenProgram()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_dest, "OrdoCadentia.exe"))
                {
                    UseShellExecute = true,
                    WorkingDirectory = _dest
                });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Txt.InsNaoAbriu, ex.Message), AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenInstructions()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_dest, Txt.InstructionFile))
                { UseShellExecute = true });
            }
            catch { }
        }

        void RefreshPhase()
        {
            bool pronto = _phase == Phase.Ready;
            bool concluido = _phase == Phase.Done;

            _chipsLanguage.Visible = pronto;
            _btnChange.Visible = pronto;
            _optDesktop.Visible = _optStartMenu.Visible = _optInstructions.Visible = pronto;

            _btnPrimary.Visible = !concluido;
            _btnPrimary.Enabled = _phase != Phase.Installing;
            _btnPrimary.Text = _phase == Phase.Failed ? Txt.InsBtTentarDeNovo
                                : (_phase == Phase.Installing ? Txt.InsBtInstalando : Txt.InsBtInstalar);
            _btnPrimary.Subtext = "";

            _btnReadMe.Visible = concluido;
            _btnClose.Visible = concluido;
            if (concluido)
            {
                _btnPrimary.Visible = true;
                _btnPrimary.Text = Txt.InsBtAbrir;
                _btnPrimary.Enabled = true;
                _btnPrimary.Bounds = new Rectangle(Theme.S(26), ClientSize.Height - Theme.S(150),
                                                     ClientSize.Width - Theme.S(52), Theme.S(54));
            }

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
            Theme.Text(g, Txt.InsTitulo, Theme.Tiny, Theme.Dust, Theme.S(12), Theme.S(11));
            Theme.TextCentered(g, "✕", Theme.Body, Theme.BoneDim, _closeBox);

            int s = Theme.S(76);
            Theme.Sigil(g, new RectangleF((ClientSize.Width - s) / 2f, Theme.S(46), s, s),
                        _pulse, _phase == Phase.Done ? Theme.Moss : Theme.Crimson);

            Theme.TextCentered(g, Theme.Product, Theme.Title, Theme.Bone,
                new RectangleF(0, Theme.S(132), ClientSize.Width, Theme.S(26)));
            Theme.TextCentered(g, Theme.Subtitle + "  ·  v" + Theme.Version, Theme.Tiny, Theme.Dust,
                new RectangleF(0, Theme.S(158), ClientSize.Width, Theme.S(18)));

            switch (_phase)
            {
                case Phase.Ready: PaintReady(g); break;
                case Phase.Installing: PaintInstalling(g); break;
                case Phase.Done: PaintDone(g); break;
                case Phase.Failed: PaintFailed(g); break;
            }

            Theme.Atmosphere(g, tudo, e.ClipRectangle);
            using (var caneta = new Pen(Color.FromArgb(255, 58, 26, 26)))
                g.DrawRectangle(caneta, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        void PaintReady(Graphics g)
        {
            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            Theme.Text(g, Txt.InsIdioma, Theme.Tiny, Theme.Dust, m, Theme.S(186));

            Theme.TextCentered(g, Txt.InsResumo, Theme.Body, Theme.BoneDim,
                new RectangleF(m, Theme.S(248), larg, Theme.S(20)));

            Theme.Text(g, Txt.InsOndeInstalar, Theme.Tiny, Theme.Dust, m, Theme.S(276));

            var caixa = new Rectangle(m, Theme.S(292), larg - Theme.S(96), Theme.S(30));
            Theme.Card(g, caixa, Theme.Abyss, Theme.Line, 2);
            using (var fmt = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisPath,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var pincel = new SolidBrush(Theme.BoneDim))
                g.DrawString(_dest, Theme.Tiny, pincel,
                    new RectangleF(caixa.X + Theme.S(9), caixa.Y, caixa.Width - Theme.S(16), caixa.Height), fmt);

            Theme.TextCentered(g, Txt.InsRodape, Theme.Tiny, Theme.Dust,
                new RectangleF(m, ClientSize.Height - Theme.S(116), larg, Theme.S(16)));
        }

        void PaintInstalling(Graphics g)
        {
            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            Theme.TextCentered(g, _tick, Theme.Body, Theme.Bone,
                new RectangleF(m, Theme.S(280), larg, Theme.S(22)));

            var trilho = new Rectangle(m + larg / 4, Theme.S(320), larg / 2, Theme.S(4));
            using (var pincel = new SolidBrush(Theme.Panel)) g.FillRectangle(pincel, trilho);
            int n = 18;
            for (int i = 0; i < n; i++)
            {
                float f = ((_tick2 * 0.03f) + i / (float)n) % 1f;
                int alfa = (int)(40 + 200 * Math.Max(0, 1 - Math.Abs(f - 0.5) * 3));
                using (var pincel = new SolidBrush(Color.FromArgb(alfa, Theme.CrimsonBright)))
                    g.FillRectangle(pincel, trilho.X + (int)(f * trilho.Width), trilho.Y,
                                    Theme.S(10), trilho.Height);
            }
        }

        void PaintDone(Graphics g)
        {
            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            Theme.TextCentered(g, Txt.InsInstalado, Theme.TitleSmall, Theme.Moss,
                new RectangleF(m, Theme.S(206), larg, Theme.S(22)));

            var caixa = new Rectangle(m, Theme.S(240), larg, Theme.S(34));
            Theme.Card(g, caixa, Theme.Abyss, Theme.Line, 2);
            using (var fmt = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisPath,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var pincel = new SolidBrush(Theme.BoneDim))
                g.DrawString(_dest, Theme.Tiny, pincel, caixa, fmt);

            string[] proximos =
            {
                Txt.InsProximoPasso, Txt.InsProx1, Txt.InsProx2, Txt.InsProx3,
                "", Txt.InsProxLeiaMe
            };

            float y = Theme.S(296);
            foreach (var linha in proximos)
            {
                bool num = linha.Length > 1 && char.IsDigit(linha[0]);
                if (num)
                {
                    Theme.Text(g, linha.Substring(0, 1), Theme.BodyBold, Theme.Crimson, m + Theme.S(2), y);
                    Theme.Text(g, linha.Substring(1).TrimStart('.', ' '), Theme.Tiny, Theme.BoneDim,
                               m + Theme.S(16), y + Theme.S(1));
                }
                else if (linha.Length > 0)
                    Theme.Text(g, linha, Theme.Tiny, Theme.Dust, m, y);
                y += Theme.S(19);
            }
        }

        void PaintFailed(Graphics g)
        {
            int m = Theme.S(26);
            int larg = ClientSize.Width - m * 2;

            Theme.TextCentered(g, Txt.InsNaoDeuCerto, Theme.TitleSmall, Theme.CrimsonBright,
                new RectangleF(m, Theme.S(210), larg, Theme.S(22)));

            var caixa = new Rectangle(m, Theme.S(244), larg, Theme.S(90));
            Theme.Card(g, caixa, Theme.Abyss, Color.FromArgb(120, Theme.Crimson), 2);
            using (var fmt = new StringFormat { Trimming = StringTrimming.Word })
            using (var pincel = new SolidBrush(Theme.BoneDim))
                g.DrawString(_error, Theme.Tiny, pincel,
                    new RectangleF(caixa.X + Theme.S(10), caixa.Y + Theme.S(8),
                                   caixa.Width - Theme.S(20), caixa.Height - Theme.S(16)), fmt);

            Theme.TextCentered(g, Txt.InsErroDica, Theme.Tiny, Theme.Dust,
                new RectangleF(m, Theme.S(344), larg, Theme.S(18)));
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
            if (e.KeyCode == Keys.Escape && _phase != Phase.Installing) Close();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_phase == Phase.Installing) { e.Cancel = true; return; }
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
        static void Main()
        {
            try { SetProcessDpiAwareness(2); }
            catch { try { SetProcessDPIAware(); } catch { } }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                Theme.Init(g.DpiX / 96f);

            // Starts in the Windows language; anyone wanting another switches on the first screen.
            Txt.Current = Txt.Detect();

            Application.Run(new InstallerWindow());
        }
    }
}
