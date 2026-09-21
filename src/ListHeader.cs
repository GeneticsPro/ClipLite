using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipLite
{
    internal enum SortColumn { Date, Title }

    /// <summary>
    /// The clip list's column header. A click sorts by that column; clicking the column that is
    /// already active flips the direction. Drawn by hand — a real ListView header cannot be
    /// themed dark without owner-drawing it anyway.
    /// </summary>
    internal class ListHeader : Control
    {
        public event EventHandler SortChanged;

        readonly float scale;
        SortColumn column = SortColumn.Date;
        bool ascending;
        int hot = -1;

        public ListHeader(float scale)
        {
            this.scale = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.TextDim;
        }

        public SortColumn Column { get { return column; } }
        public bool Ascending { get { return ascending; } }

        /// <summary>Width of the date column, in pixels.</summary>
        public int DateWidth { get { return D(104); } }

        int D(int px) { return (int)Math.Round(px * scale); }

        public void SetSort(SortColumn col, bool asc)
        {
            column = col;
            ascending = asc;
            Invalidate();
        }

        /// <summary>New clips first for dates, A→Z for titles.</summary>
        public static bool DefaultAscending(SortColumn col) { return col == SortColumn.Title; }

        int HitTest(int x) { return x >= Width - DateWidth ? 1 : 0; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.X);
            if (h == hot) return;
            hot = h;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hot = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            var clicked = HitTest(e.X) == 1 ? SortColumn.Date : SortColumn.Title;
            if (clicked == column) ascending = !ascending;
            else { column = clicked; ascending = DefaultAscending(clicked); }
            Invalidate();
            if (SortChanged != null) SortChanged(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Theme.Bg)) g.FillRectangle(b, ClientRectangle);

            int split = Width - DateWidth;
            var titleRect = new Rectangle(0, 0, split, Height);
            var dateRect = new Rectangle(split, 0, DateWidth, Height);
            if (hot >= 0)
                using (var b = new SolidBrush(Theme.Hover))
                    g.FillRectangle(b, hot == 1 ? dateRect : titleRect);

            using (var p = new Pen(Theme.Border))
            {
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);
                g.DrawLine(p, split, D(4), split, Height - D(5));
            }

            const TextFormatFlags left = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int arrow = D(14);
            DrawColumn(g, Loc.T("Title"), new Rectangle(D(28), 0, split - D(28) - arrow, Height), SortColumn.Title, left);
            DrawColumn(g, Loc.T("Date"), new Rectangle(split + D(8), 0, DateWidth - D(8) - arrow, Height), SortColumn.Date, left);
        }

        void DrawColumn(Graphics g, string text, Rectangle rect, SortColumn col, TextFormatFlags flags)
        {
            bool active = col == column;
            TextRenderer.DrawText(g, text, Font, rect, active ? Theme.Text : Theme.TextDim, flags);
            if (!active) return;
            int w = TextRenderer.MeasureText(g, text, Font, new Size(rect.Width, Height), flags).Width;
            DrawArrow(g, Math.Min(rect.Left + w, rect.Right) + D(6), Height / 2, ascending);
        }

        void DrawArrow(Graphics g, int cx, int cy, bool up)
        {
            int w = D(4), h = D(3);
            var pts = up
                ? new[] { new Point(cx - w, cy + h), new Point(cx + w, cy + h), new Point(cx, cy - h) }
                : new[] { new Point(cx - w, cy - h), new Point(cx + w, cy - h), new Point(cx, cy + h) };
            var old = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Accent)) g.FillPolygon(b, pts);
            g.SmoothingMode = old;
        }
    }
}
