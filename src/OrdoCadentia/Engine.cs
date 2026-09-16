// Engine.cs — orchestration, persistence and watchdogs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OrdoCadentia
{
    // =====================================================================
    //  SETTINGS — everything the user chooses, persisted between sessions.
    // =====================================================================
    internal sealed class Settings
    {
        public int RenderedFps = 30;      // o que o jogo desenha
        public int FgMultiplier = 1;      // 1 = sem geracao de quadros; 2, 3, 4 = DLSS/FSR/AFMF
        public int ChosenHz = 0;          // 0 = automatico

        public bool SyncDisplay = true;
        public bool SharpenTimer = true;
        public bool HighPriority = true;
        public bool PinPerformanceCores = true;
        public bool DisableFullscreenOpt = false;
        public bool QuietBackground = false;

        public string Language = Txt.Code;   // "pt", "en" ou "es"
        public bool FollowFocus = false;      // devolver o Hz ao alternar de janela
        public bool MinimizeToTray = true;

        /// <summary>Frames that actually reach the screen — what the pacing must match.</summary>
        public int FpsOnScreen { get { return Math.Max(1, RenderedFps * Math.Max(1, FgMultiplier)); } }

        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrdoCadentia");
            }
        }

        static string SettingsFile { get { return Path.Combine(Folder, "settings.ini"); } }

        public static Settings Load()
        {
            var a = new Settings();
            try
            {
                if (!File.Exists(SettingsFile)) return a;
                foreach (var linha in File.ReadAllLines(SettingsFile, Encoding.UTF8))
                {
                    int i = linha.IndexOf('=');
                    if (i <= 0) continue;
                    string chave = linha.Substring(0, i).Trim();
                    string valor = linha.Substring(i + 1).Trim();
                    switch (chave)
                    {
                        case "RenderedFps": a.RenderedFps = ParseInt(valor, a.RenderedFps); break;
                        case "FgMultiplier": a.FgMultiplier = ParseInt(valor, a.FgMultiplier); break;
                        case "ChosenHz": a.ChosenHz = ParseInt(valor, a.ChosenHz); break;
                        case "SyncDisplay": a.SyncDisplay = ParseBool(valor, a.SyncDisplay); break;
                        case "SharpenTimer": a.SharpenTimer = ParseBool(valor, a.SharpenTimer); break;
                        case "HighPriority": a.HighPriority = ParseBool(valor, a.HighPriority); break;
                        case "PinPerformanceCores": a.PinPerformanceCores = ParseBool(valor, a.PinPerformanceCores); break;
                        case "DisableFullscreenOpt": a.DisableFullscreenOpt = ParseBool(valor, a.DisableFullscreenOpt); break;
                        case "QuietBackground": a.QuietBackground = ParseBool(valor, a.QuietBackground); break;
                        case "Language": a.Language = valor; break;
                        case "FollowFocus": a.FollowFocus = ParseBool(valor, a.FollowFocus); break;
                        case "MinimizeToTray": a.MinimizeToTray = ParseBool(valor, a.MinimizeToTray); break;
                    }
                }
            }
            catch { }

            // sanity: out-of-range values fall back to the default
            if (a.RenderedFps < 10 || a.RenderedFps > 480) a.RenderedFps = 30;
            if (a.FgMultiplier < 1 || a.FgMultiplier > 4) a.FgMultiplier = 1;
            if (a.ChosenHz < 0 || a.ChosenHz > 1000) a.ChosenHz = 0;
            return a;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var sb = new StringBuilder();
                sb.AppendLine("; Ordo Cadentia — ajustes. Apague este arquivo para voltar ao padrao.");
                sb.AppendLine("RenderedFps=" + RenderedFps);
                sb.AppendLine("FgMultiplier=" + FgMultiplier);
                sb.AppendLine("ChosenHz=" + ChosenHz);
                sb.AppendLine("SyncDisplay=" + SyncDisplay);
                sb.AppendLine("SharpenTimer=" + SharpenTimer);
                sb.AppendLine("HighPriority=" + HighPriority);
                sb.AppendLine("PinPerformanceCores=" + PinPerformanceCores);
                sb.AppendLine("DisableFullscreenOpt=" + DisableFullscreenOpt);
                sb.AppendLine("QuietBackground=" + QuietBackground);
                sb.AppendLine("Language=" + Language);
                sb.AppendLine("FollowFocus=" + FollowFocus);
                sb.AppendLine("MinimizeToTray=" + MinimizeToTray);
                File.WriteAllText(SettingsFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        static int ParseInt(string s, int padrao)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : padrao;
        }

        static bool ParseBool(string s, bool padrao)
        {
            bool v;
            return bool.TryParse(s, out v) ? v : padrao;
        }
    }

    // =====================================================================
    //  ENGINE — applies the adjustments, watches the target, and undoes it all on exit.
    // =====================================================================
    internal sealed class Engine
    {
        public Settings Config = Settings.Load();
        public WindowInfo CurrentTarget;
        public bool Active { get; private set; }

        public event Action<string, Color> Logged;
        public event Action Changed;

        // ---------------------------------------------------------------------
        //  DISPLAY REFRESH RATE — one owner, on purpose.
        //
        //  Two things want to change the rate: the LOCK button (a manual choice) and
        //  the automatic sync when a game is activated. If each kept its own idea of
        //  the "original value", one unlucky order of clicks would be enough to leave
        //  the display stuck on a rate nobody asked for. That is why
        //  EVERY change goes through ChangeRate/RestoreRate: the original is recorded
        //  exactly once, and _manualLock says who is in charge.
        // ---------------------------------------------------------------------
        string _rateDevice;
        int _originalHz, _rateWidth, _rateHeight;
        bool _rateChanged;     // nos mudamos a taxa e devemos desfazer
        int _restoreTries;        // quantas vezes o vigia repos a taxa seguidas
        bool _manualLock;      // quem mandou foi o botao, nao a ativacao do jogo

        // --- state needed to undo ---
        uint? _originalPriority;
        UIntPtr? _originalAffinity;
        bool _fullscreenFlagSet;
        string _fullscreenExe;
        readonly List<KeyValuePair<int, ProcessPriorityClass>> _quieted =
            new List<KeyValuePair<int, ProcessPriorityClass>>();

        // Background programs that tend to steal CPU while you play. They are never
        // closed — only lowered, and restored to normal at the end.
        static readonly string[] NoisyPrograms =
        {
            "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi",
            "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "Battle.net",
            "RiotClientServices", "Spotify", "Teams", "ms-teams", "slack",
            "OneDrive", "Dropbox", "GoogleDriveFS", "SearchApp", "Widgets",
            "PhoneExperienceHost", "YourPhone", "Adobe Desktop Service", "CCXProcess"
        };

        void Log(string texto, Color cor) { if (Logged != null) Logged(texto, cor); }
        void Notify() { if (Changed != null) Changed(); }

        // -------------------------------------------------------------- reading
        public string Device
        {
            get
            {
                return Display.DeviceForWindow(CurrentTarget != null ? CurrentTarget.Hwnd : IntPtr.Zero);
            }
        }

        public VideoMode CurrentMode2 { get { return Display.CurrentMode(Device); } }

        /// <summary>Display scan: the rates on offer, from 60 Hz to the maximum.</summary>
        public List<int> DisplayRates
        {
            get
            {
                var m = CurrentMode2;
                if (m == null) return new List<int>();
                return Display.UsableRates(Display.AvailableRates(Device, m.Width, m.Height));
            }
        }

        /// <summary>Highest rate this display accepts at the current resolution.</summary>
        public int MaxHz
        {
            get
            {
                var t = DisplayRates;
                return t.Count > 0 ? t[t.Count - 1] : 0;
            }
        }

        /// <summary>Lowest rate on offer (normally 60).</summary>
        public int MinHz
        {
            get
            {
                var t = DisplayRates;
                return t.Count > 0 ? t[0] : 0;
            }
        }

        /// <summary>The Hz to aim for: the hand-picked one, or the best automatic choice.</summary>
        public int TargetHz
        {
            get
            {
                if (Config.ChosenHz > 0) return Config.ChosenHz;
                return Display.BestRate(DisplayRates, Config.FpsOnScreen);
            }
        }

        public Cadence CurrentPacing
        {
            get
            {
                var m = CurrentMode2;
                return Cadence.Analyze(m != null ? m.Hz : 0, Config.FpsOnScreen);
            }
        }

        /// <summary>
        /// The pacing that will ACTUALLY be delivered. If SYNC THE DISPLAY is
        /// unchecked the refresh rate will not change — and promising the ideal rate here
        /// would be lying to the user on the main screen.
        /// </summary>
        public Cadence PredictedPacing
        {
            get
            {
                // Locked by hand, or with sync unchecked: nothing is going to change.
                if (RateLocked || !Config.SyncDisplay) return CurrentPacing;
                return Cadence.Analyze(TargetHz, Config.FpsOnScreen);
            }
        }

        // -------------------------------------------------------------- display refresh rate
        /// <summary>The rate is held by the user's manual choice.</summary>
        public bool RateLocked { get { return _rateChanged && _manualLock; } }

        /// <summary>Rate the display was locked at (0 when there is no lock).</summary>
        public int LockedHz { get; private set; }

        /// <summary>Rate the display had before we touched it.</summary>
        public int OriginalHz { get { return _rateChanged ? _originalHz : 0; } }

        /// <summary>
        /// The only path that changes the rate. Records the original mode the first time
        /// and leaves a recovery note on disk for as long as the rate is not the original.
        /// </summary>
        bool ChangeRate(int hz, bool manual, out string erro)
        {
            erro = null;
            if (hz <= 0) { erro = Txt.TaxaInvalida; return false; }

            string disp = _rateChanged ? _rateDevice : Device;
            var m = Display.CurrentMode(disp);
            if (m == null) { erro = Txt.NaoLiMonitorCurto; return false; }

            bool primeira = !_rateChanged;
            if (primeira)
            {
                _rateDevice = disp;
                _originalHz = m.Hz;
                _rateWidth = m.Width;
                _rateHeight = m.Height;
                WriteRecovery(_rateDevice, _rateWidth, _rateHeight, _originalHz);
            }

            if (m.Hz != hz)
            {
                if (!Display.SetRate(_rateDevice, _rateWidth, _rateHeight, hz, out erro))
                {
                    if (primeira) ClearRecovery();
                    return false;
                }
            }

            // Takes ownership even when the rate was already the requested one: that is what
            // lets the watchdog restore it if something changes it underneath later.
            _rateChanged = true;
            if (manual) { _manualLock = true; LockedHz = hz; }
            return true;
        }

        /// <summary>Puts the display back to exactly the mode it had.</summary>
        void RestoreRate()
        {
            if (!_rateChanged) return;

            Display.Restore(_rateDevice);
            var agora = Display.CurrentMode(_rateDevice);
            if (agora == null || agora.Hz != _originalHz)
            {
                string erro;
                Display.SetRate(_rateDevice, _rateWidth, _rateHeight, _originalHz, out erro);
            }

            _rateChanged = false;
            _manualLock = false;
            LockedHz = 0;
            _restoreTries = 0;
            ClearRecovery();
        }

        /// <summary>Holds the display at a rate until the user releases it.</summary>
        public bool Lock(int hz, out string erro)
        {
            var m = Display.CurrentMode(_rateChanged ? _rateDevice : Device);
            int hzAntes = m != null ? m.Hz : 0;

            if (!ChangeRate(hz, true, out erro))
            {
                Log(string.Format(Txt.MotNaoTravou, hz, erro), Theme.CrimsonBright);
                Notify();
                return false;
            }

            Log(hzAntes == hz
                ? string.Format(Txt.MotTravado, hz)
                : string.Format(Txt.MotTravadoDe, hzAntes, hz), Theme.Moss);
            Log(Txt.MotTravaEnquantoAberto, Theme.Dust);
            Notify();
            return true;
        }

        /// <summary>Releases the rate. If a game is active, hands control back to it.</summary>
        public void Unlock()
        {
            if (!RateLocked) return;

            int volta = _originalHz;
            RestoreRate();
            Log(string.Format(Txt.MotDestravado, volta), Theme.BoneDim);

            if (Active && Config.SyncDisplay) ApplySync();
            Notify();
        }

        // -------------------------------------------------------------- apply
        public void Activate()
        {
            if (Active) return;
            if (CurrentTarget == null) { Log(Txt.MotNenhumaJanela, Theme.Amber); return; }
            if (!Target.IsAlive(CurrentTarget)) { Log(Txt.MotJanelaSumiu, Theme.Amber); return; }

            Log(string.Format(Txt.MotAtivado, CurrentTarget.Process), Theme.Gold);
            Log(string.Format(Txt.MotAlvo,
                Config.RenderedFps, Config.FgMultiplier, Config.FpsOnScreen), Theme.BoneDim);

            if (Config.SyncDisplay) ApplySync();
            if (Config.SharpenTimer) ApplyTimer();
            if (Config.HighPriority) ApplyPriority();
            if (Config.PinPerformanceCores) ApplyCores();
            if (Config.DisableFullscreenOpt) ApplyFullscreenFlag();
            if (Config.QuietBackground) ApplyQuietBackground();

            Active = true;
            Notify();
        }

        void ApplySync()
        {
            if (RateLocked)
            {
                var trava = Cadence.Analyze(LockedHz, Config.FpsOnScreen);
                Log(string.Format(Txt.MotTelaMantidaTravada,
                    LockedHz, trava.Verdict.ToLowerInvariant()),
                    trava.Perfect ? Theme.Moss : Theme.Amber);
                return;
            }

            int alvo = TargetHz;
            if (alvo <= 0) { Log(Txt.MotSemTaxa, Theme.Amber); return; }

            var m = Display.CurrentMode(Device);
            int hzAntes = m != null ? m.Hz : 0;

            string erro;
            if (!ChangeRate(alvo, false, out erro))
            {
                Log(string.Format(Txt.MotNaoMudou, alvo, erro), Theme.CrimsonBright);
                return;
            }

            var nova = Cadence.Analyze(alvo, Config.FpsOnScreen);
            Log(hzAntes == alvo
                ? string.Format(Txt.MotJaEstava, alvo, nova.Verdict.ToLowerInvariant())
                : string.Format(Txt.MotTrocou,
                                hzAntes, alvo, nova.Verdict.ToLowerInvariant()),
                nova.Perfect ? Theme.Moss : Theme.Amber);

            if (nova.Perfect)
                Log(string.Format(Txt.MotQuadroDura, nova.MinMs),
                    Theme.Dust);
        }

        void ApplyTimer()
        {
            double global; string erro;
            if (!SysTimer.Raise(out global, out erro))
            {
                Log(string.Format(Txt.MotTempFalhou, erro), Theme.CrimsonBright);
                return;
            }

            Log(string.Format(Txt.MotTempPedido,
                SysTimer.RequestedMs, global), Theme.Moss);

            if (SysTimer.ProcessScopedOnly)
                Log(string.Format(Txt.MotTempRestrito, Txt.InstructionFile),
                    Theme.Amber);
        }

        void ApplyPriority()
        {
            try
            {
                using (var p = Process.GetProcessById((int)CurrentTarget.Pid))
                {
                    _originalPriority = (uint)p.PriorityClass;
                    p.PriorityClass = ProcessPriorityClass.High;
                    Log(string.Format(Txt.MotPrioridade,
                        DescribePriority((ProcessPriorityClass)_originalPriority.Value)), Theme.Moss);
                }
            }
            catch (Exception ex)
            {
                _originalPriority = null;
                Log(string.Format(Txt.MotPrioridadeFalhou, ShortError(ex)), Theme.CrimsonBright);
            }
        }

        void ApplyCores()
        {
            UIntPtr mascaraP; int lp, le;
            if (!Cpu.Map(out mascaraP, out lp, out le))
            {
                Log(Txt.MotNucleosNada, Theme.Dust);
                return;
            }

            try
            {
                using (var p = Process.GetProcessById((int)CurrentTarget.Pid))
                {
                    _originalAffinity = (UIntPtr)(ulong)(long)p.ProcessorAffinity;
                    p.ProcessorAffinity = (IntPtr)(long)mascaraP.ToUInt64();
                    Log(string.Format(Txt.MotNucleosFixado,
                        lp, Cpu.MaskToText(mascaraP)), Theme.Moss);
                    Log(Txt.MotNucleosPorque, Theme.Dust);
                }
            }
            catch (Exception ex)
            {
                _originalAffinity = null;
                Log(string.Format(Txt.MotNucleosFalhou, ShortError(ex)), Theme.CrimsonBright);
            }
        }

        void ApplyFullscreenFlag()
        {
            if (string.IsNullOrEmpty(CurrentTarget.ExePath))
            {
                Log(Txt.MotTcSemCaminho, Theme.Amber);
                return;
            }
            if (Compat.IsOn(CurrentTarget.ExePath))
            {
                Log(Txt.MotTcJaDesligadas, Theme.Dust);
                _fullscreenFlagSet = false;
                return;
            }

            string erro;
            if (Compat.Set(CurrentTarget.ExePath, true, out erro))
            {
                _fullscreenFlagSet = true;
                _fullscreenExe = CurrentTarget.ExePath;
                NoteFullscreenFlag(_fullscreenExe, true);
                Log(string.Format(Txt.MotTcDesligadas, Path.GetFileName(_fullscreenExe)), Theme.Moss);
                Log(Txt.MotTcProxima, Theme.Amber);
            }
            else Log(string.Format(Txt.MotTcFalhou, erro), Theme.CrimsonBright);
        }

        void ApplyQuietBackground()
        {
            int n = 0;
            int meuPid = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == meuPid) continue;
                    if (CurrentTarget != null && p.Id == (int)CurrentTarget.Pid) continue;
                    if (!NoisyPrograms.Any(b => string.Equals(b, p.ProcessName, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var antes = p.PriorityClass;
                    if (antes == ProcessPriorityClass.BelowNormal || antes == ProcessPriorityClass.Idle)
                        continue;

                    p.PriorityClass = ProcessPriorityClass.BelowNormal;
                    _quieted.Add(new KeyValuePair<int, ProcessPriorityClass>(p.Id, antes));
                    n++;
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }

            if (n > 0) Log(string.Format(Txt.MotFundoRebaixados, n), Theme.Moss);
            else Log(Txt.MotFundoNada, Theme.Dust);
        }

        // -------------------------------------------------------------- deactivate
        public void Deactivate(string motivo)
        {
            if (!Active) return;
            Active = false;

            // The rate only reverts if the game activation is what changed it. A manual
            // lock survives: the user asked for that rate, not the game.
            if (_rateChanged && !_manualLock)
            {
                int volta = _originalHz;
                RestoreRate();
                Log(string.Format(Txt.MotTelaDevolvidaA, volta), Theme.BoneDim);
            }
            else if (RateLocked)
                Log(string.Format(Txt.MotContinuaTravada, LockedHz), Theme.Dust);

            SysTimer.Restore();

            if (_originalPriority.HasValue)
            {
                try
                {
                    using (var p = Process.GetProcessById((int)CurrentTarget.Pid))
                        p.PriorityClass = (ProcessPriorityClass)_originalPriority.Value;
                }
                catch { }
                _originalPriority = null;
            }

            if (_originalAffinity.HasValue)
            {
                try
                {
                    using (var p = Process.GetProcessById((int)CurrentTarget.Pid))
                        p.ProcessorAffinity = (IntPtr)(long)_originalAffinity.Value.ToUInt64();
                }
                catch { }
                _originalAffinity = null;
            }

            if (_fullscreenFlagSet && !string.IsNullOrEmpty(_fullscreenExe))
            {
                string erro;
                Compat.Set(_fullscreenExe, false, out erro);
                NoteFullscreenFlag(_fullscreenExe, false);
                _fullscreenFlagSet = false;
                _fullscreenExe = null;
            }

            if (_quieted.Count > 0)
            {
                foreach (var par in _quieted)
                {
                    try { using (var p = Process.GetProcessById(par.Key)) p.PriorityClass = par.Value; }
                    catch { }
                }
                Log(string.Format(Txt.MotFundoDevolvidos, _quieted.Count),
                    Theme.BoneDim);
                _quieted.Clear();
            }

            Log(string.Format(Txt.MotDesativado,
                string.IsNullOrEmpty(motivo) ? "" : " (" + motivo + ")"), Theme.Gold);
            Notify();
        }

        /// <summary>On program exit: undo everything, including the manual lock.</summary>
        public void UndoEverything()
        {
            if (Active) Deactivate(Txt.MotProgramaFechado);
            if (_rateChanged)
            {
                RestoreRate();
                Log(Txt.MotTelaDevolvida, Theme.BoneDim);
            }
            SysTimer.Restore();
        }

        // -------------------------------------------------------------- watchdog
        bool _warnedFocusLost, _warnedRestore;

        /// <summary>
        /// Runs twice a second. It handles three things: restoring the rate if
        /// something touches it while locked, noticing when the target window dies,
        /// and reverting the rate on alt-tab (when FollowFocus is on).
        /// </summary>
        public void Watch()
        {
            WatchLock();

            if (!Active || CurrentTarget == null) return;

            if (!Target.IsAlive(CurrentTarget))
            {
                Deactivate(Txt.MotFechouJanela);
                return;
            }

            if (!Config.FollowFocus || !Config.SyncDisplay || _manualLock) return;

            bool emFoco = Target.IsForeground(CurrentTarget);
            var m = Display.CurrentMode(_rateDevice ?? Device);
            if (m == null) return;

            if (!emFoco && _rateChanged)
            {
                RestoreRate();
                if (!_warnedFocusLost)
                {
                    Log(Txt.MotSaiuDoJogo, Theme.Dust);
                    _warnedFocusLost = true;
                }
            }
            else if (emFoco && !_rateChanged)
            {
                int alvo = TargetHz;
                string erro;
                if (alvo > 0 && alvo != m.Hz && ChangeRate(alvo, false, out erro))
                {
                    _warnedFocusLost = false;
                    Log(string.Format(Txt.MotVoltouAoJogo, alvo), Theme.Dust);
                }
            }
        }

        /// <summary>
        /// While the rate is locked, restore it if anything changes it underneath — an
        /// exclusive-fullscreen game, for instance. Gives up after a few tries:
        /// if something insists on changing it, fighting would only make the screen flicker forever.
        /// </summary>
        void WatchLock()
        {
            if (!RateLocked) return;

            var m = Display.CurrentMode(_rateDevice);
            if (m == null) return;

            if (m.Hz == LockedHz)
            {
                _restoreTries = 0;
                _warnedRestore = false;
                return;
            }

            if (_restoreTries >= 3)
            {
                if (!_warnedRestore)
                {
                    Log(Txt.MotDesistiRepor, Theme.Amber);
                    Log(Txt.MotDesistiMotivo, Theme.Dust);
                    _warnedRestore = true;
                }
                return;
            }

            string erro;
            if (Display.SetRate(_rateDevice, _rateWidth, _rateHeight, LockedHz, out erro))
            {
                _restoreTries++;
                Log(string.Format(Txt.MotAlgoMudou, m.Hz, LockedHz), Theme.Amber);
            }
            else _restoreTries++;
        }

        // -------------------------------------------------------------- crash recovery
        // If the program dies outright, the video mode could be left changed.
        // This note allows undoing it the next time it opens.
        static string RecoveryFile { get { return Path.Combine(Settings.Folder, "recovery.ini"); } }

        // List of executables whose fullscreen optimizations we touched.
        // If the program dies before undoing them, the uninstaller uses this list
        // so no flag is left behind in the user's registry.
        public static string FullscreenLedgerFile
        {
            get { return Path.Combine(Settings.Folder, "fullscreen-flags.txt"); }
        }

        static void NoteFullscreenFlag(string exe, bool incluir)
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                var lista = File.Exists(FullscreenLedgerFile)
                    ? File.ReadAllLines(FullscreenLedgerFile, Encoding.UTF8).ToList()
                    : new List<string>();

                lista.RemoveAll(l => string.Equals(l.Trim(), exe, StringComparison.OrdinalIgnoreCase)
                                     || l.Trim().Length == 0);
                if (incluir) lista.Add(exe);

                if (lista.Count == 0) { if (File.Exists(FullscreenLedgerFile)) File.Delete(FullscreenLedgerFile); }
                else File.WriteAllLines(FullscreenLedgerFile, lista.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        static void WriteRecovery(string dispositivo, int largura, int altura, int hz)
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.WriteAllLines(RecoveryFile, new[]
                {
                    "dispositivo=" + (dispositivo ?? ""),
                    "largura=" + largura, "altura=" + altura, "hz=" + hz
                }, Encoding.UTF8);
            }
            catch { }
        }

        static void ClearRecovery()
        {
            try { if (File.Exists(RecoveryFile)) File.Delete(RecoveryFile); } catch { }
        }

        /// <summary>On startup: if a note survived a crash, put the display back.</summary>
        /// <summary>Undoes fullscreen flags left over from an earlier crash.</summary>
        public static int CleanFullscreenLeftovers()
        {
            int n = 0;
            try
            {
                if (!File.Exists(FullscreenLedgerFile)) return 0;
                foreach (var linha in File.ReadAllLines(FullscreenLedgerFile, Encoding.UTF8))
                {
                    string exe = linha.Trim();
                    if (exe.Length == 0) continue;
                    string erro;
                    if (Compat.Set(exe, false, out erro)) n++;
                }
                File.Delete(FullscreenLedgerFile);
            }
            catch { }
            return n;
        }

        public bool RecoverIfNeeded(out string relato)
        {
            relato = null;
            try
            {
                if (!File.Exists(RecoveryFile)) return false;

                string disp = null; int larg = 0, alt = 0, hz = 0;
                foreach (var linha in File.ReadAllLines(RecoveryFile, Encoding.UTF8))
                {
                    int i = linha.IndexOf('=');
                    if (i <= 0) continue;
                    string c = linha.Substring(0, i).Trim(), v = linha.Substring(i + 1).Trim();
                    if (c == "dispositivo") disp = v.Length == 0 ? null : v;
                    else if (c == "largura") int.TryParse(v, out larg);
                    else if (c == "altura") int.TryParse(v, out alt);
                    else if (c == "hz") int.TryParse(v, out hz);
                }
                ClearRecovery();
                if (hz <= 0 || larg <= 0 || alt <= 0) return false;

                var agora = Display.CurrentMode(disp);
                if (agora != null && agora.Hz == hz) return false;   // ja esta certo

                string erro;
                if (Display.SetRate(disp, larg, alt, hz, out erro))
                {
                    relato = string.Format(Txt.MotSocorro, hz);
                    return true;
                }
            }
            catch { }
            return false;
        }

        // -------------------------------------------------------------- helpers
        static string DescribePriority(ProcessPriorityClass c)
        {
            switch (c)
            {
                case ProcessPriorityClass.Idle: return Txt.PrioIdle;
                case ProcessPriorityClass.BelowNormal: return Txt.PrioAbaixo;
                case ProcessPriorityClass.Normal: return Txt.PrioNormal;
                case ProcessPriorityClass.AboveNormal: return Txt.PrioAcima;
                case ProcessPriorityClass.High: return Txt.PrioAlta;
                case ProcessPriorityClass.RealTime: return Txt.PrioTempoReal;
                default: return c.ToString();
            }
        }

        static string ShortError(Exception ex)
        {
            if (ex is System.ComponentModel.Win32Exception || ex is UnauthorizedAccessException)
                return Txt.ErroAcessoNegado;
            if (ex is ArgumentException) return Txt.ErroProcessoSumiu;
            return ex.Message;
        }
    }
}
