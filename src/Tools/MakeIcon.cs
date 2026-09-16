// MakeIcon.cs — generates the app sigil as a multi-size .ico plus a .png preview.
// The mark: a ring, 30 ticks (one per frame of 30 FPS), an inverted triangle and a vertical eye.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeIcon
{
    // Palette: greenish black, blood crimson, aged bone.
    static readonly Color Fundo    = Color.FromArgb(255, 11, 14, 12);
    static readonly Color Crimson = Color.FromArgb(255, 155, 17, 26);
    static readonly Color Ember    = Color.FromArgb(255, 226, 74, 54);
    static readonly Color Bone     = Color.FromArgb(255, 214, 200, 166);

    static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float s = size / 256f;          // fator de escala a partir do desenho base 256x256
            float cx = size / 2f, cy = size / 2f;
            bool micro = size <= 20;        // em 16px so sobra o essencial

            // --- background disc ---
            float discR = 120f * s;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - discR, cy - discR, discR * 2, discR * 2);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = Color.FromArgb(255, 24, 18, 18);
                    brush.SurroundColors = new[] { Fundo };
                    g.FillPath(brush, path);
                }
            }

            // --- outer crimson ring ---
            float ringR = 112f * s;
            using (var pen = new Pen(Crimson, Math.Max(1.5f, (micro ? 7f : 12f) * s)))
                g.DrawEllipse(pen, cx - ringR, cy - ringR, ringR * 2, ringR * 2);

            // inner glow of the ring
            using (var pen = new Pen(Color.FromArgb(90, Ember), Math.Max(1f, 3.5f * s)))
                g.DrawEllipse(pen, cx - ringR + 5 * s, cy - ringR + 5 * s,
                              (ringR - 5 * s) * 2, (ringR - 5 * s) * 2);

            if (!micro)
            {
                // --- 30 ticks: one for each frame of a 30 FPS second ---
                float innerR = 92f * s, outerR = 101f * s;
                using (var pen = new Pen(Color.FromArgb(190, Bone), Math.Max(1f, 2.6f * s)))
                using (var penBold = new Pen(Bone, Math.Max(1f, 4.2f * s)))
                {
                    for (int i = 0; i < 30; i++)
                    {
                        double ang = -Math.PI / 2 + i * (2 * Math.PI / 30);
                        bool major = (i % 5 == 0);       // 6 slots maiores
                        float ri = major ? innerR - 7 * s : innerR;
                        var p = major ? penBold : pen;
                        g.DrawLine(p,
                            cx + (float)Math.Cos(ang) * ri, cy + (float)Math.Sin(ang) * ri,
                            cx + (float)Math.Cos(ang) * outerR, cy + (float)Math.Sin(ang) * outerR);
                    }
                }

                // --- inverted triangle (seal) ---
                float triR = 74f * s;
                var tri = new PointF[3];
                for (int i = 0; i < 3; i++)
                {
                    double ang = Math.PI / 2 + i * (2 * Math.PI / 3);
                    tri[i] = new PointF(cx + (float)Math.Cos(ang) * triR, cy + (float)Math.Sin(ang) * triR);
                }
                using (var pen = new Pen(Color.FromArgb(225, Bone), Math.Max(1f, 6f * s)))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPolygon(pen, tri);
                }
            }

            // --- vertical eye at the centre (vesica piscis) ---
            float eyeH = (micro ? 62f : 40f) * s;   // meia-altura
            float eyeW = (micro ? 34f : 22f) * s;   // meia-largura
            using (var eye = new GraphicsPath())
            {
                eye.AddBezier(cx, cy - eyeH, cx + eyeW, cy - eyeH * 0.35f,
                               cx + eyeW, cy + eyeH * 0.35f, cx, cy + eyeH);
                eye.AddBezier(cx, cy + eyeH, cx - eyeW, cy + eyeH * 0.35f,
                               cx - eyeW, cy - eyeH * 0.35f, cx, cy - eyeH);
                eye.CloseFigure();

                using (var brush = new PathGradientBrush(eye))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = Color.FromArgb(255, 255, 214, 170);
                    brush.SurroundColors = new[] { Crimson };
                    g.FillPath(brush, eye);
                }
                using (var pen = new Pen(Color.FromArgb(255, 60, 6, 10), Math.Max(1f, 3f * s)))
                    g.DrawPath(pen, eye);
            }

            // pupil
            if (!micro)
            {
                float pr = 7f * s;
                using (var brush = new SolidBrush(Color.FromArgb(255, 20, 4, 6)))
                    g.FillEllipse(brush, cx - pr * 0.55f, cy - pr, pr * 1.1f, pr * 2);
            }
        }
        return bmp;
    }

    static void WriteIco(string path, int[] sizes)
    {
        var images = new List<byte[]>();
        foreach (int t in sizes)
            using (var bmp = Draw(t))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);      // ICO com payload PNG (suportado Vista+)
                images.Add(ms.ToArray());
            }

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);                      // reservado
            w.Write((ushort)1);                      // tipo = icone
            w.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int t = sizes[i];
                w.Write((byte)(t >= 256 ? 0 : t));   // 0 significa 256
                w.Write((byte)(t >= 256 ? 0 : t));
                w.Write((byte)0);                    // paleta
                w.Write((byte)0);                    // reservado
                w.Write((ushort)1);                  // planos
                w.Write((ushort)32);                 // bits por pixel
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
    }

    static int Main(string[] args)
    {
        string dest = args.Length > 0 ? args[0] : "OrdoCadentia.ico";
        string folder = Path.GetDirectoryName(Path.GetFullPath(dest));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        WriteIco(dest, new[] { 256, 128, 64, 48, 32, 24, 16 });

        // side-by-side preview for a visual check
        string preview = Path.Combine(folder ?? ".", "preview-sigilo.png");
        int[] samples = { 256, 64, 32, 16 };
        int width = 0; foreach (int t in samples) width += t + 16;
        using (var sheet = new Bitmap(width, 272, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 8, 10, 9));
            int x = 8;
            foreach (int t in samples)
            {
                using (var b = Draw(t)) g.DrawImageUnscaled(b, x, 8 + (256 - t) / 2);
                x += t + 16;
            }
            sheet.Save(preview, ImageFormat.Png);
        }

        Console.WriteLine("Icon written: " + Path.GetFullPath(dest));
        Console.WriteLine("Preview:      " + preview);
        return 0;
    }
}
