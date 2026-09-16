// Dialogs.cs — picking the target window.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace OrdoCadentia
{
    /// <summary>A scrollable, hand-drawn list of windows.</summary>
    internal sealed class WindowList : PaintedControl
    {
        public List<WindowInfo> Items = new List<WindowInfo>();
        public int Selected = -1;
        public event EventHandler Picked;       // clique simples
        public event EventHandler Confirmed;      // clique duplo

        int _rolagem, _hover = -1;
        const int AlturaItem = 46;

        public WindowList() { }

        int Visible { get { return Math.Max(1, (Height - 8) / AlturaItem); } }
        int MaxScroll { get { return Math.Max(0, Items.Count - Visible); } }

        public void Set(List<WindowInfo> itens)
        {
            Items = itens ?? new List<WindowInfo>();
            Selected = Items.Count > 0 ? 0 : -1;
            _rolagem = 0;
            Invalidate();
        }

        public WindowInfo SelectedItem
        {
            get { return (Selected >= 0 && Selected < Items.Count) ? Items[Selected] : null; }
        }

        int IndexAtPoint(Point p)
        {
            if (p.Y < 4) return -1;
            int i = _rolagem + (p.Y - 4) / AlturaItem;
            return (i >= 0 && i < Items.Count && i < _rolagem + Visible) ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int antes = _hover;
            _hover = IndexAtPoint(e.Location);
            if (antes != _hover) Invalidate();
            Cursor = _hover >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            int i = e.Button == MouseButtons.Left ? IndexAtPoint(e.Location) : -1;
            if (i >= 0)
            {
                Selected = i;
                Invalidate();
                if (Picked != null) Picked(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (IndexAtPoint(e.Location) >= 0 && Confirmed != null) Confirmed(this, EventArgs.Empty);
            base.OnMouseDoubleClick(e);
        }

        // The mouse wheel only reaches the control that has focus.
        protected override void OnMouseEnter(EventArgs e) { Focus(); base.OnMouseEnter(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _rolagem = Math.Max(0, Math.Min(MaxScroll, _rolagem - Math.Sign(e.Delta) * 2));
            Invalidate();
            base.OnMouseWheel(e);
        }

        public void Move(int passo)
        {
            if (Items.Count == 0) return;
            Selected = Math.Max(0, Math.Min(Items.Count - 1, Selected + passo));
            if (Selected < _rolagem) _rolagem = Selected;
            else if (Selected >= _rolagem + Visible) _rolagem = Selected - Visible + 1;
            _rolagem = Math.Max(0, Math.Min(MaxScroll, _rolagem));
            Invalidate();
            if (Picked != null) Picked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.Card(g, r, Theme.Abyss, Theme.Line, 3);

            if (Items.Count == 0)
            {
                Theme.TextCentered(g, Txt.DlgVazio, Theme.Body, Theme.Dust,
                                 new RectangleF(0, 0, Width, Height));
                return;
            }

            int fim = Math.Min(Items.Count, _rolagem + Visible);
            int y = 4;

            using (var fmt = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
                for (int i = _rolagem; i < fim; i++)
                {
                    var j = Items[i];
                    var linha = new Rectangle(4, y, Width - 9, AlturaItem - 2);
                    bool sel = (i == Selected);

                    if (sel)
                    {
                        using (var pincel = new SolidBrush(Color.FromArgb(255, 52, 19, 21)))
                            g.FillRectangle(pincel, linha);
                        using (var caneta = new Pen(Theme.CrimsonBright)) g.DrawRectangle(caneta, linha);
                        using (var pincel = new SolidBrush(Theme.CrimsonBright))
                            g.FillRectangle(pincel, linha.X, linha.Y, 3, linha.Height);
                    }
                    else if (i == _hover)
                    {
                        using (var pincel = new SolidBrush(Theme.Panel)) g.FillRectangle(pincel, linha);
                    }

                    using (var pincel = new SolidBrush(sel ? Theme.Bone : Theme.BoneDim))
                        g.DrawString(j.Process, Theme.BodyBold, pincel,
                                     new RectangleF(linha.X + 10, linha.Y + 5, linha.Width - 130, 17), fmt);

                    using (var pincel = new SolidBrush(Theme.Dust))
                        g.DrawString(j.Title, Theme.Tiny, pincel,
                                     new RectangleF(linha.X + 10, linha.Y + 24, linha.Width - 130, 16), fmt);

                    using (var direita = new StringFormat
                    {
                        Alignment = StringAlignment.Far,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    })
                    using (var pincel = new SolidBrush(Theme.Dust))
                        g.DrawString(string.Format("{0}×{1}", j.Width, j.Height), Theme.Tiny, pincel,
                                     new RectangleF(linha.Right - 118, linha.Y, 110, linha.Height), direita);

                    y += AlturaItem;
                }

            // scrollbar
            if (MaxScroll > 0)
            {
                int trilhoX = Width - 6;
                using (var pincel = new SolidBrush(Color.FromArgb(60, Theme.Line)))
                    g.FillRectangle(pincel, trilhoX, 4, 3, Height - 8);
                float frac = (float)Visible / Items.Count;
                int alturaPolegar = Math.Max(16, (int)((Height - 8) * frac));
                int topo = 4 + (int)((Height - 8 - alturaPolegar) * (_rolagem / (float)MaxScroll));
                using (var pincel = new SolidBrush(Theme.Crimson))
                    g.FillRectangle(pincel, trilhoX, topo, 3, alturaPolegar);
            }
        }
    }

    // =====================================================================
    //  THE PICKER DIALOG
    // =====================================================================
    internal sealed class WindowPicker : Form
    {
        public WindowInfo Picked2;

        WindowList _list;
        PrimaryButton _btnOk;
        GlyphButton _btnRefresh, _btnCancel;
        Rectangle _titleBar;
        bool _dragging; Point _grab;

        public WindowPicker()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Theme.S(640), Theme.S(500));
            BackColor = Theme.Pitch;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            ShowInTaskbar = false;

            _titleBar = new Rectangle(0, 0, ClientSize.Width, Theme.S(38));

            _list = new WindowList
            {
                Bounds = new Rectangle(Theme.S(14), Theme.S(58), ClientSize.Width - Theme.S(28), Theme.S(356))
            };
            _list.Confirmed += (s, e) => Confirm();
            _list.Picked += (s, e) => Invalidate(new Rectangle(0, ClientSize.Height - Theme.S(86),
                                                                 ClientSize.Width, Theme.S(86)));
            Controls.Add(_list);

            _btnOk = new PrimaryButton
            {
                Text = Txt.DlgMarcarEsta,
                Bounds = new Rectangle(Theme.S(14), ClientSize.Height - Theme.S(64), Theme.S(220), Theme.S(50))
            };
            _btnOk.Click += (s, e) => Confirm();
            Controls.Add(_btnOk);

            _btnRefresh = new GlyphButton
            {
                Text = Txt.DlgAtualizar,
                Bounds = new Rectangle(_btnOk.Right + Theme.S(10), ClientSize.Height - Theme.S(56),
                                       Theme.S(150), Theme.S(34))
            };
            _btnRefresh.Click += (s, e) => Reload();
            Controls.Add(_btnRefresh);

            _btnCancel = new GlyphButton
            {
                Text = Txt.DlgCancelar,
                Bounds = new Rectangle(ClientSize.Width - Theme.S(134), ClientSize.Height - Theme.S(56),
                                       Theme.S(120), Theme.S(34))
            };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);

            Reload();
        }

        void Reload()
        {
            _list.Set(Target.List());
            Invalidate();
        }

        void Confirm()
        {
            var j = _list.SelectedItem;
            if (j == null) return;
            Picked2 = j;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var tudo = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (var pincel = new SolidBrush(Theme.Pitch)) g.FillRectangle(pincel, tudo);

            using (var pincel = new LinearGradientBrush(
                       new Rectangle(0, 0, ClientSize.Width, Math.Max(1, _titleBar.Height)),
                       Color.FromArgb(255, 24, 15, 15), Theme.Pitch, 90f))
                g.FillRectangle(pincel, _titleBar);

            int s = Theme.S(22);
            Theme.Sigil(g, new RectangleF(Theme.S(10), (_titleBar.Height - s) / 2f, s, s), 0.5f,
                        Theme.Crimson, false);
            Theme.Text(g, Txt.DlgTitulo, Theme.TitleSmall, Theme.Bone,
                       Theme.S(40), (_titleBar.Height - Theme.S(17)) / 2f);

            Theme.Text(g, Txt.DlgDica, Theme.Tiny, Theme.Dust, Theme.S(14), Theme.S(42));

            var sel = _list.SelectedItem;
            if (sel != null)
            {
                string caminho = string.IsNullOrEmpty(sel.ExePath)
                    ? Txt.DlgSemCaminho : sel.ExePath;
                using (var fmt = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisPath,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                using (var pincel = new SolidBrush(Theme.Dust))
                    g.DrawString(caminho, Theme.Tiny, pincel,
                        new RectangleF(Theme.S(14), Theme.S(420), ClientSize.Width - Theme.S(28), Theme.S(18)), fmt);
            }

            Theme.Atmosphere(g, tudo, e.ClipRectangle);
            using (var caneta = new Pen(Color.FromArgb(255, 58, 26, 26)))
                g.DrawRectangle(caneta, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_titleBar.Contains(e.Location))
            {
                _dragging = true;
                _grab = e.Location;
                Capture = true;      // senao um arraste rapido solta a janela
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
                Location = new Point(Location.X + e.X - _grab.X, Location.Y + e.Y - _grab.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging) { _dragging = false; Capture = false; }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Confirm(); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { _list.Move(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { _list.Move(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.F5) { Reload(); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }
}
