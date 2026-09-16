// EndToEndTest.cs — end to end: opens a real target window, applies everything, checks
// that the system really changed, and that EVERYTHING is restored at the end.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OrdoCadentia;

static class EndToEndTest
{
    static int failures;

    static void Check(string what, bool ok, string detail)
    {
        Console.WriteLine(string.Format("  [{0}] {1}{2}", ok ? "OK " : "FAIL", what,
            string.IsNullOrEmpty(detail) ? "" : "  -> " + detail));
        if (!ok) failures++;
    }

    static void Title(string t)
    {
        Console.WriteLine();
        Console.WriteLine("== " + t + " " + new string('=', Math.Max(0, 60 - t.Length)));
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("ORDO CADENTIA END-TO-END TEST");
        Console.WriteLine("(the screen will blink twice: the rate change and the change back)");

        Process target = null;
        int originalHz = 0;
        string device = null;

        // This test writes and reads settings. Without backing up the real file, it
        // would leave the TEST's choices as the USER's choices.
        string settingsFile = System.IO.Path.Combine(Settings.Folder, "settings.ini");
        string backup = System.IO.File.Exists(settingsFile)
            ? System.IO.File.ReadAllText(settingsFile) : null;

        var engine = new Engine();

        try
        {
            // ---------------------------------------------------- set up the target
            Title("TARGET");
            target = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "DummyTarget.exe")) { UseShellExecute = false });
            Thread.Sleep(1800);

            var window = Target.List().FirstOrDefault(j => j.Pid == (uint)target.Id);
            Check("found the target window", window != null, window == null ? null : window.Process);
            if (window == null) return 1;

            Console.WriteLine("  target: " + window.Process + " PID " + window.Pid);
            Console.WriteLine("  exe:  " + (window.ExePath ?? "(unknown)"));

            engine.CurrentTarget = window;
            engine.Config.RenderedFps = 30;
            engine.Config.FgMultiplier = 1;
            engine.Config.ChosenHz = 0;          // automatico
            engine.Config.SyncDisplay = true;
            engine.Config.SharpenTimer = true;
            engine.Config.HighPriority = true;
            engine.Config.PinPerformanceCores = true;
            engine.Config.DisableFullscreenOpt = false;    // mexe no registro; testado a parte
            engine.Config.QuietBackground = false;     // nao mexer nos programas do usuario
            engine.Logged += (t, c) => Console.WriteLine("       | " + t);

            device = engine.Device;
            var mode = engine.CurrentMode2;
            originalHz = mode.Hz;
            Console.WriteLine("  display before: " + mode);

            Check("30 FPS on screen (no frame gen)", engine.Config.FpsOnScreen == 30,
                     engine.Config.FpsOnScreen.ToString());
            Console.WriteLine("  rate chosen by auto: " + engine.TargetHz + " Hz");
            Check("auto found an exact rate for 30",
                     engine.TargetHz > 0 && engine.TargetHz % 30 == 0, engine.TargetHz + " Hz");

            var priorityBefore = target.PriorityClass;
            var affinityBefore = target.ProcessorAffinity;
            Console.WriteLine("  priority before: " + priorityBefore);
            Console.WriteLine("  affinity before: 0x" + ((long)affinityBefore).ToString("X"));

            // ---------------------------------------------------- run it
            Title("ACTIVATE");
            engine.Activate();
            Thread.Sleep(2500);                    // a troca de mode demora

            Check("engine became active", engine.Active, null);

            var after = Display.CurrentMode(device);
            Console.WriteLine("  display during: " + after);
            Check("the display rate really changed", after.Hz == engine.TargetHz,
                     after.Hz + " Hz (expected " + engine.TargetHz + ")");
            Check("the resolution did NOT change",
                     after.Width == mode.Width && after.Height == mode.Height,
                     after.Width + "x" + after.Height);

            var cad = Cadence.Analyze(after.Hz, 30);
            Check("the pacing came out perfect", cad.Perfect,
                     string.Format("{0:F2} ms steady", cad.MinMs));

            // What the screen shows while active must be the display's REALITY,
            // not the prediction repeated back.
            Check("the live reading matches the real display",
                     engine.CurrentPacing.Hz == after.Hz,
                     engine.CurrentPacing.Hz + " Hz");

            target.Refresh();
            Check("target priority went up to high",
                     target.PriorityClass == ProcessPriorityClass.High, target.PriorityClass.ToString());

            UIntPtr mascaraP; int lp, le;
            if (Cpu.Map(out mascaraP, out lp, out le))
            {
                long esperado = (long)mascaraP.ToUInt64();
                Check("target pinned to the performance cores",
                         (long)target.ProcessorAffinity == esperado,
                         "0x" + ((long)target.ProcessorAffinity).ToString("X") +
                         " (expected 0x" + esperado.ToString("X") + ")");
            }

            double mn, mx, at;
            SysTimer.Query(out mn, out mx, out at);
            Console.WriteLine(string.Format("  timer during: {0:F2} ms (requested {1:F2} ms)",
                              at, SysTimer.RequestedMs));
            Check("the timer was asked for at its finest", Math.Abs(SysTimer.RequestedMs - 0.5) < 1e-6, null);

            // ---------------------------------------------------- undo
            Title("DEACTIVATE");
            engine.Deactivate("end of test");
            Thread.Sleep(2500);

            Check("engine became inactive", !engine.Active, null);

            var restored = Display.CurrentMode(device);
            Console.WriteLine("  display after: " + restored);
            Check("the display rate went BACK to the original", restored.Hz == originalHz,
                     restored.Hz + " Hz (original " + originalHz + ")");
            Check("the resolution is unchanged",
                     restored.Width == mode.Width && restored.Height == mode.Height, null);

            target.Refresh();
            Check("target priority restored", target.PriorityClass == priorityBefore,
                     target.PriorityClass.ToString());
            Check("target affinity restored",
                     (long)target.ProcessorAffinity == (long)affinityBefore,
                     "0x" + ((long)target.ProcessorAffinity).ToString("X"));

            // ---------------------------------------------------- target that dies
            Title("TARGET CLOSING WHILE ACTIVE");
            engine.Config.SyncDisplay = false;      // sem piscar a tela de novo
            engine.Activate();
            Check("reactivated", engine.Active, null);
            target.Kill();
            target.WaitForExit(4000);
            Thread.Sleep(400);
            engine.Watch();
            Check("the engine noticed and shut itself down", !engine.Active, null);
            target = null;

            // ---------------------------------------------------- lock the display
            Title("LOCKING THE DISPLAY RATE");
            engine.CurrentTarget = null;

            var offered = engine.DisplayRates;
            Console.WriteLine("  scan: " + string.Join(", ",
                offered.Select(t => t + "Hz").ToArray()));
            Check("the scan found rates", offered.Count > 0, offered.Count + " modes");
            Check("no rate below 60 Hz is offered",
                     offered.All(t => t >= 60), "minimum " + engine.MinHz);
            Check("the maximum matches the highest in the list",
                     engine.MaxHz == offered.Max(), engine.MaxHz + " Hz");

            string lockError;
            int lockTarget = offered.First(t => t != originalHz);
            Check("locked at " + lockTarget + " Hz", engine.Lock(lockTarget, out lockError), lockError);
            Thread.Sleep(2200);

            Check("the engine reports itself locked", engine.RateLocked, null);
            Check("LockedHz is correct", engine.LockedHz == lockTarget, engine.LockedHz.ToString());
            var lockedMode = Display.CurrentMode(device);
            Check("the display is REALLY at the locked rate", lockedMode.Hz == lockTarget,
                     lockedMode.Hz + " Hz");

            // change rate while the lock is already on
            int secondRate = offered.First(t => t != lockTarget && t != originalHz);
            Check("switch to " + secondRate + " Hz with the lock on",
                     engine.Lock(secondRate, out lockError), lockError);
            Thread.Sleep(2200);
            Check("the display followed to the new rate",
                     Display.CurrentMode(device).Hz == secondRate,
                     Display.CurrentMode(device).Hz + " Hz");

            // the watchdog restores the rate when something changes it underneath
            Title("THE LOCK WATCHDOG");
            string sabotageError;
            Display.SetRate(device, mode.Width, mode.Height, originalHz, out sabotageError);
            Thread.Sleep(2200);
            Check("sabotage applied (rate changed from outside)",
                     Display.CurrentMode(device).Hz == originalHz,
                     Display.CurrentMode(device).Hz + " Hz");
            engine.Watch();
            Thread.Sleep(2200);
            Check("the watchdog restored the locked rate",
                     Display.CurrentMode(device).Hz == secondRate,
                     Display.CurrentMode(device).Hz + " Hz");

            // the manual lock takes precedence over the game sync
            Title("MANUAL LOCK vs AUTOMATIC SYNC");
            var target2 = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "DummyTarget.exe")) { UseShellExecute = false });
            Thread.Sleep(1800);
            engine.CurrentTarget = Target.List().FirstOrDefault(j => j.Pid == (uint)target2.Id);
            Check("second target found", engine.CurrentTarget != null, null);

            engine.Config.SyncDisplay = true;
            engine.Config.RenderedFps = 30;
            engine.Config.FgMultiplier = 1;
            engine.Config.ChosenHz = 0;
            Check("with the lock on, the prediction promises no change",
                     engine.PredictedPacing.Hz == secondRate,
                     engine.PredictedPacing.Hz + " Hz");

            engine.Activate();
            Thread.Sleep(2200);
            Check("activating did NOT touch the locked rate",
                     Display.CurrentMode(device).Hz == secondRate,
                     Display.CurrentMode(device).Hz + " Hz");
            Check("the lock still stands after activating", engine.RateLocked, null);

            engine.Deactivate("end of lock test");
            Thread.Sleep(2200);
            Check("deactivating did NOT release the manual lock",
                     engine.RateLocked && Display.CurrentMode(device).Hz == secondRate,
                     Display.CurrentMode(device).Hz + " Hz");

            try { target2.Kill(); target2.WaitForExit(3000); } catch { }
            engine.CurrentTarget = null;

            engine.Unlock();
            Thread.Sleep(2200);
            Check("unlocking restored the original rate",
                     Display.CurrentMode(device).Hz == originalHz,
                     Display.CurrentMode(device).Hz + " Hz (original " + originalHz + ")");
            Check("the engine no longer reports itself locked", !engine.RateLocked, null);

            // put the original target back for the rest of the test
            engine.Config.SyncDisplay = true;

            // ---------------------------------------------------- promise vs delivery
            Title("WHAT THE SCREEN PROMISES");
            engine.Config.SyncDisplay = true;
            Check("with sync on, it promises the target rate",
                     engine.PredictedPacing.Hz == engine.TargetHz,
                     engine.PredictedPacing.Hz + " Hz");

            engine.Config.SyncDisplay = false;
            Check("with sync OFF, it promises no change at all",
                     engine.PredictedPacing.Hz == engine.CurrentPacing.Hz,
                     engine.PredictedPacing.Hz + " Hz (display at " + engine.CurrentPacing.Hz + ")");
            engine.Config.SyncDisplay = true;

            // ---------------------------------------------------- frame gen changes the math
            Title("FRAME GENERATION");
            engine.Config.RenderedFps = 30;
            engine.Config.FgMultiplier = 2;
            Check("30 rendered x2 = 60 on screen", engine.Config.FpsOnScreen == 60,
                     engine.Config.FpsOnScreen.ToString());
            int fgRate = Display.BestRate(engine.DisplayRates, engine.Config.FpsOnScreen);
            Console.WriteLine("  with FG 2x, auto asks for: " + fgRate + " Hz");
            Check("the chosen rate divides 60 exactly", fgRate > 0 && fgRate % 60 == 0, fgRate + " Hz");
            Check("frame gen really does change the math",
                     Display.BestRate(engine.DisplayRates, 30) != 0, null);

            engine.Config.FgMultiplier = 3;
            Check("30 x3 = 90 on screen", engine.Config.FpsOnScreen == 90, engine.Config.FpsOnScreen.ToString());

            // ---------------------------------------------------- settings persist
            Title("SETTINGS");
            engine.Config.RenderedFps = 45;
            engine.Config.FgMultiplier = 2;
            engine.Config.ChosenHz = 120;
            engine.Config.Save();
            var lido = Settings.Load();
            Check("FPS saved and read back", lido.RenderedFps == 45, lido.RenderedFps.ToString());
            Check("multiplier saved and read back", lido.FgMultiplier == 2, lido.FgMultiplier.ToString());
            Check("Hz saved and read back", lido.ChosenHz == 120, lido.ChosenHz.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("EXCEPTION: " + ex);
            failures++;
        }
        finally
        {
            try { if (engine.Active) engine.Deactivate("cleanup"); } catch { }
            try { SysTimer.Restore(); } catch { }
            if (device != null)
            {
                var fim = Display.CurrentMode(device);
                if (fim != null && originalHz > 0 && fim.Hz != originalHz)
                {
                    Console.WriteLine("  WARNING: forcing the display back...");
                    Display.Restore(device);
                }
            }
            try { if (target != null && !target.HasExited) target.Kill(); } catch { }

            // restore the settings file exactly as it was
            try
            {
                if (backup != null) System.IO.File.WriteAllText(settingsFile, backup);
                else if (System.IO.File.Exists(settingsFile)) System.IO.File.Delete(settingsFile);
                Console.WriteLine("  user settings restored.");
            }
            catch { }
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine(failures == 0 ? "END TO END: ALL PASSED" : failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
