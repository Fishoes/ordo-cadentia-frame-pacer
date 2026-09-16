// MakeIcon.cs — generates the app sigil as a multi-size .ico plus a .png preview.
// The mark: a ring, 30 ticks (one per frame of 30 FPS), an inverted triangle and a vertical eye.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class GerarIcone
{
    // Palette: greenish black, blood crimson, aged bone.
    static readonly Color Fundo    = Color.FromArgb(255, 11, 14, 12);
    static readonly Color Crimson = Color.FromArgb(255, 155, 17, 26);
    static readonly Color Ember    = Color.FromArgb(255, 226, 74, 54);
    static readonly Color Bone     = Color.FromArgb(255, 214, 200, 166);

    static Bitmap Draw(int tam)
    {
        var bmp = new Bitmap(tam, tam, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float s = tam / 256f;          // fator de escala a partir do desenho base 256x256
            float cx = tam / 2f, cy = tam / 2f;
            bool micro = tam <= 20;        // em 16px so sobra o essencial

            // --- background disc ---
            float rDisco = 120f * s;
            using (var caminho = new GraphicsPath())
            {
                caminho.AddEllipse(cx - rDisco, cy - rDisco, rDisco * 2, rDisco * 2);
                using (var pincel = new PathGradientBrush(caminho))
                {
                    pincel.CenterPoint = new PointF(cx, cy);
                    pincel.CenterColor = Color.FromArgb(255, 24, 18, 18);
                    pincel.SurroundColors = new[] { Fundo };
                    g.FillPath(pincel, caminho);
                }
            }

            // --- outer crimson ring ---
            float rAnel = 112f * s;
            using (var caneta = new Pen(Crimson, Math.Max(1.5f, (micro ? 7f : 12f) * s)))
                g.DrawEllipse(caneta, cx - rAnel, cy - rAnel, rAnel * 2, rAnel * 2);

            // inner glow of the ring
            using (var caneta = new Pen(Color.FromArgb(90, Ember), Math.Max(1f, 3.5f * s)))
                g.DrawEllipse(caneta, cx - rAnel + 5 * s, cy - rAnel + 5 * s,
                              (rAnel - 5 * s) * 2, (rAnel - 5 * s) * 2);

            if (!micro)
            {
                // --- 30 ticks: one for each frame of a 30 FPS second ---
                float rInt = 92f * s, rExt = 101f * s;
                using (var caneta = new Pen(Color.FromArgb(190, Bone), Math.Max(1f, 2.6f * s)))
                using (var canetaForte = new Pen(Bone, Math.Max(1f, 4.2f * s)))
                {
                    for (int i = 0; i < 30; i++)
                    {
                        double ang = -Math.PI / 2 + i * (2 * Math.PI / 30);
                        bool cardeal = (i % 5 == 0);       // 6 marcas maiores
                        float ri = cardeal ? rInt - 7 * s : rInt;
                        var p = cardeal ? canetaForte : caneta;
                        g.DrawLine(p,
                            cx + (float)Math.Cos(ang) * ri, cy + (float)Math.Sin(ang) * ri,
                            cx + (float)Math.Cos(ang) * rExt, cy + (float)Math.Sin(ang) * rExt);
                    }
                }

                // --- inverted triangle (seal) ---
                float rTri = 74f * s;
                var tri = new PointF[3];
                for (int i = 0; i < 3; i++)
                {
                    double ang = Math.PI / 2 + i * (2 * Math.PI / 3);
                    tri[i] = new PointF(cx + (float)Math.Cos(ang) * rTri, cy + (float)Math.Sin(ang) * rTri);
                }
                using (var caneta = new Pen(Color.FromArgb(225, Bone), Math.Max(1f, 6f * s)))
                {
                    caneta.LineJoin = LineJoin.Round;
                    g.DrawPolygon(caneta, tri);
                }
            }

            // --- vertical eye at the centre (vesica piscis) ---
            float olhoA = (micro ? 62f : 40f) * s;   // meia-altura
            float olhoL = (micro ? 34f : 22f) * s;   // meia-largura
            using (var olho = new GraphicsPath())
            {
                olho.AddBezier(cx, cy - olhoA, cx + olhoL, cy - olhoA * 0.35f,
                               cx + olhoL, cy + olhoA * 0.35f, cx, cy + olhoA);
                olho.AddBezier(cx, cy + olhoA, cx - olhoL, cy + olhoA * 0.35f,
                               cx - olhoL, cy - olhoA * 0.35f, cx, cy - olhoA);
                olho.CloseFigure();

                using (var pincel = new PathGradientBrush(olho))
                {
                    pincel.CenterPoint = new PointF(cx, cy);
                    pincel.CenterColor = Color.FromArgb(255, 255, 214, 170);
                    pincel.SurroundColors = new[] { Crimson };
                    g.FillPath(pincel, olho);
                }
                using (var caneta = new Pen(Color.FromArgb(255, 60, 6, 10), Math.Max(1f, 3f * s)))
                    g.DrawPath(caneta, olho);
            }

            // pupil
            if (!micro)
            {
                float pr = 7f * s;
                using (var pincel = new SolidBrush(Color.FromArgb(255, 20, 4, 6)))
                    g.FillEllipse(pincel, cx - pr * 0.55f, cy - pr, pr * 1.1f, pr * 2);
            }
        }
        return bmp;
    }

    static void EscreverIco(string caminho, int[] tamanhos)
    {
        var imagens = new List<byte[]>();
        foreach (int t in tamanhos)
            using (var bmp = Draw(t))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);      // ICO com payload PNG (suportado Vista+)
                imagens.Add(ms.ToArray());
            }

        using (var fs = new FileStream(caminho, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);                      // reservado
            w.Write((ushort)1);                      // tipo = icone
            w.Write((ushort)tamanhos.Length);

            int offset = 6 + 16 * tamanhos.Length;
            for (int i = 0; i < tamanhos.Length; i++)
            {
                int t = tamanhos[i];
                w.Write((byte)(t >= 256 ? 0 : t));   // 0 significa 256
                w.Write((byte)(t >= 256 ? 0 : t));
                w.Write((byte)0);                    // paleta
                w.Write((byte)0);                    // reservado
                w.Write((ushort)1);                  // planos
                w.Write((ushort)32);                 // bits por pixel
                w.Write(imagens[i].Length);
                w.Write(offset);
                offset += imagens[i].Length;
            }
            foreach (var img in imagens) w.Write(img);
        }
    }

    static int Main(string[] args)
    {
        string destino = args.Length > 0 ? args[0] : "OrdoCadentia.ico";
        string pasta = Path.GetDirectoryName(Path.GetFullPath(destino));
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

        EscreverIco(destino, new[] { 256, 128, 64, 48, 32, 24, 16 });

        // side-by-side preview for a visual check
        string preview = Path.Combine(pasta ?? ".", "preview-sigilo.png");
        int[] amostras = { 256, 64, 32, 16 };
        int larg = 0; foreach (int t in amostras) larg += t + 16;
        using (var folha = new Bitmap(larg, 272, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(folha))
        {
            g.Clear(Color.FromArgb(255, 8, 10, 9));
            int x = 8;
            foreach (int t in amostras)
            {
                using (var b = Draw(t)) g.DrawImageUnscaled(b, x, 8 + (256 - t) / 2);
                x += t + 16;
            }
            folha.Save(preview, ImageFormat.Png);
        }

        Console.WriteLine("Icone gerado: " + Path.GetFullPath(destino));
        Console.WriteLine("Preview:      " + preview);
        return 0;
    }
}
