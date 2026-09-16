// MainWindow.cs — the program window.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace OrdoCadentia
{
    // =====================================================================
    //  TARGET CARD — shows the chosen window.
    // =====================================================================
    internal sealed class TargetCard : PaintedControl
    {
        public WindowInfo Info;
        public string Monitor = "";
        public bool TelaInteira;

        public TargetCard() { Height = 62; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            if (Info == null)
            {
                Theme.Card(g, r, Color.FromArgb(255, 17, 20, 18), Theme.Line, 3);
                using (var caneta = new Pen(Color.FromArgb(120, Theme.Crimson)) { DashStyle = DashStyle.Dash })
                using (var caminho = Theme.RoundedRect(Rectangle.Inflate(r, -3, -3), 2))
                    g.DrawPath(caneta, caminho);
                Theme.TextCentered(g, Txt.NenhumaJanela, Theme.Body, Theme.Dust,
                                 new RectangleF(0, 0, Width, Height));
                return;
            }

            Theme.Card(g, r, Theme.Panel, Color.FromArgb(150, Theme.Crimson), 3);
            using (var pincel = new SolidBrush(Theme.Crimson))
                g.FillRectangle(pincel, 0, 1, 3, Height - 3);

            using (var fmt = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                using (var pincel = new SolidBrush(Theme.Bone))
                    g.DrawString(Info.Process, Theme.BodyBold, pincel,
                                 new RectangleF(12, 7, Width - 24, 18), fmt);

                using (var pincel = new SolidBrush(Theme.BoneDim))
                    g.DrawString(Info.Title, Theme.Tiny, pincel,
                                 new RectangleF(12, 25, Width - 24, 16), fmt);

                string rodape = string.Format("PID {0}  ·  {1}×{2}{3}{4}",
                    Info.Pid, Info.Width, Info.Height,
                    string.IsNullOrEmpty(Monitor) ? "" : "  ·  " + Monitor.Replace(@"\\.\", ""),
                    TelaInteira ? Txt.TelaInteira : "");
                using (var pincel = new SolidBrush(Theme.Dust))
                    g.DrawString(rodape, Theme.Tiny, pincel,
                                 new RectangleF(12, 42, Width - 24, 16), fmt);
            }
        }
    }

    // =====================================================================
    //  TEXT CARD — fixed instructions.
    // =====================================================================
    internal sealed class TextCard : PaintedControl
    {
        public string[] Linhas = new string[0];
        public Color CorPrincipal = Theme.BoneDim;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.Card(g, r, Color.FromArgb(255, 17, 20, 18), Theme.Line, 3);

            float y = 9;
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
                foreach (var linha in Linhas)
                {
                    bool numerada = linha.Length > 1 && char.IsDigit(linha[0]);
                    var cor = numerada ? CorPrincipal : Theme.Dust;
                    if (numerada)
                    {
                        using (var pincel = new SolidBrush(Theme.Crimson))
                            g.DrawString(linha.Substring(0, 1), Theme.BodyBold, pincel, 11, y);
                        using (var pincel = new SolidBrush(cor))
                            g.DrawString(linha.Substring(1).TrimStart('.', ' '), Theme.Tiny, pincel,
                                         new RectangleF(24, y + 1, Width - 34, 17), fmt);
                    }
                    else
                    {
                        using (var pincel = new SolidBrush(cor))
                            g.DrawString(linha, Theme.Tiny, pincel,
                                         new RectangleF(11, y + 1, Width - 22, 17), fmt);
                    }
                    y += 18;
                }
        }
    }

    // =====================================================================
    //  THE WINDOW
    // =====================================================================
    internal sealed class MainWindow : Form
    {
        readonly Engine _engine = new Engine();

        // chrome
        Rectangle _titleBar, _closeBox, _minimizeBox;
        readonly Rectangle[] _languageBoxes = new Rectangle[3];
        static readonly Language[] LanguageOrder = { Language.Pt, Language.En, Language.Es };
        static readonly string[] LanguageCodes = { "PT", "EN", "ES" };
        int _hoverLanguage = -1;
        bool _dragging; Point _grab;
        float _pulse; int _tick;

        // controls
        TargetCard _targetCard;
        GlyphButton _btnPick, _btnAim, _btnClear, _btnLock;
        ChipSelector _chipsFps, _chipsFg, _chipsHz;
        PacingMeter _meter;
        LogPanel _log;
        PrimaryButton _btnActivate;
        TextCard _howTo;
        readonly List<Toggle> _toggles = new List<Toggle>();
        Toggle _tgSync, _tgTimer, _tgPriority, _tgCores, _tgFullscreen, _tgBackground;

        Timer _uiTimer;
        NotifyIcon _tray;
        string _footer = "";
        bool _aiming;

        public MainWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;

            // Fits 1080p comfortably, and shrinks itself on smaller screens instead
            // of opening with half the window off the display.
            var util = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(Theme.S(1060), util.Width - Theme.S(20)),
                                  Math.Min(Theme.S(860), util.Height - Theme.S(20)));

            BackColor = Theme.Pitch;
            Text = Theme.Product;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildControls();
            WireEngine();
            BuildTray();
            ApplyLanguage();
            LayOut();
            RefreshAll();

            _uiTimer = new Timer { Interval = 500 };
            _uiTimer.Tick += (s, e) => Pulse();
            _uiTimer.Start();

            string socorro;
            if (_engine.RecoverIfNeeded(out socorro))
                _log.Write(socorro, Theme.Amber);

            int sobras = Engine.CleanFullscreenLeftovers();
            if (sobras > 0)
                _log.Write(string.Format(Txt.HistLimpouSobras, sobras), Theme.Amber);

            _log.Write(string.Format(Txt.HistPronto, Theme.Product, Theme.Version), Theme.Gold);
            _log.Write(Cpu.Summary(), Theme.Dust);

            var taxas = _engine.DisplayRates;
            if (taxas.Count > 0)
                _log.Write(string.Format(Txt.HistMonitorAceita,
                    string.Join(", ", taxas.Select(t => t.ToString()).ToArray()), _engine.MaxHz),
                    Theme.Dust);

            if (!Native.IsUserAnAdmin())
                _log.Write(Txt.HistSemAdmin, Theme.Amber);
            if (SysTimer.GlobalEnabled())
                _log.Write(Txt.HistTempGlobal, Theme.Moss);

            MeasureRate();
        }

        // ---------------------------------------------------------------- construction
        void BuildControls()
        {
            _targetCard = new TargetCard();
            Controls.Add(_targetCard);

            _btnPick = new GlyphButton { Accent = Theme.CrimsonBright };
            _btnPick.Click += (s, e) => PickFromList();
            Controls.Add(_btnPick);

            _btnAim = new GlyphButton();
            _btnAim.Click += (s, e) => StartAiming();
            Controls.Add(_btnAim);

            _btnClear = new GlyphButton();
            _btnClear.Click += (s, e) => SetTarget(null);
            Controls.Add(_btnClear);

            _chipsFps = new ChipSelector();
            _chipsFps.Picked += (s, e) =>
            {
                var f = _chipsFps.SelectedChip;
                if (f != null) { _engine.Config.RenderedFps = f.Value; Save2(); RefreshAll(); }
            };
            Controls.Add(_chipsFps);

            _chipsFg = new ChipSelector();
            _chipsFg.Picked += (s, e) =>
            {
                var f = _chipsFg.SelectedChip;
                if (f != null) { _engine.Config.FgMultiplier = f.Value; Save2(); RefreshAll(); }
            };
            Controls.Add(_chipsFg);

            _chipsHz = new ChipSelector();
            _chipsHz.Picked += (s, e) =>
            {
                var f = _chipsHz.SelectedChip;
                if (f != null) { _engine.Config.ChosenHz = f.Value; Save2(); RefreshAll(); }
            };
            Controls.Add(_chipsHz);

            _btnLock = new GlyphButton { Accent = Theme.Moss };
            _btnLock.Click += (s, e) => ToggleLock();
            Controls.Add(_btnLock);

            _meter = new PacingMeter();
            Controls.Add(_meter);

            _log = new LogPanel();
            Controls.Add(_log);

            _howTo = new TextCard();
            Controls.Add(_howTo);

            _tgSync = NewToggle();
            _tgTimer = NewToggle();
            _tgPriority = NewToggle();
            _tgCores = NewToggle();
            _tgFullscreen = NewToggle();
            _tgBackground = NewToggle();

            _btnActivate = new PrimaryButton();
            _btnActivate.Click += (s, e) => ToggleActivation();
            Controls.Add(_btnActivate);
        }

        Toggle NewToggle()
        {
            var i = new Toggle { Height = Theme.S(45) };
            i.Toggled += (s, e) => { StoreSettings(); RefreshAll(); };
            _toggles.Add(i);
            Controls.Add(i);
            return i;
        }

        /// <summary>
        /// Fills in every fixed piece of text. Runs at build time and whenever the
        /// language changes — which is why controls start empty: having the text in two
        /// places would guarantee one of them lagging behind in a translation.
        /// </summary>
        void ApplyLanguage()
        {
            _btnPick.Text = Txt.BtEscolherJanela;
            _btnAim.Text = Txt.BtApontar;
            _btnClear.Text = Txt.BtSoltar;

            _howTo.Linhas = new[]
            {
                Txt.ComoUsar, Txt.Passo1, Txt.Passo2, Txt.Passo3, Txt.Passo4
            };

            _tgSync.Title = Txt.AjSincronia;
            _tgSync.Description = Txt.AjSincroniaDesc;
            _tgTimer.Title = Txt.AjTemporizador;
            _tgTimer.Description = Txt.AjTemporizadorDesc;
            _tgPriority.Title = Txt.AjPrioridade;
            _tgPriority.Description = Txt.AjPrioridadeDesc;
            _tgCores.Title = Txt.AjNucleos;
            _tgCores.Description = Txt.AjNucleosDesc;
            _tgFullscreen.Title = Txt.AjTelaCheia;
            _tgFullscreen.Description = Txt.AjTelaCheiaDesc;
            _tgBackground.Title = Txt.AjFundo;
            _tgBackground.Description = Txt.AjFundoDesc;

            if (_tray != null && _tray.ContextMenuStrip != null)
            {
                var m = _tray.ContextMenuStrip.Items;
                if (m.Count >= 4)
                {
                    m[0].Text = Txt.BandejaMostrar;
                    m[1].Text = Txt.BandejaDesativar;
                    m[3].Text = Txt.BandejaSair;
                }
            }

            // The chips carry translated text; force them to rebuild.
            _chipsFps.Set(new Chip[0], -1);
            _chipsFg.Set(new Chip[0], -1);

            foreach (Control c in Controls) c.Invalidate();
            Invalidate();
        }

        void SwitchLanguage(Language novo)
        {
            if (Txt.Current == novo) return;
            Txt.Current = novo;
            _engine.Config.Language = Txt.Code;
            Save2();
            ApplyLanguage();
            LayOut();
            RefreshAll();
        }

        void WireEngine()
        {
            _engine.Logged += (texto, cor) => _log.Write(texto, cor);
            _engine.Changed += () =>
            {
                if (IsHandleCreated) BeginInvoke((MethodInvoker)RefreshAll);
            };
        }

        void BuildTray()
        {
            _tray = new NotifyIcon { Text = Theme.Product, Visible = false };
            try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var menu = new ContextMenuStrip();
            menu.Items.Add("", null, (s, e) => Restore());
            menu.Items.Add("", null, (s, e) =>
            {
                if (_engine.Active) _engine.Deactivate(Txt.MotPelaBandeja);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("", null, (s, e) => Close());
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (s, e) => Restore();
        }

        void Restore()
        {
            Show();
            WindowState = FormWindowState.Normal;
            _tray.Visible = false;
            Activate();
        }

        // ---------------------------------------------------------------- layout
        int _yFrames, _yLblFps, _yLblFg, _yRate, _yRateInfo, _yLockNote, _yHowTo;
        int _yBeforeAfter, _yApply, _yLog;
        Rectangle _resultBox;

        void LayOut()
        {
            int m = Theme.S(14);
            int topo = Theme.S(38);
            int largEsq = Theme.S(440);
            int xEsq = m;
            int xDir = xEsq + largEsq + Theme.S(14);
            int largDir = ClientSize.Width - xDir - m;
            int fimCorpo = ClientSize.Height - Theme.S(90);

            _titleBar = new Rectangle(0, 0, ClientSize.Width, topo);
            int bt = Theme.S(30);
            _closeBox = new Rectangle(ClientSize.Width - bt - Theme.S(6), Theme.S(4), bt, Theme.S(26));
            _minimizeBox = new Rectangle(_closeBox.Left - bt, Theme.S(4), bt, Theme.S(26));

            // Language picker in the title bar: anyone who installed in Portuguese and
            // wants English does not have to reinstall.
            int li = Theme.S(26), la = Theme.S(19);
            int lx = _minimizeBox.Left - Theme.S(12) - li * 3;
            int ly = (topo - la) / 2;
            for (int i = 0; i < 3; i++)
                _languageBoxes[i] = new Rectangle(lx + li * i, ly, li, la);

            // ---- left column ----
            int y = topo + Theme.S(16);
            y += Theme.S(22);
            _targetCard.Bounds = new Rectangle(xEsq, y, largEsq, Theme.S(62));
            y += Theme.S(62) + Theme.S(6);

            int lb = (largEsq - Theme.S(12)) / 3;
            _btnPick.Bounds = new Rectangle(xEsq, y, lb + Theme.S(24), Theme.S(30));
            _btnAim.Bounds = new Rectangle(_btnPick.Right + Theme.S(6), y, lb - Theme.S(18), Theme.S(30));
            _btnClear.Bounds = new Rectangle(_btnAim.Right + Theme.S(6), y,
                                              xEsq + largEsq - (_btnAim.Right + Theme.S(6)), Theme.S(30));
            y += Theme.S(30) + Theme.S(18);

            _yFrames = y; y += Theme.S(22);
            _yLblFps = y; y += Theme.S(15);
            _chipsFps.Bounds = new Rectangle(xEsq, y, largEsq, Theme.S(44));
            _chipsFps.Relayout(); y += _chipsFps.Height + Theme.S(9);

            _yLblFg = y; y += Theme.S(15);
            _chipsFg.Bounds = new Rectangle(xEsq, y, largEsq, Theme.S(44));
            _chipsFg.Relayout(); y += _chipsFg.Height + Theme.S(9);

            _resultBox = new Rectangle(xEsq, y, largEsq, Theme.S(42));
            y += Theme.S(42) + Theme.S(18);

            _yRate = y; y += Theme.S(22);
            _yRateInfo = y; y += Theme.S(17);
            _chipsHz.Bounds = new Rectangle(xEsq, y, largEsq, Theme.S(44));
            _chipsHz.Relayout(); y += _chipsHz.Height + Theme.S(7);

            _btnLock.Bounds = new Rectangle(xEsq, y, largEsq, Theme.S(32));
            y += Theme.S(32) + Theme.S(7);
            _yLockNote = y; y += Theme.S(18) + Theme.S(12);

            // instructions anchored to the foot of the column, tall enough for every step
            int alturaInstrucoes = Theme.S(18) * _howTo.Linhas.Length + Theme.S(20);
            _yHowTo = Math.Max(y, fimCorpo - alturaInstrucoes);
            _howTo.Bounds = new Rectangle(xEsq, _yHowTo, largEsq,
                                               Math.Max(Theme.S(40), fimCorpo - _yHowTo));

            // ---- right column ----
            int yd = topo + Theme.S(16);
            _yBeforeAfter = yd; yd += Theme.S(22);
            _meter.Bounds = new Rectangle(xDir, yd, largDir, Theme.S(162));
            yd += Theme.S(162) + Theme.S(14);

            _yApply = yd; yd += Theme.S(22);
            foreach (var aj in _toggles)
            {
                aj.Bounds = new Rectangle(xDir, yd, largDir, Theme.S(45));
                yd += Theme.S(45) + Theme.S(5);
            }
            yd += Theme.S(12);

            _yLog = yd; yd += Theme.S(22);
            _log.Bounds = new Rectangle(xDir, yd, largDir, Math.Max(Theme.S(50), fimCorpo - yd));

            // ---- footer ----
            _btnActivate.Bounds = new Rectangle(m, ClientSize.Height - Theme.S(72), Theme.S(300), Theme.S(58));
        }

        // ---------------------------------------------------------------- state
        void Save2() { _engine.Config.Save(); }

        void StoreSettings()
        {
            var c = _engine.Config;
            c.SyncDisplay = _tgSync.On;
            c.SharpenTimer = _tgTimer.On;
            c.HighPriority = _tgPriority.On;
            c.PinPerformanceCores = _tgCores.On;
            c.DisableFullscreenOpt = _tgFullscreen.On;
            c.QuietBackground = _tgBackground.On;
            Save2();
        }

        static readonly int[] FpsPossiveis = { 24, 30, 36, 40, 45, 48, 60, 72, 90, 120 };

        void RefreshAll()
        {
            var c = _engine.Config;
            bool ativo = _engine.Active;
            bool travada = _engine.RateLocked;
            bool temAlvo = _engine.CurrentTarget != null;

            // --- rendered frames ---
            if (_chipsFps.Chips.Count != FpsPossiveis.Length)
                _chipsFps.Set(FpsPossiveis.Select(v => new Chip
                {
                    Value = v,
                    Label = v.ToString(),
                    Note = v == 30 ? Txt.FichaConsole : (v == 60 ? Txt.FichaPadrao : ""),
                    NoteColor = Theme.Dust
                }), 1);
            _chipsFps.SelectValue(c.RenderedFps);

            // --- frame generation ---
            if (_chipsFg.Chips.Count == 0)
                _chipsFg.Set(new[]
                {
                    new Chip { Value = 1, Label = Txt.FichaNao, Note = Txt.FichaSemFg },
                    new Chip { Value = 2, Label = "2×",  Note = "DLSS 3 / FSR 3" },
                    new Chip { Value = 3, Label = "3×",  Note = "DLSS 4" },
                    new Chip { Value = 4, Label = "4×",  Note = "DLSS 4" }
                }, 0);
            _chipsFg.SelectValue(c.FgMultiplier);

            // --- display rates (result of the scan) ---
            var taxas = _engine.DisplayRates;
            int auto = Display.BestRate(taxas, c.FpsOnScreen);
            var fichasHz = new List<Chip>
            {
                new Chip
                {
                    Value = 0, Label = Txt.FichaAuto,
                    Note = auto > 0 ? auto + " Hz" : "—",
                    NoteColor = Theme.Gold, Highlight = true
                }
            };
            foreach (int hz in taxas)
            {
                var cad = Cadence.Analyze(hz, c.FpsOnScreen);
                fichasHz.Add(new Chip
                {
                    Value = hz,
                    Label = hz.ToString(),
                    Note = cad.Perfect ? Txt.FichaExato : (cad.Score >= 45 ? Txt.FichaTremor : Txt.FichaRuim),
                    NoteColor = Theme.ScoreColor(cad.Score)
                });
            }
            bool mudouLista = _chipsHz.Chips.Count != fichasHz.Count ||
                              !_chipsHz.Chips.Select(f => f.Value).SequenceEqual(fichasHz.Select(f => f.Value));
            int selecionado = fichasHz.FindIndex(f => f.Value == c.ChosenHz);
            if (selecionado < 0) { selecionado = 0; c.ChosenHz = 0; }
            _chipsHz.Set(fichasHz, selecionado);
            if (mudouLista) LayOut();

            // --- lock button ---
            int hzParaTravar = c.ChosenHz > 0 ? c.ChosenHz : auto;
            _btnLock.Text = travada
                ? string.Format(Txt.BtDestravar, _engine.LockedHz)
                : (hzParaTravar > 0 ? string.Format(Txt.BtTravarEm, hzParaTravar)
                                    : Txt.BtTravar);
            _btnLock.Accent = travada ? Theme.Amber : Theme.Moss;
            _btnLock.Enabled = travada || hzParaTravar > 0;

            // --- adjustments ---
            _tgSync.On = c.SyncDisplay;
            _tgTimer.On = c.SharpenTimer;
            _tgPriority.On = c.HighPriority;
            _tgCores.On = c.PinPerformanceCores;
            _tgFullscreen.On = c.DisableFullscreenOpt;
            _tgBackground.On = c.QuietBackground;

            _tgCores.Unavailable = !Cpu.IsHybrid;
            _tgCores.UnavailableReason = Txt.SemNucleosE;

            bool semExe = temAlvo && string.IsNullOrEmpty(_engine.CurrentTarget.ExePath);
            _tgFullscreen.Unavailable = semExe;
            _tgFullscreen.UnavailableReason = Txt.ExeDesconhecido;

            foreach (var aj in _toggles)
            {
                aj.State = ativo && aj.On && !aj.Unavailable ? Txt.EstadoAplicado : "";
                aj.StateColor = Theme.Moss;
            }
            if (ativo && _tgFullscreen.On && !_tgFullscreen.Unavailable)
            {
                _tgFullscreen.State = Txt.EstadoProxima;
                _tgFullscreen.StateColor = Theme.Amber;
            }
            if (ativo && _tgTimer.On && SysTimer.ProcessScopedOnly)
            {
                _tgTimer.State = Txt.EstadoParcial;
                _tgTimer.StateColor = Theme.Amber;
            }
            if (ativo && _tgSync.On && travada)
            {
                _tgSync.State = Txt.EstadoTravado;
                _tgSync.StateColor = Theme.Amber;
            }

            // --- chosen window ---
            _targetCard.Info = _engine.CurrentTarget;
            if (temAlvo)
            {
                _targetCard.Monitor = Display.DeviceForWindow(_engine.CurrentTarget.Hwnd) ?? "";
                _targetCard.TelaInteira = Target.FillsMonitor(_engine.CurrentTarget);
            }
            _targetCard.Invalidate();

            // --- before and after ---
            if (ativo)
            {
                _meter.Before = Cadence.Analyze(_hzBefore, c.FpsOnScreen);
                _meter.After = _engine.CurrentPacing;
                _meter.LabelBefore = Txt.MedAntes;
                _meter.LabelAfter = Txt.MedAgora;
            }
            else
            {
                _meter.Before = _engine.CurrentPacing;
                _meter.After = _engine.PredictedPacing;
                _meter.LabelBefore = Txt.MedAgora;
                _meter.LabelAfter = Txt.MedDepoisDeAtivar;
            }
            _meter.Invalidate();

            // --- primary button and footer ---
            _btnActivate.Enabled = temAlvo;
            _btnActivate.Danger = ativo;
            _btnActivate.Text = ativo ? Txt.BtDesativar : Txt.BtAtivar;
            _btnActivate.Subtext = !temAlvo ? Txt.SubEscolhaJanela
                                : (ativo ? Txt.SubDesfaz : Txt.SubAplica);
            _btnActivate.Invalidate();

            var prev = _engine.PredictedPacing;
            _footer = !temAlvo
                ? (travada ? string.Format(Txt.RodapeTravado, _engine.LockedHz)
                           : Txt.RodapeEscolha)
                : string.Format(Txt.RodapeResumo,
                    c.FpsOnScreen,
                    (ativo || travada) ? _engine.CurrentPacing.Hz : _engine.TargetHz,
                    prev.Verdict.ToLowerInvariant());

            _btnPick.Enabled = !ativo;
            _btnAim.Enabled = !ativo;
            _btnClear.Enabled = !ativo && temAlvo;
            _chipsFps.Enabled = !ativo;
            _chipsFg.Enabled = !ativo;
            _chipsHz.Enabled = !ativo;
            foreach (var aj in _toggles) aj.Enabled = !ativo;

            Invalidate();
        }

        int _hzBefore;

        // ---------------------------------------------------------------- actions
        void ToggleLock()
        {
            if (_engine.RateLocked) { _engine.Unlock(); RefreshAll(); return; }

            var c = _engine.Config;
            int hz = c.ChosenHz > 0
                ? c.ChosenHz
                : Display.BestRate(_engine.DisplayRates, c.FpsOnScreen);
            if (hz <= 0) return;

            string erro;
            _engine.Lock(hz, out erro);
            RefreshAll();
        }

        void ToggleActivation()
        {
            if (_engine.Active) { _engine.Deactivate(null); return; }

            var m = _engine.CurrentMode2;
            _hzBefore = m != null ? m.Hz : 0;

            var prev = _engine.PredictedPacing;
            var c = _engine.Config;

            if (c.SyncDisplay && !_engine.RateLocked && !prev.Perfect && _engine.TargetHz > 0)
                _log.Write(string.Format(Txt.AvisoNaoDivide,
                    _engine.TargetHz, c.FpsOnScreen), Theme.Amber);

            if (_engine.RateLocked && !prev.Perfect)
                _log.Write(string.Format(Txt.AvisoTravadoNaoDivide,
                    _engine.LockedHz, c.FpsOnScreen), Theme.Amber);

            if (c.FgMultiplier > 1 && c.RenderedFps <= 30)
                _log.Write(Txt.AvisoFgBaixo, Theme.Amber);

            int hzFinal = _engine.RateLocked ? _engine.LockedHz : _engine.TargetHz;
            if (hzFinal > 0 && hzFinal < c.FpsOnScreen)
                _log.Write(string.Format(Txt.AvisoHzMenor,
                    hzFinal, c.FpsOnScreen), Theme.CrimsonBright);

            _engine.Activate();

            if (_engine.Active)
                _log.Write(Txt.HistPodeJogar, Theme.Gold);
        }

        void PickFromList()
        {
            using (var d = new WindowPicker())
                if (d.ShowDialog(this) == DialogResult.OK && d.Picked2 != null)
                    SetTarget(d.Picked2);
        }

        void StartAiming()
        {
            _aiming = true;
            Cursor = Cursors.Cross;
            Capture = true;
            _log.Write(Txt.MiraInstrucao, Theme.Gold);
            Invalidate();
        }

        void EndAiming(bool pegar)
        {
            if (!_aiming) return;
            _aiming = false;
            Capture = false;
            Cursor = Cursors.Default;

            if (pegar)
            {
                Native.POINT ponto;
                bool souEu = false;
                if (Native.GetCursorPos(out ponto))
                {
                    IntPtr sob = Native.GetAncestor(Native.WindowFromPoint(ponto), Native.GA_ROOT);
                    souEu = (sob == Handle);
                }

                var j = Target.UnderCursor();
                if (j != null) SetTarget(j);
                else if (souEu)
                    _log.Write(Txt.MiraSouEu, Theme.Amber);
                else
                    _log.Write(Txt.MiraNada, Theme.Amber);
            }
            else _log.Write(Txt.MiraCancelada, Theme.Dust);
            Invalidate();
        }

        void SetTarget(WindowInfo j)
        {
            _engine.CurrentTarget = j;
            if (j != null)
            {
                _log.Write(string.Format(Txt.HistJanelaEscolhida, j.Process, j.Pid), Theme.Bone);
                if (!string.IsNullOrEmpty(j.ExePath))
                    _log.Write("   " + j.ExePath, Theme.Dust);
                MeasureRate();
            }
            else _log.Write(Txt.HistJanelaSolta, Theme.Dust);
            RefreshAll();
        }

        double _measuredHz;

        void MeasureRate()
        {
            // DwmFlush bloqueia; medir fora da thread da interface.
            var t = new System.Threading.Thread(() =>
            {
                double hz = Display.MeasureRealRate();
                if (hz <= 0) return;
                try
                {
                    if (IsHandleCreated)
                        BeginInvoke((MethodInvoker)(() => { _measuredHz = hz; Invalidate(); }));
                }
                catch (InvalidOperationException) { }
            });
            t.IsBackground = true;
            t.Start();
        }

        void Pulse()
        {
            _tick++;
            _pulse = (float)((Math.Sin(_tick * 0.22) + 1) / 2);
            _engine.Watch();
            if (_tick % 6 == 0 && !_engine.Active) RefreshAll();
            Invalidate(new Rectangle(0, 0, Theme.S(420), Theme.S(38)));
            Invalidate(new Rectangle(0, ClientSize.Height - Theme.S(90), ClientSize.Width, Theme.S(90)));
        }

        // ---------------------------------------------------------------- painting
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var tudo = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (var pincel = new SolidBrush(Theme.Pitch)) g.FillRectangle(pincel, tudo);

            DrawTitleBar(g);

            int m = Theme.S(14);
            int largEsq = Theme.S(440);
            int xDir = m + largEsq + Theme.S(14);
            int largDir = ClientSize.Width - xDir - m;

            int yCabecalho = Theme.S(38) + Theme.S(16) - Theme.S(2);
            Theme.Header(g, new Rectangle(m, yCabecalho, largEsq, 20), "1.", Txt.SecJanelaJogo);
            Theme.Header(g, new Rectangle(m, _yFrames, largEsq, 20), "2.", Txt.SecQuadros);
            Theme.Header(g, new Rectangle(m, _yRate, largEsq, 20), "3.", Txt.SecTaxa);
            Theme.Header(g, new Rectangle(xDir, yCabecalho, largDir, 20), "", Txt.SecAntesDepois);
            Theme.Header(g, new Rectangle(xDir, _yApply, largDir, 20), "", Txt.SecAplicar);
            Theme.Header(g, new Rectangle(xDir, _yLog, largDir, 20), "", Txt.SecHistorico);

            Theme.Text(g, Txt.RotFps, Theme.Tiny, Theme.Dust, m, _yLblFps);
            Theme.Text(g, Txt.RotFg, Theme.Tiny, Theme.Dust, m, _yLblFg);

            DrawRateInfo(g, m, largEsq);

            Theme.Text(g, _engine.RateLocked ? Txt.NotaTravaLigada : Txt.NotaTravaDesligada,
                Theme.Tiny, Theme.Dust, m + Theme.S(2), _yLockNote);
            DrawResult(g);
            DrawFooter(g);

            if (_aiming) DrawAimOverlay(g, tudo);

            Theme.Atmosphere(g, tudo, e.ClipRectangle);

            using (var caneta = new Pen(Color.FromArgb(255, 58, 26, 26)))
                g.DrawRectangle(caneta, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        void DrawRateInfo(Graphics g, int x, int larg)
        {
            int min = _engine.MinHz, max = _engine.MaxHz;
            var modo = _engine.CurrentMode2;

            string esquerda = (min > 0 && max > 0)
                ? (min == max ? string.Format(Txt.MonitorAceitaUm, max)
                              : string.Format(Txt.MonitorAceitaFaixa, min, max))
                : Txt.MonitorSemVarredura;
            Theme.Text(g, esquerda, Theme.Tiny, Theme.Dust, x, _yRateInfo);

            if (modo == null) return;

            string direita = _engine.RateLocked
                ? string.Format(Txt.TravadoEm, _engine.LockedHz)
                : string.Format(Txt.AgoraEm, modo.Hz);
            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var pincel = new SolidBrush(_engine.RateLocked ? Theme.Amber : Theme.BoneDim))
                g.DrawString(direita, Theme.Tiny, pincel,
                             new RectangleF(x, _yRateInfo, larg, Theme.S(16)), fmt);
        }

        void DrawTitleBar(Graphics g)
        {
            var r = _titleBar;
            using (var pincel = new LinearGradientBrush(
                       new Rectangle(r.X, r.Y, r.Width, Math.Max(1, r.Height)),
                       Color.FromArgb(255, 24, 15, 15), Theme.Pitch, 90f))
                g.FillRectangle(pincel, r);
            using (var caneta = new Pen(Color.FromArgb(255, 52, 26, 26)))
                g.DrawLine(caneta, 0, r.Bottom - 1, r.Width, r.Bottom - 1);

            int s = Theme.S(26);
            Theme.Sigil(g, new RectangleF(Theme.S(9), (r.Height - s) / 2f, s, s), _pulse,
                        _engine.Active ? Theme.Ember : Theme.Crimson, false);

            float x = Theme.S(44);
            Theme.Text(g, Theme.Product, Theme.TitleSmall, Theme.Bone, x, (r.Height - Theme.S(17)) / 2f);
            SizeF t = g.MeasureString(Theme.Product, Theme.TitleSmall);

            Theme.Text(g, "·  " + Theme.Subtitle, Theme.Tiny, Theme.Dust,
                       x + t.Width + Theme.S(8), (r.Height - Theme.S(13)) / 2f);

            string aviso = _engine.Active ? Txt.TituloAtivo
                         : (_engine.RateLocked ? Txt.TituloTravado : null);
            if (aviso != null)
            {
                SizeF ta = g.MeasureString(aviso, Theme.Tiny);
                float xa = _languageBoxes[0].Left - ta.Width - Theme.S(14);
                Color cor = _engine.Active ? Theme.Ember : Theme.Amber;
                using (var pincel = new SolidBrush(Color.FromArgb((int)(140 + 115 * _pulse), cor)))
                    g.DrawString(aviso, Theme.Tiny, pincel, xa, (r.Height - ta.Height) / 2f);
            }

            DrawLanguages(g);
            DrawChromeButton(g, _minimizeBox, "—");
            DrawChromeButton(g, _closeBox, "✕");
        }

        void DrawLanguages(Graphics g)
        {
            for (int i = 0; i < 3; i++)
            {
                var r = _languageBoxes[i];
                bool sel = LanguageOrder[i] == Txt.Current;
                bool sobre = _hoverLanguage == i;

                if (sel)
                {
                    using (var pincel = new SolidBrush(Color.FromArgb(255, 58, 20, 22)))
                        g.FillRectangle(pincel, r);
                    using (var caneta = new Pen(Color.FromArgb(190, Theme.CrimsonBright)))
                        g.DrawRectangle(caneta, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
                else if (sobre)
                {
                    using (var pincel = new SolidBrush(Theme.PanelHi))
                        g.FillRectangle(pincel, r);
                }

                Theme.TextCentered(g, LanguageCodes[i], Theme.Tiny,
                    sel ? Theme.Bone : (sobre ? Theme.BoneDim : Theme.Dust), r);
            }
        }

        Rectangle _hoverChrome = Rectangle.Empty;

        void DrawChromeButton(Graphics g, Rectangle r, string glifo)
        {
            bool sobre = _hoverChrome == r;
            if (sobre)
                using (var pincel = new SolidBrush(r == _closeBox
                           ? Color.FromArgb(255, 122, 22, 28) : Theme.PanelHi))
                    g.FillRectangle(pincel, r);
            Theme.TextCentered(g, glifo, Theme.Body, sobre ? Theme.Bone : Theme.BoneDim, r);
        }

        void DrawResult(Graphics g)
        {
            var c = _engine.Config;
            var r = _resultBox;
            var prev = _engine.PredictedPacing;
            Color cor = Theme.ScoreColor(prev.Score);

            Theme.Card(g, r, Color.FromArgb(255, 17, 20, 18), Color.FromArgb(110, cor), 3);
            using (var pincel = new SolidBrush(cor))
                g.FillRectangle(pincel, r.X, r.Y + 1, Theme.S(3), r.Height - 2);

            var faixa = new RectangleF(0, r.Y, 0, r.Height);
            float x = r.X + Theme.S(12);

            Action<string, Font, Color> escrever = (texto, fonte, tinta) =>
            {
                SizeF t = g.MeasureString(texto, fonte);
                faixa.X = x; faixa.Width = t.Width;
                using (var pincel = new SolidBrush(tinta))
                using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center })
                    g.DrawString(texto, fonte, pincel, faixa, fmt);
                x += t.Width;
            };

            if (c.FgMultiplier > 1)
                escrever(string.Format("{0} × {1}  =  ", c.RenderedFps, c.FgMultiplier),
                         Theme.Body, Theme.BoneDim);

            escrever(c.FpsOnScreen.ToString(), Theme.DataMid, Theme.Bone);
            escrever(Txt.QuadrosNaTela, Theme.Tiny, Theme.BoneDim);

            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var pincel = new SolidBrush(cor))
                g.DrawString(prev.Verdict, Theme.Tiny, pincel,
                    new RectangleF(x + Theme.S(8), r.Y, r.Right - x - Theme.S(18), r.Height), fmt);
        }

        void DrawFooter(Graphics g)
        {
            int y = ClientSize.Height - Theme.S(90);
            using (var caneta = new Pen(Color.FromArgb(255, 42, 24, 24)))
                g.DrawLine(caneta, Theme.S(14), y, ClientSize.Width - Theme.S(14), y);

            float x = _btnActivate.Right + Theme.S(20);
            float larg = ClientSize.Width - Theme.S(14) - x;

            Theme.Text(g, _footer, Theme.Body,
                       _engine.Active ? Theme.Bone : Theme.BoneDim, x, y + Theme.S(20));

            var linhas = new List<string>();
            var modo = _engine.CurrentMode2;
            if (modo != null)
            {
                string med = _measuredHz > 0 ? string.Format(Txt.RodapeMedido, _measuredHz) : "";
                linhas.Add(string.Format(Txt.RodapeMonitor, modo, med));
            }
            double mn, mx, at;
            SysTimer.Query(out mn, out mx, out at);
            linhas.Add(string.Format(Txt.RodapeTemporizador, at));

            using (var fmt = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var pincel = new SolidBrush(Theme.Dust))
                g.DrawString(string.Join("      ", linhas.ToArray()), Theme.Tiny, pincel,
                             new RectangleF(x, y + Theme.S(44), larg, Theme.S(18)), fmt);
        }

        void DrawAimOverlay(Graphics g, Rectangle tudo)
        {
            using (var pincel = new SolidBrush(Color.FromArgb(190, 6, 8, 7)))
                g.FillRectangle(pincel, tudo);
            Theme.TextCentered(g, Txt.MiraTitulo, Theme.Title, Theme.Bone,
                new RectangleF(0, tudo.Height / 2f - Theme.S(30), tudo.Width, Theme.S(30)));
            Theme.TextCentered(g, Txt.MiraCancela, Theme.Body, Theme.Dust,
                new RectangleF(0, tudo.Height / 2f + Theme.S(4), tudo.Width, Theme.S(20)));
        }

        // ---------------------------------------------------------------- input
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_aiming) { EndAiming(e.Button == MouseButtons.Left); return; }
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            if (_closeBox.Contains(e.Location)) { Close(); return; }
            if (_minimizeBox.Contains(e.Location)) { MinimizeWindow(); return; }

            for (int i = 0; i < 3; i++)
                if (_languageBoxes[i].Contains(e.Location)) { SwitchLanguage(LanguageOrder[i]); return; }
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
            {
                Location = new Point(Location.X + e.X - _grab.X, Location.Y + e.Y - _grab.Y);
                return;
            }

            var antes = _hoverChrome;
            _hoverChrome = _closeBox.Contains(e.Location) ? _closeBox
                          : (_minimizeBox.Contains(e.Location) ? _minimizeBox : Rectangle.Empty);

            int antesIdioma = _hoverLanguage;
            _hoverLanguage = -1;
            for (int i = 0; i < 3; i++)
                if (_languageBoxes[i].Contains(e.Location)) { _hoverLanguage = i; break; }

            if (antes != _hoverChrome || antesIdioma != _hoverLanguage) Invalidate(_titleBar);

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging) { _dragging = false; Capture = false; }
            base.OnMouseUp(e);
        }

        /// <summary>
        /// If Windows takes mouse capture away from us (alt-tab, another window
        /// stealing focus), aiming would hang waiting for a click that never
        /// arrives. Here it cancels itself.
        /// </summary>
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (_aiming && !Capture) EndAiming(false);
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Esc cancels aiming and nothing else. Deactivating has a visible consequence
            // (the screen blinks): it has to be the button, which is written on screen.
            if (e.KeyCode == Keys.Escape && _aiming)
            {
                EndAiming(false);
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.F5) { RefreshAll(); MeasureRate(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void MinimizeWindow()
        {
            // Only goes to the tray when something is active that has to stay
            // alive. Otherwise it minimizes like any other program.
            bool algoValendo = _engine.Active || _engine.RateLocked;
            if (algoValendo && _engine.Config.MinimizeToTray)
            {
                _tray.Visible = true;
                Hide();
                try
                {
                    _tray.ShowBalloonTip(3000, Theme.Product,
                        _engine.Active ? Txt.BandejaAtivo : Txt.BandejaTravado,
                        ToolTipIcon.None);
                }
                catch { }
            }
            else WindowState = FormWindowState.Minimized;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _engine.UndoEverything();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool descartando)
        {
            if (descartando && _uiTimer != null) { _uiTimer.Stop(); _uiTimer.Dispose(); }
            base.Dispose(descartando);
        }
    }
}
