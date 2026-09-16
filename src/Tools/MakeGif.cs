// MakeGif.cs — drives the program and records an animated GIF of it in use.
// Development only, for the README.
//
//   MakeGif.exe demo      <OrdoCadentia.exe> <DummyTarget.exe> <out.gif>
//   MakeGif.exe installer <Instalar-OrdoCadentia.exe>          <out.gif>
//
// Why it splices GIFs by hand: GDI+ can encode a GIF, but its SaveAdd path
// offers no way to set a per-frame delay, so every frame would play at zero
// delay and the animation would be a blur. So each frame is encoded to a
// single-frame GIF by GDI+ (which handles quantisation and LZW), and this
// file stitches those frames into one GIF89a with the delays it wants.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class MakeGif
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after,
                                                              int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_SHOWWINDOW = 0x0040;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const int GifWidth = 520;   // smaller than life size, to keep the file sensible

    // ---------------------------------------------------------------- windows
    static IntPtr FindWindow(int pid, int minArea = 60000)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) =>
        {
            uint wpid;
            GetWindowThreadProcessId(h, out wpid);
            if (wpid != (uint)pid || !IsWindowVisible(h)) return true;
            RECT r;
            if (!GetWindowRect(h, out r)) return true;
            if ((r.R - r.L) * (r.B - r.T) < minArea) return true;
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>After a click opens a dialog, the newest window is the smallest one.</summary>
    static IntPtr FindSmallest(int pid)
    {
        IntPtr best = IntPtr.Zero;
        int smallest = int.MaxValue;
        EnumWindows((h, p) =>
        {
            uint wpid;
            GetWindowThreadProcessId(h, out wpid);
            if (wpid != (uint)pid || !IsWindowVisible(h)) return true;
            RECT r;
            if (!GetWindowRect(h, out r)) return true;
            int a = (r.R - r.L) * (r.B - r.T);
            if (a < 150000) return true;
            if (a < smallest) { smallest = a; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    /// <summary>
    /// Brings the window forward and waits for it, because a click lands on
    /// whatever is in front. Without this, an automated click can end up
    /// pressing something in a window that is not ours.
    /// </summary>
    static bool Focus(IntPtr win)
    {
        for (int i = 0; i < 12; i++)
        {
            if (GetForegroundWindow() == win) return true;
            SetForegroundWindow(win);
            Thread.Sleep(180);
        }
        return GetForegroundWindow() == win;
    }

    static void Click(IntPtr win, int x, int y)
    {
        if (!Focus(win)) { Console.WriteLine("  (could not bring the window forward)"); return; }
        RECT r;
        GetWindowRect(win, out r);
        Cursor.Position = new Point(r.L + x, r.T + y);
        Thread.Sleep(130);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    static void ClickScreen(int x, int y)
    {
        Cursor.Position = new Point(x, y);
        Thread.Sleep(130);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    /// <summary>
    /// Reads the window's own contents with PrintWindow rather than grabbing
    /// the screen. Screen grabs capture whatever happens to be in front, and on
    /// a machine someone is actually using that is a coin toss — one recording
    /// came out with an unrelated window, and its contents, baked into it.
    /// PW_RENDERFULLCONTENT (2) renders from the window's own buffer.
    /// </summary>
    static Bitmap Grab(IntPtr win)
    {
        RECT r;
        GetWindowRect(win, out r);
        int w = r.R - r.L, h = r.B - r.T;
        var full = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        bool ok = false;
        using (var g = Graphics.FromImage(full))
        {
            IntPtr hdc = g.GetHdc();
            try { ok = PrintWindow(win, hdc, 2); }
            finally { g.ReleaseHdc(hdc); }
        }
        if (!ok)
            using (var g = Graphics.FromImage(full))
                g.CopyFromScreen(r.L, r.T, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);

        int gw = GifWidth, gh = (int)Math.Round(h * (GifWidth / (double)w));
        var small = new Bitmap(gw, gh, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(full, 0, 0, gw, gh);
        }
        full.Dispose();
        return small;
    }

    // ================================================================ GIF
    sealed class Frame
    {
        public byte[] Palette;
        public byte MinCodeSize;
        public byte[] Lzw;
        public int Width, Height;
    }

    // ---------------------------------------------------------------- palette
    // GDI+ picks its own colour table when handed a 32-bit bitmap, and on a dark
    // UI with film grain that table is a disaster: the result comes out as red
    // speckle. Handed an 8-bit indexed bitmap it keeps the palette it is given,
    // so the quantising happens here, with one adaptive palette shared by every
    // frame (median cut).

    sealed class Box
    {
        public List<int> Colors = new List<int>();   // packed 0xRRGGBB
        public int R0, R1, G0, G1, B0, B1;

        public void Measure()
        {
            R0 = G0 = B0 = 255; R1 = G1 = B1 = 0;
            foreach (int c in Colors)
            {
                int r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
                if (r < R0) R0 = r; if (r > R1) R1 = r;
                if (g < G0) G0 = g; if (g > G1) G1 = g;
                if (b < B0) B0 = b; if (b > B1) B1 = b;
            }
        }

        public int LongestAxis
        {
            get
            {
                int dr = R1 - R0, dg = G1 - G0, db = B1 - B0;
                return (dr >= dg && dr >= db) ? 0 : (dg >= db ? 1 : 2);
            }
        }

        public int Span { get { return Math.Max(R1 - R0, Math.Max(G1 - G0, B1 - B0)); } }

        public Color Average()
        {
            long r = 0, g = 0, b = 0;
            foreach (int c in Colors) { r += (c >> 16) & 0xFF; g += (c >> 8) & 0xFF; b += c & 0xFF; }
            int n = Math.Max(1, Colors.Count);
            return Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
        }
    }

    static Color[] BuildPalette(List<Bitmap> frames, int maxColors)
    {
        // One histogram across every frame, so the palette suits the whole
        // animation and no frame shifts colour halfway through.
        var seen = new HashSet<int>();
        foreach (var bmp in frames)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int bytes = Math.Abs(data.Stride) * bmp.Height;
            var buf = new byte[bytes];
            Marshal.Copy(data.Scan0, buf, 0, bytes);
            bmp.UnlockBits(data);

            for (int y = 0; y < bmp.Height; y += 2)          // sampling is plenty
                for (int x = 0; x < bmp.Width; x += 2)
                {
                    int i = y * data.Stride + x * 4;
                    seen.Add((buf[i + 2] << 16) | (buf[i + 1] << 8) | buf[i]);
                }
        }

        var first = new Box();
        first.Colors.AddRange(seen);
        first.Measure();
        var boxes = new List<Box> { first };

        while (boxes.Count < maxColors)
        {
            // Split whichever box still covers the widest range of colour.
            int pick = -1, widest = 0;
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].Colors.Count > 1 && boxes[i].Span > widest)
                { widest = boxes[i].Span; pick = i; }
            if (pick < 0) break;

            var box = boxes[pick];
            int axis = box.LongestAxis;
            box.Colors.Sort((a, b) =>
            {
                int va = axis == 0 ? (a >> 16) & 0xFF : axis == 1 ? (a >> 8) & 0xFF : a & 0xFF;
                int vb = axis == 0 ? (b >> 16) & 0xFF : axis == 1 ? (b >> 8) & 0xFF : b & 0xFF;
                return va.CompareTo(vb);
            });

            int half = box.Colors.Count / 2;
            var left = new Box(); var right = new Box();
            left.Colors.AddRange(box.Colors.GetRange(0, half));
            right.Colors.AddRange(box.Colors.GetRange(half, box.Colors.Count - half));
            left.Measure(); right.Measure();

            boxes[pick] = left;
            boxes.Add(right);
        }

        var palette = new Color[Math.Max(2, boxes.Count)];
        for (int i = 0; i < boxes.Count; i++) palette[i] = boxes[i].Average();
        for (int i = boxes.Count; i < palette.Length; i++) palette[i] = Color.Black;
        return palette;
    }

    /// <summary>Maps a bitmap onto the palette, caching lookups by RGB555.</summary>
    static Bitmap ToIndexed(Bitmap src, Color[] palette, short[] cache)
    {
        int w = src.Width, h = src.Height;
        var dst = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
        var pal = dst.Palette;
        for (int i = 0; i < pal.Entries.Length; i++)
            pal.Entries[i] = i < palette.Length ? palette[i] : Color.Black;
        dst.Palette = pal;

        var sd = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly,
                              PixelFormat.Format32bppArgb);
        var dd = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly,
                              PixelFormat.Format8bppIndexed);
        var sbuf = new byte[Math.Abs(sd.Stride) * h];
        var dbuf = new byte[Math.Abs(dd.Stride) * h];
        Marshal.Copy(sd.Scan0, sbuf, 0, sbuf.Length);

        for (int y = 0; y < h; y++)
        {
            int srow = y * sd.Stride, drow = y * dd.Stride;
            for (int x = 0; x < w; x++)
            {
                int i = srow + x * 4;
                int b = sbuf[i], g = sbuf[i + 1], r = sbuf[i + 2];
                int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);

                short idx = cache[key];
                if (idx < 0)
                {
                    int best = 0, bestDist = int.MaxValue;
                    for (int c = 0; c < palette.Length; c++)
                    {
                        int dr = r - palette[c].R, dg = g - palette[c].G, db = b - palette[c].B;
                        int dist = dr * dr + dg * dg + db * db;
                        if (dist < bestDist) { bestDist = dist; best = c; }
                    }
                    idx = (short)best;
                    cache[key] = idx;
                }
                dbuf[drow + x] = (byte)idx;
            }
        }

        Marshal.Copy(dbuf, 0, dd.Scan0, dbuf.Length);
        src.UnlockBits(sd);
        dst.UnlockBits(dd);
        return dst;
    }

    /// <summary>Encodes one bitmap as a GIF through GDI+, then pulls it apart.</summary>
    static Frame Encode(Bitmap bmp)
    {
        byte[] raw;
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Gif);
            raw = ms.ToArray();
        }

        int i = 6;                                   // skip "GIF89a"
        int w = raw[i] | (raw[i + 1] << 8);
        int h = raw[i + 2] | (raw[i + 3] << 8);
        byte packed = raw[i + 4];
        i += 7;

        byte[] palette = null;
        if ((packed & 0x80) != 0)
        {
            int entries = 2 << (packed & 0x07);
            palette = new byte[entries * 3];
            Array.Copy(raw, i, palette, 0, palette.Length);
            i += palette.Length;
        }

        while (i < raw.Length)
        {
            byte b = raw[i];
            if (b == 0x21)                            // extension: skip
            {
                i += 2;
                while (i < raw.Length && raw[i] != 0) i += raw[i] + 1;
                i++;
            }
            else if (b == 0x2C)                       // image descriptor
            {
                i += 1 + 8;
                byte ipacked = raw[i];
                i++;
                if ((ipacked & 0x80) != 0)
                {
                    int entries = 2 << (ipacked & 0x07);
                    palette = new byte[entries * 3];
                    Array.Copy(raw, i, palette, 0, palette.Length);
                    i += palette.Length;
                }

                byte minCode = raw[i]; i++;
                int start = i;
                while (i < raw.Length && raw[i] != 0) i += raw[i] + 1;
                i++;                                  // keep the 0x00 terminator

                var lzw = new byte[i - start];
                Array.Copy(raw, start, lzw, 0, lzw.Length);
                return new Frame { Palette = palette, MinCodeSize = minCode,
                                   Lzw = lzw, Width = w, Height = h };
            }
            else break;
        }
        throw new InvalidDataException("no image descriptor in the GIF GDI+ produced");
    }

    static void WriteGif(string path, List<Bitmap> frames, List<int> delaysMs)
    {
        Console.WriteLine("  building a shared palette...");
        var palette = BuildPalette(frames, 255);

        var cache = new short[32768];
        for (int i = 0; i < cache.Length; i++) cache[i] = -1;

        var encoded = new List<Frame>();
        foreach (var b in frames)
            using (var indexed = ToIndexed(b, palette, cache))
                encoded.Add(Encode(indexed));

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            var first = encoded[0];
            int bits = 0, entries = first.Palette.Length / 3;
            while ((2 << bits) < entries) bits++;

            w.Write(Encoding.ASCII.GetBytes("GIF89a"));
            w.Write((ushort)first.Width);
            w.Write((ushort)first.Height);
            w.Write((byte)(0x80 | (bits & 0x07)));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write(first.Palette);

            // Netscape extension: loop forever.
            w.Write(new byte[] { 0x21, 0xFF, 0x0B });
            w.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            w.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

            for (int i = 0; i < encoded.Count; i++)
            {
                var f = encoded[i];
                int cs = Math.Max(2, (int)Math.Round(delaysMs[i] / 10.0));

                w.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 });   // disposal 1, no transparency
                w.Write((ushort)cs);
                w.Write((byte)0);
                w.Write((byte)0);

                int fbits = 0, fentries = f.Palette.Length / 3;
                while ((2 << fbits) < fentries) fbits++;

                w.Write((byte)0x2C);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)f.Width); w.Write((ushort)f.Height);
                w.Write((byte)(0x80 | (fbits & 0x07)));           // per-frame colour table
                w.Write(f.Palette);

                w.Write(f.MinCodeSize);
                w.Write(f.Lzw);
            }

            w.Write((byte)0x3B);
        }
    }

    // ================================================================ demo
    // Where things sit in the main window, in window coordinates.
    const int BtnPick = 97, BtnPickY = 159;          // PICK WINDOW
    const int BtnAim = 248;                          // POINT
    const int ChipFps60X = 45, ChipFpsY = 296;       // the 60 chip, second row
    const int ChipFps30X = 111, ChipFps30Y = 250;    // the 30 chip, first row
    const int ChipFg2X = 124, ChipFgY = 362;         // frame generation 2x
    const int ChipFgNoX = 45;                        // frame generation off
    const int BtnActivateX = 164, BtnActivateY = 817;


    static int RecordDemo(string appExe, string targetExe, string output)
    {
        var frames = new List<Bitmap>();
        var delays = new List<int>();
        Process target = null, app = null;

        try
        {
            // A window to aim at. It is parked in the top-left corner, small
            // enough to stay clear of our own window, and pinned above everything
            // else: aiming reads whatever window is topmost under the cursor, so
            // without the pin it picks up whatever the machine happens to have
            // open there instead.
            target = Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = false });
            Thread.Sleep(1500);
            IntPtr targetWin = FindWindow(target.Id, 10000);
            if (targetWin != IntPtr.Zero)
                SetWindowPos(targetWin, HWND_TOPMOST, 10, 10, 400, 300, SWP_SHOWWINDOW);

            app = Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = false });
            Thread.Sleep(2800);
            IntPtr win = FindWindow(app.Id, 300000);
            if (win == IntPtr.Zero) { Console.WriteLine("main window not found"); return 1; }
            if (!Focus(win)) { Console.WriteLine("the window will not come forward"); return 1; }
            Thread.Sleep(700);

            // 1 — the program as it opens: the meter already shows the problem
            frames.Add(Grab(win)); delays.Add(2200);
            Console.WriteLine("  1  opening screen");

            // 2 — aim at the game window.
            //
            // This uses POINT rather than the window list on purpose. The list
            // shows every window open on the machine doing the recording, and
            // those titles are nobody's business once this is published.
            Click(win, BtnAim, BtnPickY);
            Thread.Sleep(800);
            frames.Add(Grab(win)); delays.Add(1700);
            Console.WriteLine("  2  aiming overlay");

            // The demo window sits at the top left, clear of ours.
            ClickScreen(200, 150);
            Thread.Sleep(1000);

            Focus(win);
            Thread.Sleep(500);
            frames.Add(Grab(win)); delays.Add(2300);
            Console.WriteLine("  3  target chosen, before/after filled in");

            // 3 — changing the frame rate makes the meter react
            Click(win, ChipFps60X, ChipFpsY);
            Thread.Sleep(800);
            frames.Add(Grab(win)); delays.Add(1800);
            Console.WriteLine("  4  60 FPS");

            // 4 — frame generation changes the arithmetic
            Click(win, ChipFps30X, ChipFps30Y);
            Thread.Sleep(500);
            Click(win, ChipFg2X, ChipFgY);
            Thread.Sleep(800);
            frames.Add(Grab(win)); delays.Add(2400);
            Console.WriteLine("  5  frame gen 2x -> 60 frames on screen");

            Click(win, ChipFgNoX, ChipFgY);
            Thread.Sleep(800);
            frames.Add(Grab(win)); delays.Add(1400);
            Console.WriteLine("  6  back to 30");

            // 5 — activate, and the display really changes
            Click(win, BtnActivateX, BtnActivateY);
            Thread.Sleep(2600);
            frames.Add(Grab(win)); delays.Add(3600);
            Console.WriteLine("  7  active");

            WriteGif(output, frames, delays);
            var fi = new FileInfo(output);
            Console.WriteLine("written: " + output + "  (" + (fi.Length / 1024) + " KB, "
                              + frames.Count + " frames)");
            return 0;
        }
        finally
        {
            foreach (var b in frames) b.Dispose();
            // Close the app properly so it restores the display rate itself.
            try { if (app != null && !app.HasExited) { app.CloseMainWindow(); app.WaitForExit(6000); } } catch { }
            try { if (app != null && !app.HasExited) app.Kill(); } catch { }
            try { if (target != null && !target.HasExited) target.Kill(); } catch { }
        }
    }

    // ================================================================ installer
    const int ChipEnglishX = 330, ChipLangY = 219;
    const int OptInstructionsY = 460, OptX = 300;
    const int InstallX = 330, InstallY = 559;

    static int RecordInstaller(string installer, string output)
    {
        var frames = new List<Bitmap>();
        var delays = new List<int>();
        var proc = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = false });
        if (proc == null) { Console.WriteLine("could not launch the installer"); return 1; }

        try
        {
            Thread.Sleep(2600);
            IntPtr win = FindWindow(proc.Id);
            if (win == IntPtr.Zero) { Console.WriteLine("installer window not found"); return 1; }
            Focus(win);
            Thread.Sleep(600);

            frames.Add(Grab(win)); delays.Add(1800);
            Click(win, ChipEnglishX, ChipLangY);
            Thread.Sleep(800);
            frames.Add(Grab(win)); delays.Add(1900);

            Click(win, OptX, OptInstructionsY);       // do not open Notepad while recording
            Thread.Sleep(400);

            Click(win, InstallX, InstallY);
            Thread.Sleep(340);
            frames.Add(Grab(win)); delays.Add(420);
            Thread.Sleep(380);
            frames.Add(Grab(win)); delays.Add(420);

            Thread.Sleep(1600);
            win = FindWindow(proc.Id);
            frames.Add(Grab(win)); delays.Add(3400);

            WriteGif(output, frames, delays);
            var fi = new FileInfo(output);
            Console.WriteLine("written: " + output + "  (" + (fi.Length / 1024) + " KB, "
                              + frames.Count + " frames)");
            return 0;
        }
        finally
        {
            foreach (var b in frames) b.Dispose();
            try { if (!proc.HasExited) { proc.Kill(); proc.WaitForExit(3000); } } catch { }
        }
    }

    // ================================================================ main
    static int Main(string[] args)
    {
        SetProcessDPIAware();

        if (args.Length >= 4 && args[0] == "demo")
            return RecordDemo(args[1], args[2], args[3]);
        if (args.Length >= 3 && args[0] == "installer")
            return RecordInstaller(args[1], args[2]);

        Console.WriteLine("usage: MakeGif.exe demo      <app.exe> <target.exe> <out.gif>");
        Console.WriteLine("       MakeGif.exe installer <installer.exe>        <out.gif>");
        return 2;
    }
}
