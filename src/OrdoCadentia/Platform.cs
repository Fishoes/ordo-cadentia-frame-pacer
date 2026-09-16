// Platform.cs — low-level services: pacing, video, timer, CPU, compatibility, targeting.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace OrdoCadentia
{
    // =====================================================================
    //  CADENCE — the arithmetic behind why 30 FPS stutters on a PC.
    // =====================================================================
    //  With VSync on, a frame can only appear on a display refresh.
    //  If Hz / FPS is not a whole number, each frame waits N refreshes, then
    //  N+1 -> the gap between frames swings -> judder.
    //  165 / 30 = 5.5  ->  5, 6, 5, 6...  ->  30.3 ms / 36.4 ms  -> STUTTERS
    //  120 / 30 = 4.0  ->  4, 4, 4, 4...  ->  33.3 ms steady     -> SMOOTH
    // =====================================================================
    internal sealed class Cadence
    {
        public int Hz;
        public int Fps;
        public double Ratio;            // atualizacoes por quadro
        public bool Perfect;
        public int MinRefreshes;
        public int MaxRefreshes;
        public double MinMs, MaxMs, SwingMs, StdDevMs;
        public string Pattern = "";      // ex.: "5-6-5-6"

        public static Cadence Analyze(int hz, int fps)
        {
            var c = new Cadence { Hz = hz, Fps = fps };
            if (hz <= 0 || fps <= 0) return c;

            c.Ratio = (double)hz / fps;
            c.Perfect = (hz % fps == 0);

            // Simulates the present queue: frame k appears on refresh ceil(k*Hz/Fps).
            int amostras = Math.Min(2000, fps * 20);
            var intervalos = new List<int>(amostras);
            long anterior = 0;
            for (int k = 1; k <= amostras; k++)
            {
                long atual = (long)Math.Ceiling((double)k * hz / fps);
                intervalos.Add((int)(atual - anterior));
                anterior = atual;
            }

            c.MinRefreshes = intervalos.Min();
            c.MaxRefreshes = intervalos.Max();
            double msPorAtualizacao = 1000.0 / hz;
            c.MinMs = c.MinRefreshes * msPorAtualizacao;
            c.MaxMs = c.MaxRefreshes * msPorAtualizacao;
            c.SwingMs = c.MaxMs - c.MinMs;

            double media = intervalos.Average() * msPorAtualizacao;
            double soma = intervalos.Sum(i => Math.Pow(i * msPorAtualizacao - media, 2));
            c.StdDevMs = Math.Sqrt(soma / intervalos.Count);

            // A short, readable pattern (skips the first, which suffers an edge effect).
            var amostra = intervalos.Skip(1).Take(8).ToList();
            c.Pattern = string.Join("-", amostra.Select(i => i.ToString()).ToArray());
            return c;
        }

        // 0 = unbearable, 100 = identical to a console.
        public int Score
        {
            get
            {
                if (Hz <= 0 || Fps <= 0) return 0;
                if (Perfect) return 100;
                double quadroIdealMs = 1000.0 / Fps;
                double erro = SwingMs / quadroIdealMs;      // oscilacao relativa
                int n = (int)Math.Round(100 - erro * 320);
                return Math.Max(0, Math.Min(99, n));
            }
        }

        public string Verdict
        {
            get
            {
                int n = Score;
                if (n >= 100) return Txt.CadenciaPerfeita;
                if (n >= 75) return Txt.TremorLeve;
                if (n >= 45) return Txt.TremorVisivel;
                return Txt.TremorSevero;
            }
        }
    }

    // =====================================================================
    //  DISPLAY — enumerates and switches the video mode of the target's monitor.
    // =====================================================================
    internal sealed class VideoMode
    {
        public int Width, Height, Hz;
        public override string ToString()
        {
            return string.Format("{0}x{1} @ {2} Hz", Width, Height, Hz);
        }
    }

    internal static class Display
    {
        public static string DeviceForWindow(IntPtr hWnd)
        {
            IntPtr mon = hWnd != IntPtr.Zero
                ? Native.MonitorFromWindow(hWnd, Native.MONITOR_DEFAULTTONEAREST)
                : Native.MonitorFromPoint(new Native.POINT(0, 0), Native.MONITOR_DEFAULTTOPRIMARY);

            var mi = Native.MONITORINFOEX.Novo();
            if (mon != IntPtr.Zero && Native.GetMonitorInfoW(mon, ref mi))
                return mi.szDevice;
            return null;   // null = monitor padrao
        }

        public static VideoMode CurrentMode(string dispositivo)
        {
            var dm = Native.DEVMODE.Novo();
            if (!Native.EnumDisplaySettingsExW(dispositivo, Native.ENUM_CURRENT_SETTINGS, ref dm, 0))
                return null;
            return new VideoMode
            {
                Width = (int)dm.dmPelsWidth,
                Height = (int)dm.dmPelsHeight,
                Hz = (int)dm.dmDisplayFrequency
            };
        }

        /// <summary>Rates available at the given resolution, in ascending order.</summary>
        public static List<int> AvailableRates(string dispositivo, int largura, int altura)
        {
            var taxas = new SortedSet<int>();
            for (int i = 0; ; i++)
            {
                var dm = Native.DEVMODE.Novo();
                if (!Native.EnumDisplaySettingsExW(dispositivo, i, ref dm, 0)) break;
                if (dm.dmPelsWidth == (uint)largura && dm.dmPelsHeight == (uint)altura &&
                    dm.dmBitsPerPel == 32 && dm.dmDisplayFrequency > 1)
                    taxas.Add((int)dm.dmDisplayFrequency);
            }
            return taxas.ToList();
        }

        /// <summary>
        /// Of the rates the display accepts, the ones the program offers: 60 Hz
        /// and up. Below that there is no gain and it only clutters the choice. If the
        /// display has none at 60 or above (an old TV, a projector),
        /// return the whole list so nobody is left without options.
        /// </summary>
        public static List<int> UsableRates(List<int> todas)
        {
            if (todas == null || todas.Count == 0) return new List<int>();
            var uteis = todas.Where(h => h >= 60).ToList();
            return uteis.Count > 0 ? uteis : todas;
        }

        /// <summary>
        /// Best rate for the target FPS: the HIGHEST one that divides evenly.
        /// Higher = less input lag and a lighter desktop.
        /// If none divides evenly, return the one with the least swing.
        /// </summary>
        public static int BestRate(List<int> disponiveis, int fps)
        {
            if (disponiveis == null || disponiveis.Count == 0 || fps <= 0) return 0;

            var exatas = disponiveis.Where(h => h % fps == 0).ToList();
            if (exatas.Count > 0) return exatas.Max();

            int melhor = disponiveis[0];
            double melhorOsc = double.MaxValue;
            foreach (int h in disponiveis)
            {
                double osc = Cadence.Analyze(h, fps).SwingMs;
                if (osc < melhorOsc - 1e-9) { melhorOsc = osc; melhor = h; }
            }
            return melhor;
        }

        /// <summary>Changes the rate, keeping the resolution. Temporary: not written to the registry.</summary>
        public static bool SetRate(string dispositivo, int largura, int altura, int hz, out string erro)
        {
            erro = null;
            var dm = Native.DEVMODE.Novo();
            if (!Native.EnumDisplaySettingsExW(dispositivo, Native.ENUM_CURRENT_SETTINGS, ref dm, 0))
            {
                erro = Txt.NaoLiMonitor;
                return false;
            }

            dm.dmPelsWidth = (uint)largura;
            dm.dmPelsHeight = (uint)altura;
            dm.dmDisplayFrequency = (uint)hz;
            dm.dmFields = Native.DM_PELSWIDTH | Native.DM_PELSHEIGHT | Native.DM_DISPLAYFREQUENCY;

            int teste = Native.ChangeDisplaySettingsExW(dispositivo, ref dm, IntPtr.Zero, Native.CDS_TEST, IntPtr.Zero);
            if (teste != Native.DISP_CHANGE_SUCCESSFUL)
            {
                erro = string.Format(Txt.MonitorRecusou, hz, Describe(teste));
                return false;
            }

            int r = Native.ChangeDisplaySettingsExW(dispositivo, ref dm, IntPtr.Zero,
                                                   Native.CDS_FULLSCREEN, IntPtr.Zero);
            if (r != Native.DISP_CHANGE_SUCCESSFUL)
            {
                erro = Describe(r);
                return false;
            }
            return true;
        }

        /// <summary>Returns the display to the mode saved in the registry (the user's "real" mode).</summary>
        public static bool Restore(string dispositivo)
        {
            int r = Native.ChangeDisplaySettingsExW(dispositivo, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            return r == Native.DISP_CHANGE_SUCCESSFUL;
        }

        static string Describe(int codigo)
        {
            switch (codigo)
            {
                case Native.DISP_CHANGE_SUCCESSFUL: return Txt.VideoOk;
                case Native.DISP_CHANGE_RESTART: return Txt.VideoReiniciar;
                case Native.DISP_CHANGE_BADMODE: return Txt.VideoModoRuim;
                case Native.DISP_CHANGE_FAILED: return Txt.VideoDriverRecusou;
                case Native.DISP_CHANGE_NOTUPDATED: return Txt.VideoNaoGravou;
                case Native.DISP_CHANGE_BADFLAGS: return Txt.VideoParametros;
                case Native.DISP_CHANGE_BADPARAM: return Txt.VideoParametro;
                case Native.DISP_CHANGE_BADDUALVIEW: return Txt.VideoMultiMonitor;
                default: return string.Format(Txt.VideoErroN, codigo);
            }
        }

        /// <summary>
        /// The display's real rate, measured by timing DwmFlush (which returns on every
        /// composition, that is, on every refresh). It reveals that "165 Hz" is really
        /// about 165.05 Hz. BLOCKS for ~150 ms: call it from a background thread.
        /// </summary>
        public static double MeasureRealRate(int amostras = 24)
        {
            try
            {
                long freq;
                if (!Native.QueryPerformanceFrequency(out freq) || freq <= 0) return 0;

                Native.DwmFlush();                      // alinha no inicio de uma atualizacao
                long t0; Native.QueryPerformanceCounter(out t0);
                for (int i = 0; i < amostras; i++) Native.DwmFlush();
                long t1; Native.QueryPerformanceCounter(out t1);

                double segundos = (t1 - t0) / (double)freq;
                if (segundos <= 0) return 0;
                double hz = amostras / segundos;
                return (hz > 20 && hz < 1000) ? hz : 0;   // fora disso, medicao suja
            }
            catch { return 0; }
        }
    }

    // =====================================================================
    //  SYSTIMER — the system timer resolution.
    // =====================================================================
    internal static class SysTimer
    {
        static bool _raised;
        static uint _original100ns;

        public static void Query(out double minMs, out double maxMs, out double atualMs)
        {
            uint mn, mx, at;
            minMs = maxMs = atualMs = 0;
            if (Native.NtQueryTimerResolution(out mn, out mx, out at) != 0) return;
            minMs = mn / 10000.0;      // "min" da API = periodo mais GROSSO
            maxMs = mx / 10000.0;      // "max" da API = periodo mais FINO
            atualMs = at / 10000.0;
        }

        /// <summary>The finest resolution we asked the system for, in ms.</summary>
        public static double RequestedMs { get; private set; }

        /// <summary>
        /// true when Windows granted the request to this process only — since
        /// Windows 10 2004 the request stopped applying system-wide unless
        /// GlobalTimerResolutionRequests is on. In that case the game does NOT inherit the
        /// precision, and the UI has to say so rather than pretend it worked.
        /// </summary>
        public static bool ProcessScopedOnly { get; private set; }

        public static bool Raise(out double globalMs, out string erro)
        {
            globalMs = 0; erro = null;
            try
            {
                uint mn, mx, at;
                if (Native.NtQueryTimerResolution(out mn, out mx, out at) != 0)
                { erro = Txt.TempNaoConsultou; return false; }

                if (!_raised) _original100ns = at;

                uint concedida;
                int s = Native.NtSetTimerResolution(mx, true, out concedida);   // mx = o mais fino
                if (s != 0) { erro = string.Format(Txt.TempRecusado, s); return false; }

                Native.TimeBeginPeriod(1);   // reforco pela API multimidia
                _raised = true;
                RequestedMs = mx / 10000.0;

                uint mn2, mx2, at2;
                Native.NtQueryTimerResolution(out mn2, out mx2, out at2);
                globalMs = at2 / 10000.0;

                // Granted to us, but the global clock did not follow: local effect only.
                ProcessScopedOnly = globalMs > RequestedMs + 1e-6;
                return true;
            }
            catch (Exception ex) { erro = ex.Message; return false; }
        }

        public static void Restore()
        {
            if (!_raised) return;
            try
            {
                uint ignorado;
                Native.TimeEndPeriod(1);
                Native.NtSetTimerResolution(_original100ns, false, out ignorado);
            }
            catch { }
            _raised = false;
            ProcessScopedOnly = false;
            RequestedMs = 0;
        }

        // Since Windows 10 2004 a resolution request applies only to the caller.
        // This key restores the global behaviour — but writing it affects the
        // whole system and needs a restart, so the program only READS it and
        // reports. Changing it is the user's call, with the steps in the instructions.
        const string ChaveKernel = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";

        public static bool GlobalEnabled()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(ChaveKernel))
                {
                    if (k == null) return false;
                    object v = k.GetValue("GlobalTimerResolutionRequests");
                    return v != null && Convert.ToInt32(v) == 1;
                }
            }
            catch { return false; }
        }

    }

    // =====================================================================
    //  CPU — hybrid topology (P cores vs E cores).
    // =====================================================================
    internal static class Cpu
    {
        public static int LogicalCount { get { return Environment.ProcessorCount; } }

        /// <summary>true when the CPU mixes performance and efficiency cores.</summary>
        public static bool IsHybrid { get { UIntPtr m; int p, e; return Map(out m, out p, out e); } }

        public static UIntPtr PerformanceMask
        {
            get { UIntPtr m; int p, e; return Map(out m, out p, out e) ? m : UIntPtr.Zero; }
        }

        public static string Summary()
        {
            UIntPtr m; int logP, logE;
            if (!Map(out m, out logP, out logE))
                return string.Format(Txt.CpuUniforme, LogicalCount);
            return string.Format(Txt.CpuHibrida, LogicalCount, logP, logE);
        }

        /// <summary>
        /// Reads GetLogicalProcessorInformationEx and splits cores by efficiency
        /// class. Returns false if the CPU is uniform or if there is more than one
        /// processor group (simple affinity does not apply in that case).
        /// </summary>
        public static bool Map(out UIntPtr mascaraP, out int logicosP, out int logicosE)
        {
            mascaraP = UIntPtr.Zero; logicosP = 0; logicosE = 0;

            uint tam = 0;
            Native.GetLogicalProcessorInformationEx(Native.RelationProcessorCore, IntPtr.Zero, ref tam);
            if (tam == 0) return false;

            IntPtr buf = Marshal.AllocHGlobal((int)tam);
            try
            {
                if (!Native.GetLogicalProcessorInformationEx(Native.RelationProcessorCore, buf, ref tam))
                    return false;

                var nucleos = new List<KeyValuePair<byte, ulong>>();   // classe -> mascara
                int pos = 0;
                while (pos + 8 <= (int)tam)
                {
                    int relacao = Marshal.ReadInt32(buf, pos);
                    int tamRegistro = Marshal.ReadInt32(buf, pos + 4);
                    if (tamRegistro <= 0 || pos + tamRegistro > (int)tam) break;

                    if (relacao == Native.RelationProcessorCore)
                    {
                        // PROCESSOR_RELATIONSHIP starting at +8:
                        //   +8 Flags | +9 EfficiencyClass | +10..29 Reserved
                        //   +30 GroupCount | +32 GroupMask[0].Mask | +40 GroupMask[0].Group
                        byte classe = Marshal.ReadByte(buf, pos + 9);
                        ushort grupos = (ushort)Marshal.ReadInt16(buf, pos + 30);
                        if (grupos >= 1)
                        {
                            ushort grupo = (ushort)Marshal.ReadInt16(buf, pos + 40);
                            if (grupo != 0) return false;            // varios grupos: nao mexemos
                            ulong mascara = (ulong)Marshal.ReadInt64(buf, pos + 32);
                            nucleos.Add(new KeyValuePair<byte, ulong>(classe, mascara));
                        }
                    }
                    pos += tamRegistro;
                }

                if (nucleos.Count == 0) return false;

                byte melhorClasse = nucleos.Max(n => n.Key);
                byte piorClasse = nucleos.Min(n => n.Key);
                if (melhorClasse == piorClasse) return false;         // CPU uniforme

                ulong mP = 0;
                foreach (var n in nucleos)
                {
                    int bits = CountBits(n.Value);
                    if (n.Key == melhorClasse) { mP |= n.Value; logicosP += bits; }
                    else logicosE += bits;
                }

                if (mP == 0) return false;
                mascaraP = new UIntPtr(mP);
                return true;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        static int CountBits(ulong v)
        {
            int c = 0;
            while (v != 0) { c += (int)(v & 1); v >>= 1; }
            return c;
        }

        public static string MaskToText(UIntPtr mascara)
        {
            ulong v = mascara.ToUInt64();
            var cpus = new List<string>();
            for (int i = 0; i < 64; i++) if ((v & (1UL << i)) != 0) cpus.Add(i.ToString());
            return "CPU " + string.Join(", ", cpus.ToArray());
        }
    }

    // =====================================================================
    //  COMPATIBILITY — turns off "Fullscreen Optimizations" for an executable.
    // =====================================================================
    internal static class Compat
    {
        const string Chave = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string Marca = "DISABLEDXMAXIMIZEDWINDOWEDMODE";

        public static bool IsOn(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Chave))
                {
                    if (k == null) return false;
                    var v = k.GetValue(exePath) as string;
                    return v != null && v.IndexOf(Marca, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        public static bool Set(string caminhoExe, bool ligar, out string erro)
        {
            erro = null;
            if (string.IsNullOrEmpty(caminhoExe)) { erro = Txt.SemCaminhoExe; return false; }
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Chave))
                {
                    if (k == null) { erro = Txt.SemRegistro; return false; }

                    var atual = (k.GetValue(caminhoExe) as string) ?? "";
                    var itens = atual.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    bool tem = itens.Any(i => i.Equals(Marca, StringComparison.OrdinalIgnoreCase));

                    if (ligar && !tem)
                    {
                        if (!itens.Contains("~")) itens.Insert(0, "~");   // "~" = aplica ao usuario atual
                        itens.Add(Marca);
                    }
                    else if (!ligar && tem)
                    {
                        itens.RemoveAll(i => i.Equals(Marca, StringComparison.OrdinalIgnoreCase));
                    }
                    else return true;   // ja esta como se pediu

                    // Only the "~" is left: no flags remain, so the value can go.
                    if (itens.Count == 0 || (itens.Count == 1 && itens[0] == "~"))
                        k.DeleteValue(caminhoExe, false);
                    else
                        k.SetValue(caminhoExe, string.Join(" ", itens.ToArray()), RegistryValueKind.String);
                }
                return true;
            }
            catch (Exception ex) { erro = ex.Message; return false; }
        }
    }

    // =====================================================================
    //  TARGET — the chosen window and the process behind it.
    // =====================================================================
    internal sealed class WindowInfo
    {
        public IntPtr Hwnd;
        public uint Pid;
        public string Title = "";
        public string Process = "";
        public string ExePath = "";
        public int Width, Height;

        public string Label
        {
            get
            {
                string t = Title.Length > 54 ? Title.Substring(0, 51) + "..." : Title;
                return string.Format("{0}   [{1}]", t, Process);
            }
        }

        public override string ToString() { return Label; }
    }

    internal static class Target
    {
        // Windows that are never interesting as a game target.
        static readonly string[] Ignored =
        {
            "framepacer", "applicationframehost", "systemsettings", "textinputhost",
            "shellexperiencehost", "searchhost", "searchapp", "startmenuexperiencehost",
            "explorer", "lockapp", "widgets", "widgetboard", "dwm", "sihost", "taskmgr",
            "narrator", "magnify", "peopleexperiencehost", "gameinputsvc"
        };

        public static List<WindowInfo> List()
        {
            var achadas = new List<WindowInfo>();
            uint meuPid = (uint)Process.GetCurrentProcess().Id;

            Native.EnumWindows((h, l) =>
            {
                try
                {
                    if (!Native.IsWindowVisible(h)) return true;
                    if (Native.GetAncestor(h, Native.GA_ROOT) != h) return true;

                    long estiloEx = Native.GetWindowLongSafe(h, Native.GWL_EXSTYLE);
                    if ((estiloEx & Native.WS_EX_TOOLWINDOW) != 0) return true;

                    long estilo = Native.GetWindowLongSafe(h, Native.GWL_STYLE);
                    if ((estilo & Native.WS_CHILD) != 0) return true;

                    int tam = Native.GetWindowTextLengthW(h);
                    if (tam <= 0) return true;

                    var sb = new StringBuilder(tam + 2);
                    Native.GetWindowTextW(h, sb, sb.Capacity);
                    string titulo = sb.ToString().Trim();
                    if (titulo.Length == 0) return true;

                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (pid == 0 || pid == meuPid) return true;

                    Native.RECT r;
                    if (!Native.GetWindowRect(h, out r)) return true;
                    if (r.Width < 200 || r.Height < 120) return true;   // barras, popups, tooltips

                    var info = new WindowInfo
                    {
                        Hwnd = h, Pid = pid, Title = titulo,
                        Width = r.Width, Height = r.Height
                    };
                    FillProcess(info);

                    string p = info.Process.ToLowerInvariant().Replace(".exe", "");
                    if (Array.IndexOf(Ignored, p) >= 0) return true;

                    achadas.Add(info);
                }
                catch { }
                return true;
            }, IntPtr.Zero);

            return achadas.OrderBy(j => j.Process, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(j => j.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static WindowInfo UnderCursor()
        {
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return null;
            IntPtr h = Native.WindowFromPoint(p);
            if (h == IntPtr.Zero) return null;
            h = Native.GetAncestor(h, Native.GA_ROOT);
            return De(h);
        }

        public static WindowInfo De(IntPtr h)
        {
            if (h == IntPtr.Zero || !Native.IsWindow(h)) return null;

            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (pid == 0) return null;
            if (pid == (uint)Process.GetCurrentProcess().Id) return null;

            int tam = Native.GetWindowTextLengthW(h);
            var sb = new StringBuilder(Math.Max(tam, 1) + 2);
            Native.GetWindowTextW(h, sb, sb.Capacity);

            Native.RECT r;
            Native.GetWindowRect(h, out r);

            var info = new WindowInfo
            {
                Hwnd = h, Pid = pid, Title = sb.ToString().Trim(),
                Width = r.Width, Height = r.Height
            };
            FillProcess(info);
            if (info.Title.Length == 0) info.Title = info.Process;
            return info;
        }

        static void FillProcess(WindowInfo info)
        {
            try
            {
                using (var p = Process.GetProcessById((int)info.Pid))
                    info.Process = p.ProcessName + ".exe";
            }
            catch { info.Process = "?"; }

            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, info.Pid);
            if (h == IntPtr.Zero) return;
            try
            {
                uint cap = 1024;
                var sb = new StringBuilder((int)cap);
                if (Native.QueryFullProcessImageNameW(h, 0, sb, ref cap))
                    info.ExePath = sb.ToString();
            }
            catch { }
            finally { Native.CloseHandle(h); }
        }

        public static bool IsAlive(WindowInfo info)
        {
            if (info == null || info.Hwnd == IntPtr.Zero) return false;
            if (!Native.IsWindow(info.Hwnd)) return false;
            try { using (Process.GetProcessById((int)info.Pid)) return true; }
            catch { return false; }
        }

        public static bool IsForeground(WindowInfo info)
        {
            if (info == null) return false;
            IntPtr f = Native.GetForegroundWindow();
            if (f == IntPtr.Zero) return false;
            if (f == info.Hwnd) return true;
            uint pid;
            Native.GetWindowThreadProcessId(f, out pid);
            return pid == info.Pid;     // outra janela do mesmo jogo tambem conta
        }

        /// <summary>Does the window fill the whole monitor? (borderless fullscreen)</summary>
        public static bool FillsMonitor(WindowInfo info)
        {
            if (info == null) return false;
            Native.RECT r;
            if (!Native.GetWindowRect(info.Hwnd, out r)) return false;
            IntPtr mon = Native.MonitorFromWindow(info.Hwnd, Native.MONITOR_DEFAULTTONEAREST);
            var mi = Native.MONITORINFOEX.Novo();
            if (!Native.GetMonitorInfoW(mon, ref mi)) return false;
            return Math.Abs(r.Width - mi.rcMonitor.Width) <= 2 &&
                   Math.Abs(r.Height - mi.rcMonitor.Height) <= 2;
        }
    }
}
