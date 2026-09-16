// PacingMeter.cs — the frame strip: the control that makes judder visible.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace OrdoCadentia
{
    // =====================================================================
    //  PACING METER — the visual heart of the program.
    //  Each block is one frame; the block's WIDTH is how long it stays
    //  on screen. Uneven blocks = judder. Even blocks = console.
    // =====================================================================
    internal sealed class PacingMeter : PaintedControl
    {
        public Cadence Before;
        public Cadence After;
        public string LabelBefore = "";
        public string LabelAfter = "";

        public PacingMeter() { Height = 168; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.Card(g, r, Theme.Panel, Theme.Line, 3);

            if (Before == null || Before.Hz <= 0)
            {
                Theme.TextCentered(g, Txt.MedVazio, Theme.Body,
                                 Theme.Dust, new RectangleF(0, 0, Width, Height));
                return;
            }

            int y = 12;
            y = Row(g, y, LabelBefore, Before, Theme.ScoreColor(Before.Score));

            if (After != null && After.Hz > 0)
            {
                using (var caneta = new Pen(Theme.Line) { DashStyle = DashStyle.Dot })
                    g.DrawLine(caneta, 12, y + 4, Width - 13, y + 4);
                y += 14;
                Row(g, y, LabelAfter, After, Theme.ScoreColor(After.Score));
            }
        }

        int Row(Graphics g, int y, string rotulo, Cadence c, Color cor)
        {
            Theme.Text(g, rotulo, Theme.Tiny, Theme.BoneDim, 12, y);

            string resumo = c.Perfect
                ? string.Format(Txt.MedConstante, c.Hz, c.Fps, c.MinMs)
                : string.Format(Txt.MedOscila, c.Hz, c.Fps, c.MinMs, c.MaxMs, c.SwingMs);
            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var pincel = new SolidBrush(cor))
                g.DrawString(resumo, Theme.Tiny, pincel, new RectangleF(90, y, Width - 104, 16), fmt);

            y += 19;

            // --- the frame strip ---
            int alturaFita = 30;
            var fita = new Rectangle(12, y, Width - 25, alturaFita);
            using (var pincel = new SolidBrush(Theme.Abyss)) g.FillRectangle(pincel, fita);

            // Each frame becomes a block whose width is proportional to its duration.
            // The frame count is chosen so each block is about 55 px wide:
            // with many frames on screen, the difference between waiting 5 or 6
            // refreshes becomes 1 px and the judder — the whole point — disappears.
            var intervalos = new List<int>();
            long anterior = 0;
            int maximoQuadros = Math.Max(6, Math.Min(12, fita.Width / 55));
            for (int k = 1; k <= maximoQuadros; k++)
            {
                long atual = (long)Math.Ceiling((double)k * c.Hz / c.Fps);
                intervalos.Add((int)(atual - anterior));
                anterior = atual;
            }

            int totalAtualizacoes = intervalos.Sum();
            if (totalAtualizacoes <= 0) return y + alturaFita + 8;

            float porAtualizacao = (float)fita.Width / totalAtualizacoes;
            float x = fita.X;

            // The block's WIDTH is the only signal, and colour merely reinforces the same
            // information. Shading alternate blocks on top of that made the strip
            // look striped — and the eye read the stripes, not the unevenness.
            foreach (int iv in intervalos)
            {
                float larg = iv * porAtualizacao;
                if (larg < 0.7f) { x += larg; continue; }

                bool longo = (iv > c.MinRefreshes);
                Color pintura = c.Perfect
                    ? Color.FromArgb(255, 74, 96, 66)
                    : (longo ? Theme.CrimsonBright : Color.FromArgb(255, 72, 58, 44));

                var bloco = new RectangleF(x + 1f, fita.Y + 1, Math.Max(0.8f, larg - 2), fita.Height - 2);
                using (var pincel = new SolidBrush(pintura)) g.FillRectangle(pincel, bloco);

                // the frame that overstays gets an ember on top: that is the one
                // the eye perceives as a "hitch"
                if (longo && larg > 3)
                    using (var pincel = new SolidBrush(Color.FromArgb(150, Theme.Ember)))
                        g.FillRectangle(pincel, new RectangleF(bloco.X, bloco.Y, bloco.Width, 3f));

                x += larg;
            }

            using (var caneta = new Pen(Theme.Line)) g.DrawRectangle(caneta, fita);

            y += alturaFita + 3;
            double janelaMs = totalAtualizacoes * 1000.0 / c.Hz;
            string legenda = c.Perfect
                ? string.Format(Txt.MedLegendaBoa, intervalos.Count, janelaMs)
                : string.Format(Txt.MedLegendaRuim, c.Pattern, intervalos.Count, janelaMs);
            Theme.Text(g, legenda, Theme.Tiny, Theme.Dust, 12, y);

            return y + 17;
        }

    }
}
