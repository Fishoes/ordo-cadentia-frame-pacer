// SelfTest.cs — exercises the engine with no UI. Validates P/Invoke, struct offsets and the math.
using System;
using System.Collections.Generic;
using System.Linq;
using OrdoCadentia;

static class SelfTest
{
    static int failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        Console.WriteLine(string.Format("  [{0}] {1}{2}", ok ? "OK " : "FAIL", what,
            string.IsNullOrEmpty(detail) ? "" : "  -> " + detail));
        if (!ok) failures++;
    }

    static void Title(string t)
    {
        Console.WriteLine();
        Console.WriteLine("== " + t + " " + new string('=', Math.Max(0, 62 - t.Length)));
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("ORDO CADENTIA ENGINE SELF-TEST");

        // ---------------------------------------------------------- PACING
        Title("PACING");
        var c165 = Cadence.Analyze(165, 30);
        Console.WriteLine(string.Format("  165Hz/30fps: ratio={0:F3} pattern={1} min={2:F2}ms max={3:F2}ms swing={4:F2}ms score={5} [{6}]",
            c165.Ratio, c165.Pattern, c165.MinMs, c165.MaxMs, c165.SwingMs, c165.Score, c165.Verdict));
        Check("165/30 is not perfect", !c165.Perfect, null);
        Check("165/30 alternates 5 and 6 refreshes",
                 c165.MinRefreshes == 5 && c165.MaxRefreshes == 6, c165.Pattern);
        Check("165/30 swings by ~6.06 ms", Math.Abs(c165.SwingMs - 6.0606) < 0.01,
                 c165.SwingMs.ToString("F4"));

        var c120 = Cadence.Analyze(120, 30);
        Console.WriteLine(string.Format("  120Hz/30fps: ratio={0:F3} pattern={1} min={2:F2}ms max={3:F2}ms swing={4:F2}ms score={5} [{6}]",
            c120.Ratio, c120.Pattern, c120.MinMs, c120.MaxMs, c120.SwingMs, c120.Score, c120.Verdict));
        Check("120/30 is perfect", c120.Perfect, null);
        Check("120/30 has no swing", Math.Abs(c120.SwingMs) < 1e-9, null);
        Check("120/30 scores 100", c120.Score == 100, c120.Score.ToString());
        Check("120/30 gives a 33.33 ms frame", Math.Abs(c120.MinMs - 33.3333) < 0.01, c120.MinMs.ToString("F4"));

        var c60 = Cadence.Analyze(60, 30);
        Check("60/30 is perfect", c60.Perfect && c60.SwingMs < 1e-9, null);
        Check("60/30 gives a 33.33 ms frame", Math.Abs(c60.MinMs - 33.3333) < 0.01, c60.MinMs.ToString("F4"));

        var c75 = Cadence.Analyze(75, 30);
        Console.WriteLine(string.Format("  75Hz/30fps:  pattern={0} swing={1:F2}ms score={2}", c75.Pattern, c75.SwingMs, c75.Score));
        Check("75/30 alternates 2 and 3", c75.MinRefreshes == 2 && c75.MaxRefreshes == 3, c75.Pattern);

        var c100 = Cadence.Analyze(100, 30);
        Console.WriteLine(string.Format("  100Hz/30fps: pattern={0} swing={1:F2}ms score={2}", c100.Pattern, c100.SwingMs, c100.Score));

        var c40 = Cadence.Analyze(120, 40);
        Check("120/40 is perfect (console 40 FPS mode)", c40.Perfect, c40.Pattern);
        var c60_60 = Cadence.Analyze(60, 60);
        Check("60/60 is perfect", c60_60.Perfect && Math.Abs(c60_60.MinMs - 16.6667) < 0.01, null);
        Check("165/30 scores worse than 120/30", c165.Score < c120.Score,
                 c165.Score + " < " + c120.Score);
        Check("invalid input does not break it", Cadence.Analyze(0, 30).Score == 0, null);

        // ---------------------------------------------------------- DISPLAY
        Title("DISPLAY");
        string dev = Display.DeviceForWindow(IntPtr.Zero);
        Console.WriteLine("  device: " + (dev ?? "(default)"));
        var current = Display.CurrentMode(dev);
        Check("read the current mode", current != null, current == null ? null : current.ToString());
        if (current != null)
        {
            var rates = Display.AvailableRates(dev, current.Width, current.Height);
            Console.WriteLine("  rates: " + string.Join(", ", rates.Select(t => t + "Hz").ToArray()));
            Check("enumerated the rates", rates.Count > 0, rates.Count + " modes");
            Check("the current rate is in the list", rates.Contains(current.Hz), current.Hz + "Hz");

            foreach (int fps in new[] { 30, 40, 60 })
            {
                int best = Display.BestRate(rates, fps);
                var an = Cadence.Analyze(best, fps);
                Console.WriteLine(string.Format("  target {0} FPS -> best rate {1}Hz (score {2}, {3})",
                    fps, best, an.Score, an.Verdict));
                Check("best rate for " + fps + " FPS is exact",
                         best > 0 && best % fps == 0, best + "Hz");
            }
            double real = Display.MeasureRealRate();
            Console.WriteLine(string.Format("  measured real rate (DwmFlush): {0:F4} Hz  [nominal {1}]", real, current.Hz));
            Check("DWM returned a plausible rate", real > 20 && real < 1000, real.ToString("F3"));
            Check("DWM rate matches the nominal one", Math.Abs(real - current.Hz) < 2.0,
                     string.Format("{0:F3} vs {1}", real, current.Hz));
        }

        // ---------------------------------------------------------- CPU
        Title("CPU");
        Console.WriteLine("  " + Cpu.Summary());
        UIntPtr mP; int lp, le;
        bool hybrid = Cpu.Map(out mP, out lp, out le);
        Console.WriteLine("  hybrid=" + hybrid + "  logicalP=" + lp + "  logicalE=" + le);
        if (hybrid)
        {
            Console.WriteLine("  P mask: 0x" + mP.ToUInt64().ToString("X") + "  (" + Cpu.MaskToText(mP) + ")");
            Check("P mask is not empty", mP.ToUInt64() != 0, null);
            Check("P + E equals the logical count", lp + le == Environment.ProcessorCount,
                     lp + "+" + le + " vs " + Environment.ProcessorCount);
            Check("efficiency cores were detected", le > 0, le.ToString());
            int bits = 0; ulong v = mP.ToUInt64();
            while (v != 0) { bits += (int)(v & 1); v >>= 1; }
            Check("bits in the P mask match the P count", bits == lp, bits + " vs " + lp);
        }
        else Console.WriteLine("  (uniform CPU — the core-pinning adjustment is unavailable)");

        // ---------------------------------------------------------- TIMER
        Title("TIMER");
        double mn, mx, at;
        SysTimer.Query(out mn, out mx, out at);
        Console.WriteLine(string.Format("  coarsest={0:F4}ms  finest={1:F4}ms  current={2:F4}ms", mn, mx, at));
        Check("read the timer resolution", mn > 0 && mx > 0 && at > 0, null);
        Check("the finest is <= the current", mx <= at + 1e-9, null);

        double granted; string error;
        bool raised = SysTimer.Raise(out granted, out error);
        Console.WriteLine(string.Format("  raise -> {0}  requested={1:F4}ms  global={2:F4}ms  processScopedOnly={3}  {4}",
            raised, SysTimer.RequestedMs, granted, SysTimer.ProcessScopedOnly, error ?? ""));
        Check("raised the resolution", raised, error);
        Check("asked for the finest period (0.5 ms)", Math.Abs(SysTimer.RequestedMs - 0.5) < 1e-6,
                 SysTimer.RequestedMs.ToString("F4"));
        Check("global clock ended up <= 1.0 ms", granted <= 1.0 + 1e-9, granted.ToString("F4"));
        Check("correctly detected the scope of the request",
                 SysTimer.ProcessScopedOnly == (granted > SysTimer.RequestedMs + 1e-6), null);
        SysTimer.Restore();
        SysTimer.Query(out mn, out mx, out at);
        Console.WriteLine(string.Format("  after restoring: current={0:F4}ms", at));
        Console.WriteLine("  GlobalTimerResolutionRequests on: " + SysTimer.GlobalEnabled());

        // ---------------------------------------------------------- TARGET
        Title("TARGET");
        var janelas = Target.List();
        Console.WriteLine("  candidate windows: " + janelas.Count);
        foreach (var j in janelas.Take(12))
            Console.WriteLine(string.Format("    pid {0,-6} {1,-26} {2}x{3}  {4}",
                j.Pid, j.Process, j.Width, j.Height,
                j.Title.Length > 40 ? j.Title.Substring(0, 37) + "..." : j.Title));
        Check("found at least one window", janelas.Count > 0, null);
        Check("no window without a process", janelas.All(j => !string.IsNullOrEmpty(j.Process)), null);
        Check("no window belongs to this program",
                 janelas.All(j => j.Process.IndexOf("ordocadentia", StringComparison.OrdinalIgnoreCase) < 0), null);
        int withPath = janelas.Count(j => !string.IsNullOrEmpty(j.ExePath));
        Console.WriteLine("  windows with a resolved exe path: " + withPath + "/" + janelas.Count);

        var first = janelas.FirstOrDefault();
        if (first != null)
        {
            Check("From(hwnd) finds the window again", Target.De(first.Hwnd) != null, null);
            Check("IsAlive confirms it", Target.IsAlive(first), null);
            Console.WriteLine("  fills the whole monitor: " + Target.FillsMonitor(first));
        }

        // ---------------------------------------------------------- COMPAT
        Title("COMPATIBILITY (fullscreen flags)");
        string fakeExe = @"C:\__frame_pacer_teste__\jogo.exe";
        string e2;
        bool before = Compat.IsOn(fakeExe);
        Check("starts off", !before, null);
        Check("turns on", Compat.Set(fakeExe, true, out e2), e2);
        Check("query confirms it is on", Compat.IsOn(fakeExe), null);
        Check("turning it on again is harmless", Compat.Set(fakeExe, true, out e2) && Compat.IsOn(fakeExe), e2);
        Check("turns off", Compat.Set(fakeExe, false, out e2), e2);
        Check("query confirms it is off", !Compat.IsOn(fakeExe), null);
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
            Check("value removed from the registry (no leftovers)",
                     k == null || k.GetValue(fakeExe) == null, null);

        // preserves flags set by other tools
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                   @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
        {
            k.SetValue(fakeExe, "~ RUNASADMIN", Microsoft.Win32.RegistryValueKind.String);
            Compat.Set(fakeExe, true, out e2);
            string v1 = (string)k.GetValue(fakeExe);
            Check("preserved RUNASADMIN when turning on", v1 != null && v1.Contains("RUNASADMIN"), v1);
            Compat.Set(fakeExe, false, out e2);
            string v2 = (string)k.GetValue(fakeExe);
            Check("preserved RUNASADMIN when turning off", v2 != null && v2.Contains("RUNASADMIN")
                     && !v2.Contains("DISABLEDXMAXIMIZED"), v2);
            k.DeleteValue(fakeExe, false);
        }

        // ---------------------------------------------------------- result
        Console.WriteLine();
        Console.WriteLine(new string('=', 66));
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
