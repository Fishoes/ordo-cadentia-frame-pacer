// Theme.cs — visual identity: palette, typography and textures.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace OrdoCadentia
{
    internal static class Theme
    {
        public const string Product = "ORDO CADENTIA";
        public static string Subtitle { get { return Txt.Subtitle; } }
        public const string Version = "1.0";

        // ---------------------------------------------------------------- palette
        public static readonly Color Abyss = Color.FromArgb(255, 8, 10, 9);     // fundo mais fundo
        public static readonly Color Pitch = Color.FromArgb(255, 13, 16, 14);     // fundo da janela
        public static readonly Color Panel = Color.FromArgb(255, 20, 24, 21);   // cartoes
        public static readonly Color PanelHi = Color.FromArgb(255, 27, 32, 28);
        public static readonly Color Line = Color.FromArgb(255, 44, 50, 44);    // divisorias

        public static readonly Color Crimson = Color.FromArgb(255, 155, 17, 26);
        public static readonly Color CrimsonBright = Color.FromArgb(255, 199, 36, 42);
        public static readonly Color Ember = Color.FromArgb(255, 226, 74, 54);
        public static readonly Color Blood = Color.FromArgb(255, 108, 12, 19);

        public static readonly Color Bone = Color.FromArgb(255, 214, 200, 166);  // texto principal
        public static readonly Color BoneDim = Color.FromArgb(255, 150, 142, 122);
        public static readonly Color Dust = Color.FromArgb(255, 104, 100, 88); // texto apagado
        public static readonly Color Gold = Color.FromArgb(255, 198, 160, 78);

        public static readonly Color Moss = Color.FromArgb(255, 106, 138, 94);   // confirmacao
        public static readonly Color Amber = Color.FromArgb(255, 205, 150, 62);  // aviso

        // ---------------------------------------------------------------- typography
        // Georgia, Consolas and Segoe UI exist on every Windows install.
        // Fonts are declared in pixels and rebuilt at the machine's DPI scale,
        // otherwise text comes out tiny on 125%/150% displays.
        public static float Scale = 1f;

        public static Font Title, TitleSmall, Section, Body, BodyBold, Tiny, Data, DataBig, DataMid, ButtonFont;

        static Theme() { Init(1f); }

        /// <summary>Rebuilds the typography at the given scale (1.0 = 96 DPI).</summary>
        public static void Init(float escala)
        {
            Scale = (escala < 0.5f || escala > 4f) ? 1f : escala;
            float e = Scale;
            Title     = new Font("Georgia",  19f   * e, FontStyle.Bold,    GraphicsUnit.Pixel);
            TitleSmall  = new Font("Georgia",  14f   * e, FontStyle.Bold,    GraphicsUnit.Pixel);
            Section      = new Font("Georgia",  12.5f * e, FontStyle.Bold,    GraphicsUnit.Pixel);
            Body      = new Font("Segoe UI", 12f   * e, FontStyle.Regular, GraphicsUnit.Pixel);
            BodyBold   = new Font("Segoe UI", 12f   * e, FontStyle.Bold,    GraphicsUnit.Pixel);
            Tiny      = new Font("Segoe UI", 10.5f * e, FontStyle.Regular, GraphicsUnit.Pixel);
            Data       = new Font("Consolas", 12f   * e, FontStyle.Regular, GraphicsUnit.Pixel);
            DataBig = new Font("Consolas", 25f   * e, FontStyle.Bold,    GraphicsUnit.Pixel);
            DataMid  = new Font("Consolas", 15f   * e, FontStyle.Bold,    GraphicsUnit.Pixel);
            ButtonFont      = new Font("Georgia",  15f   * e, FontStyle.Bold,    GraphicsUnit.Pixel);
        }

        /// <summary>Converts a layout measurement into pixels at the current scale.</summary>
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        // ---------------------------------------------------------------- shapes
        public static GraphicsPath RoundedRect(Rectangle r, int raio)
        {
            var p = new GraphicsPath();
            if (raio <= 0) { p.AddRectangle(r); return p; }
            int d = raio * 2;
            d = Math.Min(d, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Card(Graphics g, Rectangle r, Color fundo, Color borda, int raio = 3)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = RoundedRect(r, raio))
            using (var b = new SolidBrush(fundo))
            {
                g.FillPath(b, p);

                // the grain crosses the cards too: without it they look
                // too clean against a dirty background and the illusion breaks.
                var estado = g.Save();
                g.SetClip(p);
                using (var pincel = new TextureBrush(Grain(), WrapMode.Tile))
                    g.FillRectangle(pincel, r);
                g.Restore(estado);

                using (var caneta = new Pen(borda)) g.DrawPath(caneta, p);
            }
        }

        /// <summary>Section header: a numeral, a title, and a rule running across.</summary>
        public static void Header(Graphics g, Rectangle r, string numeral, string titulo)
        {
            SizeF tn = g.MeasureString(string.IsNullOrEmpty(numeral) ? "I." : numeral, Section);
            float x = r.X;
            if (!string.IsNullOrEmpty(numeral))
            {
                using (var b = new SolidBrush(Crimson))
                    g.DrawString(numeral, Section, b, r.X, r.Y);
                x += g.MeasureString(numeral, Section).Width + 4;
            }

            using (var b = new SolidBrush(Bone))
                g.DrawString(titulo, Section, b, x, r.Y);

            SizeF tt = g.MeasureString(titulo, Section);
            float xf = x + tt.Width + 10;
            float y = r.Y + tn.Height / 2f;
            if (xf < r.Right - 6)
                using (var caneta = new Pen(Line))
                    g.DrawLine(caneta, xf, y, r.Right - 2, y);
        }

        public static void Text(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        public static void TextCentered(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (var b = new SolidBrush(c))
            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
                g.DrawString(s, f, b, r, fmt);
        }

        // ---------------------------------------------------------------- textures
        static Bitmap _grain;
        static readonly object _grainLock = new object();

        /// <summary>Film grain, 128x128, tiled. Generated once and reused.</summary>
        public static Bitmap Grain()
        {
            lock (_grainLock)
            {
                if (_grain != null) return _grain;
                var rnd = new Random(1313);         // semente fixa: o grao nunca "ferve"
                var bmp = new Bitmap(128, 128, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var dados = bmp.LockBits(new Rectangle(0, 0, 128, 128),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                int bytes = Math.Abs(dados.Stride) * 128;
                var buffer = new byte[bytes];
                for (int y = 0; y < 128; y++)
                {
                    int linha = y * dados.Stride;
                    for (int x = 0; x < 128; x++)
                    {
                        int i = linha + x * 4;
                        int v = rnd.Next(0, 255);
                        buffer[i + 0] = 190;                                        // B
                        buffer[i + 1] = 200;                                        // G
                        buffer[i + 2] = 205;                                        // R
                        buffer[i + 3] = (byte)(v < 236 ? 0 : 16 + rnd.Next(0, 14)); // A
                    }
                }
                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dados.Scan0, bytes);
                bmp.UnlockBits(dados);
                _grain = bmp;
                return _grain;
            }
        }

        /// <summary>
        /// Grain + scanlines + vignette: the "dirt" that sets the mood.
        /// <paramref name="r"/> is the whole window (it defines the vignette geometry) and
        /// <paramref name="clip"/> is only the piece being repainted.
        /// The distinction matters: the footer repaints twice a second, and without
        /// the clip each of those repaints would generate the whole window's atmosphere
        /// only to throw 95% away. Since the grain is a texture tiled from the
        /// origin and the vignette is computed over <paramref name="r"/>, painting only the
        /// clip produces exactly the same pixels.
        /// </summary>
        public static void Atmosphere(Graphics g, Rectangle r, Rectangle recorte)
        {
            recorte.Intersect(r);
            if (recorte.Width <= 0 || recorte.Height <= 0) return;

            // scanlines — aligned to r so they do not "walk" between repaints
            using (var caneta = new Pen(Color.FromArgb(14, 0, 0, 0)))
            {
                int primeira = r.Top + ((recorte.Top - r.Top + 2) / 3) * 3;
                for (int y = primeira; y < recorte.Bottom; y += 3)
                    g.DrawLine(caneta, recorte.Left, y, recorte.Right, y);
            }

            // grain
            using (var pincel = new TextureBrush(Grain(), WrapMode.Tile))
                g.FillRectangle(pincel, recorte);

            // vignette — geometry always comes from the whole window
            int m = Math.Max(r.Width, r.Height);
            var alvo = new Rectangle(r.X - m / 4, r.Y - m / 4, r.Width + m / 2, r.Height + m / 2);
            using (var caminho = new GraphicsPath())
            {
                caminho.AddEllipse(alvo);
                using (var pincel = new PathGradientBrush(caminho))
                {
                    pincel.CenterPoint = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
                    pincel.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pincel.SurroundColors = new[] { Color.FromArgb(168, 0, 0, 0) };
                    pincel.FocusScales = new PointF(0.42f, 0.30f);
                    g.FillRectangle(pincel, recorte);
                }
            }
        }

        public static void Atmosphere(Graphics g, Rectangle r) { Atmosphere(g, r, r); }

        /// <summary>Sigil: ring, ticks and eye. The same as the icon, drawn live.</summary>
        public static void Sigil(Graphics g, RectangleF r, float pulso, Color anel, bool comMarcas = true)
        {
            var antes = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float raio = Math.Min(r.Width, r.Height) / 2f;
            float s = raio / 128f;

            using (var caneta = new Pen(anel, Math.Max(1f, 11f * s)))
                g.DrawEllipse(caneta, cx - raio * 0.88f, cy - raio * 0.88f, raio * 1.76f, raio * 1.76f);

            int alfaPulso = (int)(40 + 55 * pulso);
            using (var caneta = new Pen(Color.FromArgb(alfaPulso, Ember), Math.Max(1f, 3.5f * s)))
                g.DrawEllipse(caneta, cx - raio * 0.80f, cy - raio * 0.80f, raio * 1.60f, raio * 1.60f);

            if (comMarcas && raio > 22)
            {
                float ri = raio * 0.72f, re = raio * 0.79f;
                using (var caneta = new Pen(Color.FromArgb(150, Bone), Math.Max(1f, 2.2f * s)))
                using (var forte = new Pen(Color.FromArgb(215, Bone), Math.Max(1f, 3.6f * s)))
                    for (int i = 0; i < 30; i++)
                    {
                        double a = -Math.PI / 2 + i * (2 * Math.PI / 30);
                        bool card = (i % 5 == 0);
                        float rr = card ? ri - raio * 0.05f : ri;
                        g.DrawLine(card ? forte : caneta,
                            cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr,
                            cx + (float)Math.Cos(a) * re, cy + (float)Math.Sin(a) * re);
                    }

                float rt = raio * 0.58f;
                var tri = new PointF[3];
                for (int i = 0; i < 3; i++)
                {
                    double a = Math.PI / 2 + i * (2 * Math.PI / 3);
                    tri[i] = new PointF(cx + (float)Math.Cos(a) * rt, cy + (float)Math.Sin(a) * rt);
                }
                using (var caneta = new Pen(Color.FromArgb(200, Bone), Math.Max(1f, 5f * s)))
                { caneta.LineJoin = LineJoin.Round; g.DrawPolygon(caneta, tri); }
            }

            // eye
            float oa = raio * 0.31f, ol = raio * 0.17f;
            using (var olho = new GraphicsPath())
            {
                olho.AddBezier(cx, cy - oa, cx + ol, cy - oa * 0.35f, cx + ol, cy + oa * 0.35f, cx, cy + oa);
                olho.AddBezier(cx, cy + oa, cx - ol, cy + oa * 0.35f, cx - ol, cy - oa * 0.35f, cx, cy - oa);
                olho.CloseFigure();
                using (var pincel = new PathGradientBrush(olho))
                {
                    pincel.CenterPoint = new PointF(cx, cy);
                    pincel.CenterColor = Color.FromArgb(255, (int)(210 + 45 * pulso), (int)(170 + 44 * pulso), 140);
                    pincel.SurroundColors = new[] { anel };
                    g.FillPath(pincel, olho);
                }
                using (var caneta = new Pen(Color.FromArgb(255, 48, 6, 9), Math.Max(1f, 2.4f * s)))
                    g.DrawPath(caneta, olho);
            }

            g.SmoothingMode = antes;
        }

        /// <summary>The colour representing pacing quality (0 to 100).</summary>
        public static Color ScoreColor(int nota)
        {
            if (nota >= 100) return Moss;
            if (nota >= 75) return Gold;
            if (nota >= 45) return Amber;
            return CrimsonBright;
        }

        public static void EnableDoubleBuffer(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(c, true, null);
        }
    }

    /// <summary>Base for every hand-drawn control: no flicker, anti-aliased.</summary>
    internal class PaintedControl : Control
    {
        public PaintedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.Bone;
            Font = Theme.Body;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            base.OnPaint(e);
        }
    }
}
