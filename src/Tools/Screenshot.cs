// Screenshot.cs — launches an executable, waits for its window and saves a screenshot.
// Development only, for checking how things look.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Screenshot
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    static IntPtr FindWindow2(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) =>
        {
            uint wpid;
            GetWindowThreadProcessId(h, out wpid);
            if (wpid != (uint)pid || !IsWindowVisible(h)) return true;
            RECT r;
            if (!GetWindowRect(h, out r)) return true;
            if (r.R - r.L < 300 || r.B - r.T < 200) return true;   // ignora janelas fantasma
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    static int Main(string[] args)
    {
        SetProcessDPIAware();

        if (args.Length < 2)
        {
            Console.WriteLine("usage: Screenshot.exe <exe> <output.png> [waitMs] [--click x,y] [--keep]");
            return 2;
        }

        string exe = args[0], outPath = args[1];
        int waitMs = args.Length > 2 ? int.Parse(args[2]) : 2500;
        var clicks = new System.Collections.Generic.List<string>();
        bool keep = false;
        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--click" && i + 1 < args.Length) clicks.Add(args[++i]);
            else if (args[i] == "--keep") keep = true;
        }

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        var proc = Process.Start(psi);
        if (proc == null) { Console.WriteLine("could not launch it"); return 1; }

        Thread.Sleep(waitMs);

        IntPtr h = FindWindow2(proc.Id);
        if (h == IntPtr.Zero)
        {
            Console.WriteLine("window not found");
            try { proc.Kill(); } catch { }
            return 1;
        }

        SetForegroundWindow(h);
        Thread.Sleep(700);

        foreach (var click in clicks)
        {
            if (proc.HasExited) break;
            var parts = click.Split(',');
            RECT rr; GetWindowRect(h, out rr);
            int cx = rr.L + int.Parse(parts[0]), cy = rr.T + int.Parse(parts[1]);
            Cursor.Position = new Point(cx, cy);
            Thread.Sleep(250);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(1800);
            if (!proc.HasExited) h = FindNewestWindow(proc.Id, h);
        }

        if (proc.HasExited)
        {
            Console.WriteLine("the process exited after the clicks (expected once finished)");
            return 0;
        }

        RECT r2;
        GetWindowRect(h, out r2);
        int w = r2.R - r2.L, alt = r2.B - r2.T;
        Console.WriteLine(string.Format("window {0}x{1} at {2},{3}", w, alt, r2.L, r2.T));

        using (var bmp = new Bitmap(w, alt, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r2.L, r2.T, 0, 0, new Size(w, alt), CopyPixelOperation.SourceCopy);
            bmp.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine("saved: " + outPath);

        if (!keep) { try { proc.Kill(); proc.WaitForExit(3000); } catch { } }
        return 0;
    }

    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr extra);

    /// <summary>After a click that opens a dialog, grabs the process's newest window.</summary>
    static IntPtr FindNewestWindow(int pid, IntPtr padrao)
    {
        IntPtr best = IntPtr.Zero;
        int smallestArea = int.MaxValue;
        EnumWindows((h, p) =>
        {
            uint wpid;
            GetWindowThreadProcessId(h, out wpid);
            if (wpid != (uint)pid || !IsWindowVisible(h)) return true;
            RECT r;
            if (!GetWindowRect(h, out r)) return true;
            int a = (r.R - r.L) * (r.B - r.T);
            if (a < 150000) return true;
            if (a < smallestArea) { smallestArea = a; best = h; }   // o dialogo e menor que a principal
            return true;
        }, IntPtr.Zero);
        return best != IntPtr.Zero ? best : padrao;
    }
}
