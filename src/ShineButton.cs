using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AngelsFixRes
{
    // Primary action button: a rounded gradient fill with a light "shine" that sweeps across on
    // hover (slow, elegant) and a brighter multi-sweep on apply (Flash). Owner-drawn - a GDI+
    // gradient plus a diagonal translucent band clipped to the rounded shape. The animation timer
    // runs only while it has something to show, so it costs nothing at rest.
    internal sealed class ShineButton : Button
    {
        public string Label = "";
        public int CornerRadius = 0;   // square corners, to match the other buttons
        readonly Color topColor, bottomColor, parentBg;
        readonly Timer timer;
        float shine = -1f;      // sweep position; the band is drawn only while this is in [0,1]
        int applyPulses;
        bool hover, pressed, flashing;
        const float HoverSpeed = 0.03f;   // slow, elegant sweep on hover
        const float FlashSpeed = 0.06f;   // snappier celebratory sweep on apply

        public ShineButton(Color top, Color bottom, Color parent)
        {
            topColor = top; bottomColor = bottom; parentBg = parent;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            Text = ""; UseVisualStyleBackColor = false; BackColor = parent;
            timer = new Timer(); timer.Interval = 16; timer.Tick += OnTick;
            MouseEnter += delegate { hover = true; Kick(); };
            MouseLeave += delegate { hover = false; pressed = false; Invalidate(); };
            MouseDown += delegate { pressed = true; Invalidate(); };
            MouseUp += delegate { pressed = false; Invalidate(); };
        }

        // Celebratory sweep when the fix is applied.
        public void Flash() { flashing = true; applyPulses = 2; Kick(); }

        void Kick() { if (!timer.Enabled) { shine = -0.15f; timer.Start(); } }

        void OnTick(object s, EventArgs e)
        {
            shine += flashing ? FlashSpeed : HoverSpeed;
            if (shine > 1.25f)
            {
                if (applyPulses > 0) { applyPulses--; shine = -0.25f; }
                else if (hover) { shine = -0.25f; flashing = false; }
                else { timer.Stop(); shine = -1f; flashing = false; }
            }
            Invalidate();
        }

        static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            if (rad <= 0) { p.AddRectangle(r); return p; }   // square corners
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(parentBg)) g.FillRectangle(bg, ClientRectangle);
            // Square fills edge-to-edge like the other buttons; a rounded radius insets 1px for AA.
            Rectangle r = CornerRadius > 0 ? new Rectangle(0, 0, Width - 1, Height - 1) : ClientRectangle;
            using (var path = Rounded(r, CornerRadius))
            {
                float amt = pressed ? -0.10f : (hover ? 0.14f : 0f);
                Color t = Shift(topColor, amt), b = Shift(bottomColor, amt);
                using (var lg = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), t, b, 90f))
                    g.FillPath(lg, path);
                // top gloss and the sweeping shine, both clipped to the button shape
                GraphicsState top = g.Save();
                g.SetClip(path);
                using (var gloss = new SolidBrush(Color.FromArgb(hover ? 46 : 30, 255, 255, 255)))
                    g.FillRectangle(gloss, 0, 0, Width, Height / 2);
                if (shine >= 0f && shine <= 1f) DrawShine(g, shine);
                g.Restore(top);
                if (CornerRadius > 0)
                    using (var edge = new Pen(Color.FromArgb(90, 255, 255, 255))) g.DrawPath(edge, path);
            }
            TextRenderer.DrawText(g, Label, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        void DrawShine(Graphics g, float s)
        {
            float bandW = Width * 0.34f;
            float x = s * (Width + bandW) - bandW * 0.5f;   // sweep center, enters left, exits right
            float skew = Height;                             // 45-degree lean
            PointF[] pts = {
                new PointF(x, 0), new PointF(x + bandW, 0),
                new PointF(x + bandW - skew, Height), new PointF(x - skew, Height)
            };
            using (var band = new GraphicsPath())
            using (var lb = new LinearGradientBrush(new PointF(x - skew, 0), new PointF(x + bandW, 0), Color.White, Color.White))
            {
                band.AddPolygon(pts);
                var blend = new ColorBlend(3);
                blend.Colors = new Color[] {
                    Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                blend.Positions = new float[] { 0f, 0.5f, 1f };
                lb.InterpolationColors = blend;
                g.FillPath(lb, band);
            }
        }

        static Color Shift(Color c, float amt)
        {
            if (amt > 0f) return ControlPaint.Light(c, amt);
            if (amt < 0f) return ControlPaint.Dark(c, -amt);
            return c;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null) { timer.Stop(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
