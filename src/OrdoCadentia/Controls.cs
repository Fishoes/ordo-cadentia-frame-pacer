// Controls.cs — hand-drawn controls in the app's theme.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace OrdoCadentia
{
    // =====================================================================
    //  PRIMARY BUTTON — the big one that commits the action.
    // =====================================================================
    internal sealed class PrimaryButton : PaintedControl
    {
        public string Text = "";
        public string Subtext = "";
        public bool Danger;              // true = estado "ativo", vira encerramento
        bool _hover, _pressionado;

        public PrimaryButton()
        {
            Height = 58;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressionado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color cima, baixo, borda, tinta;
            if (!Enabled)
            {
                cima = Color.FromArgb(255, 26, 30, 27); baixo = Color.FromArgb(255, 20, 24, 21);
                borda = Theme.Line; tinta = Theme.Dust;
            }
            else if (Danger)
            {
                cima = Color.FromArgb(255, 42, 30, 26); baixo = Color.FromArgb(255, 30, 22, 20);
                borda = Color.FromArgb(255, 96, 52, 44); tinta = Theme.Bone;
            }
            else
            {
                int reforco = _pressionado ? -18 : (_hover ? 22 : 0);
                cima = Shift(Theme.Crimson, 26 + reforco);
                baixo = Shift(Theme.Blood, reforco);
                borda = Shift(Theme.CrimsonBright, reforco);
                tinta = Color.FromArgb(255, 248, 238, 216);
            }

            using (var caminho = Theme.RoundedRect(r, 3))
            using (var pincel = new LinearGradientBrush(
                       new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), cima, baixo, 90f))
            {
                g.FillPath(pincel, caminho);
                using (var caneta = new Pen(borda, 1.4f)) g.DrawPath(caneta, caminho);
            }

            if (Enabled && !Danger && _hover)
                using (var caminho = Theme.RoundedRect(Rectangle.Inflate(r, -3, -3), 2))
                using (var caneta = new Pen(Color.FromArgb(60, Theme.Ember)))
                    g.DrawPath(caneta, caminho);

            bool temSub = !string.IsNullOrEmpty(Subtext);
            var areaTexto = temSub
                ? new RectangleF(10, 8, Width - 20, Height - 28)
                : new RectangleF(10, 0, Width - 20, Height);
            Theme.TextCentered(g, Text, Theme.ButtonFont, tinta, areaTexto);

            if (temSub)
                Theme.TextCentered(g, Subtext, Theme.Tiny,
                    Color.FromArgb(Enabled ? 170 : 90, tinta),
                    new RectangleF(10, Height - 24, Width - 20, 18));
        }

        static Color Shift(Color c, int d)
        {
            return Color.FromArgb(c.A,
                Math.Max(0, Math.Min(255, c.R + d)),
                Math.Max(0, Math.Min(255, c.G + d / 2)),
                Math.Max(0, Math.Min(255, c.B + d / 2)));
        }
    }

    // =====================================================================
    //  GLYPH BUTTON — the quiet secondary button.
    // =====================================================================
    internal sealed class GlyphButton : PaintedControl
    {
        public string Text = "";
        public Color Accent = Theme.Gold;
        bool _hover, _pressionado;

        public GlyphButton() { Height = 32; Cursor = Cursors.Hand; }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressionado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fundo = _pressionado ? Theme.Panel : (_hover ? Theme.PanelHi : Theme.Panel);
            Color borda = Enabled && _hover ? Color.FromArgb(150, Accent) : Theme.Line;
            Color tinta = !Enabled ? Theme.Dust : (_hover ? Accent : Theme.BoneDim);

            Theme.Card(g, r, fundo, borda, 2);
            Theme.TextCentered(g, Text, Theme.BodyBold, tinta, new RectangleF(6, 0, Width - 12, Height));
        }
    }

    // =====================================================================
    //  CHIP SELECTOR — choice by clickable chips, everything visible at once.
    //  Better than a dropdown: you can see every option and how good
    //  each one is without opening anything.
    // =====================================================================
    internal sealed class Chip
    {
        public int Value;
        public string Label = "";
        public string Note = "";        // linha miuda embaixo (ex.: "PERFEITO")
        public Color NoteColor = Theme.Dust;
        public bool Highlight;           // desenha um contorno de recomendacao
    }

    internal sealed class ChipSelector : PaintedControl
    {
        public List<Chip> Chips = new List<Chip>();
        public int Selected = -1;
        public int ChipHeight = 42;
        public int MinWidth = 62;
        public event EventHandler Picked;

        int _hover = -1;
        readonly List<Rectangle> _boxes = new List<Rectangle>();

        // A measuring ruler independent of the window: CreateGraphics() would require the
        // control to already have a handle, and layout happens before that.
        static readonly Bitmap _ruler = new Bitmap(1, 1);
        static readonly Graphics _textRuler = Graphics.FromImage(_ruler);

        public ChipSelector() { Height = 46; }

        public void Set(IEnumerable<Chip> fichas, int selecionado)
        {
            Chips = fichas.ToList();
            Selected = selecionado;
            Relayout();
            Invalidate();
        }

        /// <summary>Recalcula as areas e a altura necessaria (quebra em varias linhas).</summary>
        public void Relayout()
        {
            _boxes.Clear();
            if (Chips.Count == 0) { Height = ChipHeight; return; }

            int esp = 5, x = 0, y = 0, linhaAltura = ChipHeight;
            var g = _textRuler;
            foreach (var f in Chips)
            {
                int larg = MinWidth;
                SizeF tr = g.MeasureString(f.Label, Theme.DataMid);
                larg = Math.Max(larg, (int)tr.Width + 18);
                if (!string.IsNullOrEmpty(f.Note))
                {
                    SizeF tn = g.MeasureString(f.Note, Theme.Tiny);
                    larg = Math.Max(larg, (int)tn.Width + 14);
                }
                if (x > 0 && x + larg > Width) { x = 0; y += linhaAltura + esp; }
                _boxes.Add(new Rectangle(x, y, larg, linhaAltura));
                x += larg + esp;
            }

            int necessaria = y + linhaAltura;
            if (Height != necessaria) Height = necessaria;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); Invalidate(); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int antes = _hover;
            _hover = -1;
            for (int i = 0; i < _boxes.Count; i++)
                if (_boxes[i].Contains(e.Location)) { _hover = i; break; }
            if (antes != _hover) Invalidate();
            Cursor = _hover >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            for (int i = 0; i < _boxes.Count; i++)
                if (_boxes[i].Contains(e.Location))
                {
                    if (Selected != i)
                    {
                        Selected = i;
                        Invalidate();
                        if (Picked != null) Picked(this, EventArgs.Empty);
                    }
                    break;
                }
            base.OnMouseDown(e);
        }

        public Chip SelectedChip
        {
            get { return (Selected >= 0 && Selected < Chips.Count) ? Chips[Selected] : null; }
        }

        public bool SelectValue(int valor)
        {
            for (int i = 0; i < Chips.Count; i++)
                if (Chips[i].Value == valor) { Selected = i; Invalidate(); return true; }
            return false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            if (_boxes.Count != Chips.Count) Relayout();

            for (int i = 0; i < Chips.Count && i < _boxes.Count; i++)
            {
                var f = Chips[i];
                var r = _boxes[i];
                bool sel = (i == Selected);
                bool sobre = (i == _hover);

                Color fundo, borda, tinta;
                if (!Enabled)
                {
                    // while active the choices are frozen: they have to look frozen
                    fundo = Color.FromArgb(255, 17, 20, 18);
                    borda = sel ? Color.FromArgb(110, Theme.Crimson) : Theme.Line;
                    tinta = sel ? Theme.BoneDim : Theme.Dust;
                }
                else if (sel)
                {
                    fundo = Color.FromArgb(255, 58, 20, 22);
                    borda = Theme.CrimsonBright;
                    tinta = Color.FromArgb(255, 245, 232, 210);
                }
                else
                {
                    fundo = sobre ? Theme.PanelHi : Theme.Panel;
                    borda = sobre ? Color.FromArgb(150, Theme.Gold) : Theme.Line;
                    tinta = sobre ? Theme.Bone : Theme.BoneDim;
                }

                Theme.Card(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), fundo, borda, 2);

                if (f.Highlight && !sel && Enabled)
                    using (var caneta = new Pen(Color.FromArgb(120, Theme.Moss)) { DashStyle = DashStyle.Dot })
                    using (var caminho = Theme.RoundedRect(new Rectangle(r.X + 2, r.Y + 2, r.Width - 5, r.Height - 5), 2))
                        g.DrawPath(caneta, caminho);

                bool temNota = !string.IsNullOrEmpty(f.Note);
                var areaRot = temNota
                    ? new RectangleF(r.X + 2, r.Y + 3, r.Width - 4, r.Height - 17)
                    : new RectangleF(r.X + 2, r.Y, r.Width - 4, r.Height);
                Theme.TextCentered(g, f.Label, Theme.DataMid, tinta, areaRot);

                if (temNota)
                    Theme.TextCentered(g, f.Note, Theme.Tiny,
                        !Enabled ? Theme.Dust : (sel ? f.NoteColor : Color.FromArgb(190, f.NoteColor)),
                        new RectangleF(r.X + 2, r.Bottom - 16, r.Width - 4, 13));
            }
        }
    }

    // =====================================================================
    //  TOGGLE — one adjustment row: name, explanation and state.
    // =====================================================================
    internal sealed class Toggle : PaintedControl
    {
        public string Title = "";
        public string Description = "";
        public string State = "";
        public Color StateColor = Theme.Dust;
        public bool On;
        public bool Unavailable;
        public string UnavailableReason = "";
        public event EventHandler Toggled;

        bool _hover;

        public Toggle() { Height = 52; Cursor = Cursors.Hand; }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { _hover = false; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !Unavailable)
            {
                On = !On;
                Invalidate();
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            // "Unavailable" = the adjustment does not apply to this machine.
            // "!Enabled"      = something is active and nothing may change right now.
            bool apagado = Unavailable || !Enabled;

            Color fundo = apagado ? Color.FromArgb(255, 17, 20, 18)
                                  : (_hover ? Theme.PanelHi : Theme.Panel);
            Color borda = On && !Unavailable ? Color.FromArgb(140, Theme.Crimson) : Theme.Line;
            Theme.Card(g, r, fundo, borda, 2);

            // seal: a circle that lights up when the adjustment is on
            int cs = 18, cx = 14, cy = Height / 2;
            var selo = new Rectangle(cx - cs / 2, cy - cs / 2, cs, cs);
            using (var caneta = new Pen(apagado ? Theme.Line
                                       : (On ? Theme.CrimsonBright : Theme.Dust), 1.6f))
                g.DrawEllipse(caneta, selo);

            if (On && !Unavailable)
            {
                using (var pincel = new SolidBrush(Theme.CrimsonBright))
                    g.FillEllipse(pincel, Rectangle.Inflate(selo, -5, -5));
                using (var caneta = new Pen(Color.FromArgb(70, Theme.Ember), 1.2f))
                    g.DrawEllipse(caneta, Rectangle.Inflate(selo, 3, 3));
            }

            Color tintaTitulo = apagado ? Theme.Dust : (On ? Theme.Bone : Theme.BoneDim);
            Theme.Text(g, Title, Theme.BodyBold, tintaTitulo, 32, 7);

            string baixo = Unavailable && !string.IsNullOrEmpty(UnavailableReason)
                         ? UnavailableReason : Description;
            using (var fmt = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var pincel = new SolidBrush(Unavailable
                       ? Color.FromArgb(255, 88, 74, 66) : Theme.Dust))
                g.DrawString(baixo, Theme.Tiny, pincel,
                    new RectangleF(32, 27, Width - 130, 18), fmt);

            if (!string.IsNullOrEmpty(State) && !Unavailable)
            {
                var area = new RectangleF(Width - 98, 0, 90, Height);
                using (var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                using (var pincel = new SolidBrush(StateColor))
                    g.DrawString(State, Theme.Tiny, pincel, area, fmt);
            }
        }
    }

    // =====================================================================
    //  LOG PANEL — the running record of what happened.
    // =====================================================================
    internal sealed class LogPanel : PaintedControl
    {
        public sealed class Entry
        {
            public DateTime When;
            public string Text = "";
            public Color Color;
        }

        readonly List<Entry> _lines = new List<Entry>();
        const int Maximo = 300;
        int _rolagem;             // linhas escondidas no topo

        public LogPanel() { }

        public void Write(string texto, Color cor)
        {
            lock (_lines)
            {
                _lines.Add(new Entry { When = DateTime.Now, Text = texto, Color = cor });
                while (_lines.Count > Maximo) _lines.RemoveAt(0);
                _rolagem = 0;     // sempre volta para o fim ao registrar algo novo
            }
            if (IsHandleCreated)
            {
                try { BeginInvoke((MethodInvoker)Invalidate); }
                catch (InvalidOperationException) { }
            }
        }

        public void Clear() { lock (_lines) _lines.Clear(); Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int visiveis = Math.Max(1, (Height - 14) / 17);
            lock (_lines)
            {
                int maximoRolagem = Math.Max(0, _lines.Count - visiveis);
                _rolagem = Math.Max(0, Math.Min(maximoRolagem, _rolagem + (e.Delta > 0 ? 1 : -1) * 2));
            }
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseEnter(EventArgs e) { Focus(); base.OnMouseEnter(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.Card(g, r, Theme.Abyss, Theme.Line, 3);

            Entry[] copia;
            lock (_lines) copia = _lines.ToArray();

            if (copia.Length == 0)
            {
                Theme.Text(g, Txt.HistVazio, Theme.Tiny, Theme.Dust, 10, 10);
                return;
            }

            int alturaLinha = 17;
            int visiveis = Math.Max(1, (Height - 12) / alturaLinha);
            int fim = Math.Max(0, copia.Length - _rolagem);
            int ini = Math.Max(0, fim - visiveis);

            int y = 7;
            using (var fmt = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
                for (int i = ini; i < fim; i++)
                {
                    var linha = copia[i];
                    using (var pincel = new SolidBrush(Color.FromArgb(120, Theme.Dust)))
                        g.DrawString(linha.When.ToString("HH:mm:ss"), Theme.Tiny, pincel, 9, y);
                    using (var pincel = new SolidBrush(linha.Color))
                        g.DrawString(linha.Text, Theme.Tiny, pincel,
                                     new RectangleF(63, y, Width - 72, alturaLinha), fmt);
                    y += alturaLinha;
                }

            if (_rolagem > 0)
                using (var pincel = new SolidBrush(Color.FromArgb(200, Theme.Amber)))
                    g.DrawString("▼ " + _rolagem, Theme.Tiny, pincel, Width - 46, Height - 19);
        }
    }
}
