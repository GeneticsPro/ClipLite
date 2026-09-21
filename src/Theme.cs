using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ClipLite
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(30, 31, 34);
        public static readonly Color Panel = Color.FromArgb(37, 38, 42);
        public static readonly Color Surface = Color.FromArgb(24, 25, 28);
        public static readonly Color Border = Color.FromArgb(52, 54, 60);
        public static readonly Color Text = Color.FromArgb(222, 224, 228);
        public static readonly Color TextDim = Color.FromArgb(140, 144, 152);
        public static readonly Color Accent = Color.FromArgb(78, 140, 255);
        public static readonly Color Selection = Color.FromArgb(44, 74, 128);
        public static readonly Color SelectionInactive = Color.FromArgb(55, 58, 66);
        public static readonly Color Hover = Color.FromArgb(46, 48, 54);
        public static readonly Color Danger = Color.FromArgb(235, 100, 90);

        public static Font UiFont = new Font("Segoe UI", 9f);
        public static Font MonoFont = new Font("Consolas", 10f);

        // ---------- icons ----------
        public static Bitmap Icon(string kind, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float s = size / 16f;
                g.ScaleTransform(s, s);
                switch (kind)
                {
                    case "root":
                        using (var b = new SolidBrush(Color.FromArgb(120, 130, 150))) g.FillRectangle(b, 1, 5, 14, 7);
                        using (var b = new SolidBrush(Color.FromArgb(170, 180, 200))) g.FillRectangle(b, 2, 6, 9, 5);
                        break;
                    case "inbox":
                        using (var b = new SolidBrush(Color.FromArgb(232, 180, 70))) g.FillPolygon(b, new[] { new PointF(1, 4), new PointF(6, 4), new PointF(7.5f, 5.5f), new PointF(15, 5.5f), new PointF(15, 14), new PointF(1, 14) });
                        using (var p = new Pen(Color.FromArgb(40, 30, 10), 1.4f)) { g.DrawLine(p, 8, 7, 8, 12); g.DrawLine(p, 5.5f, 9.5f, 8, 12); g.DrawLine(p, 10.5f, 9.5f, 8, 12); }
                        break;
                    case "trash":
                        using (var p = new Pen(Color.FromArgb(170, 176, 186), 1.3f))
                        {
                            g.DrawLine(p, 2, 4, 14, 4); g.DrawLine(p, 6, 2, 10, 2);
                            g.DrawPolygon(p, new[] { new PointF(3.5f, 4), new PointF(12.5f, 4), new PointF(11.5f, 14.5f), new PointF(4.5f, 14.5f) });
                            g.DrawLine(p, 6.5f, 6.5f, 6.5f, 12.5f); g.DrawLine(p, 9.5f, 6.5f, 9.5f, 12.5f);
                        }
                        break;
                    case "search":
                        using (var p = new Pen(Color.FromArgb(120, 180, 255), 1.8f)) { g.DrawEllipse(p, 2, 2, 8.5f, 8.5f); g.DrawLine(p, 9.5f, 9.5f, 14, 14); }
                        break;
                    case "safe": // a vault: dark door, dial and handle (kept chunky to survive 16 px)
                        using (var b = new SolidBrush(Color.FromArgb(96, 170, 130))) g.FillRectangle(b, 1.5f, 2.5f, 13, 11);
                        using (var b = new SolidBrush(Color.FromArgb(24, 52, 40))) g.FillRectangle(b, 3.5f, 4.5f, 9, 7);
                        using (var b = new SolidBrush(Color.FromArgb(205, 242, 220))) g.FillEllipse(b, 5.6f, 6.2f, 3.8f, 3.8f);
                        using (var p = new Pen(Color.FromArgb(205, 242, 220), 1.4f)) g.DrawLine(p, 10.2f, 8.1f, 11.8f, 8.1f);
                        break;
                    case "text":
                        DocBase(g, Color.FromArgb(200, 204, 212));
                        using (var p = new Pen(Color.FromArgb(90, 94, 104), 1f)) { g.DrawLine(p, 5, 6, 11, 6); g.DrawLine(p, 5, 8.5f, 11, 8.5f); g.DrawLine(p, 5, 11, 9, 11); }
                        break;
                    case "rich":
                        DocBase(g, Color.FromArgb(200, 204, 212));
                        using (var p = new Pen(Color.FromArgb(78, 140, 255), 1.3f)) { g.DrawLine(p, 5, 6, 11, 6); g.DrawLine(p, 5, 8.5f, 11, 8.5f); }
                        using (var p = new Pen(Color.FromArgb(90, 94, 104), 1f)) g.DrawLine(p, 5, 11, 9, 11);
                        break;
                    case "image":
                        using (var b = new SolidBrush(Color.FromArgb(80, 150, 120))) g.FillRectangle(b, 1.5f, 3, 13, 10);
                        using (var b = new SolidBrush(Color.FromArgb(230, 220, 120))) g.FillEllipse(b, 3.5f, 4.5f, 3, 3);
                        using (var b = new SolidBrush(Color.FromArgb(40, 90, 70))) g.FillPolygon(b, new[] { new PointF(1.5f, 13), new PointF(6, 8), new PointF(9, 11), new PointF(11, 9), new PointF(14.5f, 13) });
                        break;
                    case "files":
                        using (var b = new SolidBrush(Color.FromArgb(232, 180, 70))) g.FillRectangle(b, 1, 5, 14, 9);
                        using (var b = new SolidBrush(Color.FromArgb(200, 150, 50))) g.FillRectangle(b, 1, 3, 6, 3);
                        break;
                    case "paste":
                        using (var b = new SolidBrush(Color.FromArgb(150, 120, 90))) g.FillRectangle(b, 3, 2.5f, 10, 12);
                        using (var b = new SolidBrush(Color.FromArgb(220, 224, 230))) g.FillRectangle(b, 5, 5, 6, 8);
                        using (var b = new SolidBrush(Color.FromArgb(170, 176, 186))) g.FillRectangle(b, 6, 1.5f, 4, 2.5f);
                        break;
                    case "copy":
                        using (var p = new Pen(Color.FromArgb(190, 196, 206), 1.3f)) { g.DrawRectangle(p, 5, 5, 8.5f, 9.5f); g.DrawLines(p, new[] { new PointF(3, 11), new PointF(2.5f, 11), new PointF(2.5f, 2), new PointF(10.5f, 2), new PointF(10.5f, 3) }); }
                        break;
                    case "save":
                        using (var b = new SolidBrush(Color.FromArgb(78, 140, 255))) g.FillRectangle(b, 2, 2, 12, 12);
                        using (var b = new SolidBrush(Color.FromArgb(230, 235, 245))) { g.FillRectangle(b, 4.5f, 2.5f, 7, 4); g.FillRectangle(b, 4, 9, 8, 4.5f); }
                        break;
                    case "wrap":
                        using (var p = new Pen(Color.FromArgb(190, 196, 206), 1.3f))
                        {
                            g.DrawLine(p, 2, 4, 14, 4);
                            g.DrawArc(p, 9, 6.5f, 5, 5, -90, 180);
                            g.DrawLine(p, 2, 8, 11.5f, 8); g.DrawLine(p, 11.5f, 11.5f, 7, 11.5f); g.DrawLine(p, 2, 12.5f, 4.5f, 12.5f);
                            g.DrawLine(p, 7, 11.5f, 8.8f, 9.8f); g.DrawLine(p, 7, 11.5f, 8.8f, 13.2f);
                        }
                        break;
                    case "delete":
                        using (var p = new Pen(Color.FromArgb(235, 100, 90), 2f)) { g.DrawLine(p, 4, 4, 12, 12); g.DrawLine(p, 12, 4, 4, 12); }
                        break;
                    case "options": // a gear: a 16-point star with the hub punched out
                        using (var path = new GraphicsPath())
                        {
                            var teeth = new PointF[16];
                            for (int i = 0; i < teeth.Length; i++)
                            {
                                double a = Math.PI * i / 8;
                                float r = i % 2 == 0 ? 7.2f : 5.1f;
                                teeth[i] = new PointF(8 + (float)(r * Math.Cos(a)), 8 + (float)(r * Math.Sin(a)));
                            }
                            path.AddPolygon(teeth);
                            path.AddEllipse(5.7f, 5.7f, 4.6f, 4.6f);
                            using (var b = new SolidBrush(Color.FromArgb(190, 196, 206))) g.FillPath(b, path);
                        }
                        break;
                    case "case": // a big "A" next to a small "a"
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        using (var big = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var small = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            var fmt = StringFormat.GenericTypographic;
                            using (var b = new SolidBrush(Color.FromArgb(220, 224, 230))) g.DrawString("A", big, b, 0f, 1.5f, fmt);
                            using (var b = new SolidBrush(Color.FromArgb(120, 180, 255))) g.DrawString("a", small, b, 7.4f, 4f, fmt);
                        }
                        break;
                }
            }
            return bmp;
        }

        static void DocBase(Graphics g, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillPolygon(b, new[] { new PointF(3, 1.5f), new PointF(10, 1.5f), new PointF(13, 4.5f), new PointF(13, 14.5f), new PointF(3, 14.5f) });
        }

        public static Icon AppIcon()
        {
            return BuildIcon(DrawAppGlyph, 16, 20, 24, 32, 48);
        }

        /// <summary>
        /// The notification-area icon: amber and edge-to-edge, so it stays readable among the
        /// mostly blue-grey system icons at 16 px.
        /// </summary>
        public static Icon TrayIcon()
        {
            return BuildIcon(DrawTrayGlyph, 16, 20, 24, 32);
        }

        static void DrawAppGlyph(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(78, 140, 255))) g.FillRectangle(b, 6, 5, 20, 25);
            using (var b = new SolidBrush(Color.FromArgb(235, 240, 250))) g.FillRectangle(b, 9, 10, 14, 17);
            using (var b = new SolidBrush(Color.FromArgb(40, 50, 70))) g.FillRectangle(b, 11, 2, 10, 6);
            using (var p = new Pen(Color.FromArgb(78, 140, 255), 2f)) { g.DrawLine(p, 12, 15, 20, 15); g.DrawLine(p, 12, 19, 20, 19); g.DrawLine(p, 12, 23, 17, 23); }
        }

        static void DrawTrayGlyph(Graphics g)
        {
            var amber = Color.FromArgb(255, 196, 40);
            var dark = Color.FromArgb(38, 30, 6);
            using (var b = new SolidBrush(amber)) g.FillRectangle(b, 1, 3, 30, 28);          // body, edge to edge
            using (var b = new SolidBrush(dark)) g.FillRectangle(b, 9, 0, 14, 7);            // the clip on top
            using (var p = new Pen(dark, 3.4f))                                             // three heavy text lines
            {
                g.DrawLine(p, 7, 13, 25, 13);
                g.DrawLine(p, 7, 20, 25, 20);
                g.DrawLine(p, 7, 27, 18, 27);
            }
        }

        /// <summary>
        /// Builds a real multi-resolution icon. Letting Windows shrink one 32 px bitmap to tray
        /// size smears it, so every size is drawn at its own scale.
        /// </summary>
        static Icon BuildIcon(Action<Graphics> draw, params int[] sizes)
        {
            var images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++) images[i] = Dib(draw, sizes[i]);

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((short)0);              // reserved
                w.Write((short)1);              // type: icon
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);           // palette size
                    w.Write((byte)0);           // reserved
                    w.Write((short)1);          // planes
                    w.Write((short)32);         // bits per pixel
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
                ms.Position = 0;
                return new Icon(ms);
            }
        }

        /// <summary>One icon image: BITMAPINFOHEADER + bottom-up BGRA pixels + an empty AND mask.</summary>
        static byte[] Dib(Action<Graphics> draw, int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(size / 32f, size / 32f); // every glyph is drawn in a 32x32 space
                    draw(g);
                }
                int maskStride = ((size + 31) / 32) * 4;
                using (var ms = new MemoryStream())
                using (var w = new BinaryWriter(ms))
                {
                    w.Write(40);                    // biSize
                    w.Write(size);                  // biWidth
                    w.Write(size * 2);              // biHeight: colour rows + mask rows
                    w.Write((short)1);              // biPlanes
                    w.Write((short)32);             // biBitCount
                    w.Write(0);                     // biCompression: BI_RGB
                    w.Write(size * size * 4 + maskStride * size);
                    w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                    for (int y = size - 1; y >= 0; y--)     // bottom-up
                        for (int x = 0; x < size; x++)
                        {
                            var c = bmp.GetPixel(x, y);
                            w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                        }
                    w.Write(new byte[maskStride * size]);   // alpha already carries transparency
                    return ms.ToArray();
                }
            }
        }

        // ---------- menus ----------
        public class MenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemBorder { get { return Border; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color ToolStripDropDownBackground { get { return Panel; } }
            public override Color ImageMarginGradientBegin { get { return Panel; } }
            public override Color ImageMarginGradientMiddle { get { return Panel; } }
            public override Color ImageMarginGradientEnd { get { return Panel; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Panel; } }
            public override Color ToolStripBorder { get { return Bg; } }
            public override Color ToolStripGradientBegin { get { return Bg; } }
            public override Color ToolStripGradientMiddle { get { return Bg; } }
            public override Color ToolStripGradientEnd { get { return Bg; } }
            public override Color ButtonSelectedHighlight { get { return Hover; } }
            public override Color ButtonSelectedGradientBegin { get { return Hover; } }
            public override Color ButtonSelectedGradientMiddle { get { return Hover; } }
            public override Color ButtonSelectedGradientEnd { get { return Hover; } }
            public override Color ButtonSelectedBorder { get { return Border; } }
            public override Color ButtonPressedGradientBegin { get { return Selection; } }
            public override Color ButtonPressedGradientMiddle { get { return Selection; } }
            public override Color ButtonPressedGradientEnd { get { return Selection; } }
            public override Color ButtonCheckedGradientBegin { get { return Selection; } }
            public override Color ButtonCheckedGradientMiddle { get { return Selection; } }
            public override Color ButtonCheckedGradientEnd { get { return Selection; } }
            public override Color CheckBackground { get { return Selection; } }
            public override Color CheckSelectedBackground { get { return Selection; } }
            public override Color CheckPressedBackground { get { return Selection; } }
        }

        public class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new MenuColors()) { RoundedEdges = false; }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Text : TextDim;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = TextDim;
                base.OnRenderArrow(e);
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                var r = e.ImageRectangle;
                using (var b = new SolidBrush(Selection)) e.Graphics.FillRectangle(b, r);
                using (var p = new Pen(Text, 1.6f))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawLines(p, new[] { new PointF(r.Left + r.Width * .25f, r.Top + r.Height * .52f), new PointF(r.Left + r.Width * .43f, r.Top + r.Height * .7f), new PointF(r.Left + r.Width * .76f, r.Top + r.Height * .32f) });
                }
            }
        }

        public static void Apply(ToolStrip ts)
        {
            ts.Renderer = new DarkRenderer();
            ts.BackColor = Bg;
            ts.ForeColor = Text;
        }
    }
}
