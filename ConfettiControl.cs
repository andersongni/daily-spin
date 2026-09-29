using System.Drawing.Drawing2D;

namespace RoletaDaDaily;

internal sealed class ConfettiControl : Control
{
    private sealed class Particle
    {
        public float X, Y, Speed, Drift, Angle, Spin, Width, Height;
        public int ColorIndex;
    }

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Particle[] _particles = new Particle[180];
    private SolidBrush[] _brushes = [];
    private Color[] _colors = [];
    private ThemePalette _palette = ThemePalette.All[0];
    private readonly Random _random = Random.Shared;

    public ConfettiControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
        for (int i = 0; i < _particles.Length; i++) _particles[i] = NewParticle(true);
        _timer.Tick += (_, _) => { UpdateParticles(); Invalidate(); };
        SetPalette(ThemePalette.All[0]);
    }

    public string Winner { get; set; } = string.Empty;
    public bool LightMode { get; set; }
    public ThemePalette Palette { get => _palette; set => SetPalette(value); }
    public void StartAnimation() => _timer.Start();
    public void StopAnimation() => _timer.Stop();

    private void SetPalette(ThemePalette palette)
    {
        _palette = palette;
        foreach (SolidBrush brush in _brushes) brush.Dispose();
        _colors = palette.Confetti;
        _brushes = _colors.Select(c => new SolidBrush(c)).ToArray();
        for (int i = 0; i < _particles.Length; i++) _particles[i].ColorIndex = i % _colors.Length;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle client = ClientRectangle;
        Color top = LightMode ? Color.FromArgb(242, 239, 250) : Color.FromArgb(34, 27, 53);
        Color bottom = LightMode ? Color.FromArgb(222, 230, 241) : Color.FromArgb(12, 17, 30);
        using (var background = new LinearGradientBrush(client, top, bottom, LinearGradientMode.Vertical))
            g.FillRectangle(background, client);
        RectangleF glowBounds = new(client.Width * 0.18f, client.Height * 0.06f, client.Width * 0.64f, client.Height * 0.64f);
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(glowBounds);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(LightMode ? 58 : 62, _palette.RingStart),
                SurroundColors = [Color.FromArgb(0, _palette.RingStart)]
            };
            g.FillPath(glow, glowPath);
        }

        foreach (Particle p in _particles)
        {
            var state = g.Save();
            g.TranslateTransform(p.X, p.Y);
            g.RotateTransform(p.Angle);
            g.FillRectangle(_brushes[p.ColorIndex], -p.Width / 2, -p.Height / 2, p.Width, p.Height);
            g.Restore(state);
        }
        DrawCelebration(g);
    }

    private void DrawCelebration(Graphics g)
    {
        float w = ClientSize.Width, h = ClientSize.Height;
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var badgeFont = new Font("Segoe UI Semibold", Math.Clamp(w * 0.012f, 10, 15), FontStyle.Bold, GraphicsUnit.Pixel);
        using var headingFont = new Font("Segoe UI Semibold", Math.Clamp(w * 0.029f, 20, 37), FontStyle.Bold, GraphicsUnit.Pixel);
        using var subFont = new Font("Segoe UI", Math.Clamp(w * 0.012f, 11, 16), FontStyle.Regular, GraphicsUnit.Pixel);
        Color foreground = LightMode ? Color.FromArgb(40, 39, 55) : Color.FromArgb(246, 241, 250);
        Color muted = LightMode ? Color.FromArgb(96, 91, 112) : Color.FromArgb(194, 185, 208);
        Color gold = LightMode ? Color.FromArgb(157, 102, 31) : Color.FromArgb(255, 220, 151);

        RectangleF badge = new(w * 0.38f, h * 0.12f, w * 0.24f, 34);
        using (var badgeFill = new SolidBrush(Color.FromArgb(LightMode ? 222 : 40, _palette.RingStart)))
        using (var badgeBorder = new Pen(Color.FromArgb(LightMode ? 125 : 95, _palette.RingStart), 1))
        using (GraphicsPath badgePath = ThemePalette.RoundedRectangle(badge, 16))
        {
            g.FillPath(badgeFill, badgePath);
            g.DrawPath(badgeBorder, badgePath);
        }
        using (var badgeBrush = new SolidBrush(gold))
            g.DrawString("✦  RODADA CONCLUÍDA  ✦", badgeFont, badgeBrush, badge, sf);
        using (var headingBrush = new SolidBrush(foreground))
            g.DrawString("E O PRÓXIMO A FALAR É", headingFont, headingBrush, new RectangleF(w * 0.08f, h * 0.31f, w * 0.84f, h * 0.08f), sf);

        using Font winnerFont = FitWinnerFont(g, Winner, Math.Clamp(w * 0.065f, 34, 78), w * 0.82f, h * 0.19f);
        RectangleF nameBounds = new(w * 0.08f, h * 0.41f, w * 0.84f, h * 0.22f);
        using (var shadow = new SolidBrush(Color.FromArgb(LightMode ? 65 : 140, 0, 0, 0)))
            g.DrawString(Winner, winnerFont, shadow, new RectangleF(nameBounds.X + 2, nameBounds.Y + 3, nameBounds.Width, nameBounds.Height), sf);
        using (var nameBrush = new SolidBrush(gold))
            g.DrawString(Winner, winnerFont, nameBrush, nameBounds, sf);
        using (var subBrush = new SolidBrush(muted))
            g.DrawString("Agora é a sua vez de compartilhar sua atualização.", subFont, subBrush, new RectangleF(w * 0.09f, h * 0.65f, w * 0.82f, 34), sf);
    }

    private static Font FitWinnerFont(Graphics graphics, string text, float startSize, float maxWidth, float maxHeight)
    {
        float size = startSize;
        while (size > 20)
        {
            var font = new Font("Segoe UI Semibold", size, FontStyle.Bold, GraphicsUnit.Pixel);
            SizeF measured = graphics.MeasureString(text, font, new SizeF(maxWidth, maxHeight));
            if (measured.Width <= maxWidth && measured.Height <= maxHeight) return font;
            font.Dispose();
            size -= 2;
        }
        return new Font("Segoe UI Semibold", size, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private Particle NewParticle(bool randomY) => new()
    {
        X = (float)(_random.NextDouble() * Math.Max(1, Width)),
        Y = randomY ? (float)(_random.NextDouble() * Math.Max(1, Height)) : -10,
        Speed = (float)(1.5 + _random.NextDouble() * 4.2),
        Drift = (float)((_random.NextDouble() - 0.5) * 1.9),
        Angle = (float)(_random.NextDouble() * 360),
        Spin = (float)((_random.NextDouble() - 0.5) * 8),
        Width = (float)(4 + _random.NextDouble() * 7),
        Height = (float)(6 + _random.NextDouble() * 10),
        ColorIndex = _random.Next(Math.Max(1, _colors.Length))
    };

    private void UpdateParticles()
    {
        foreach (Particle p in _particles)
        {
            p.Y += p.Speed;
            p.X += p.Drift + MathF.Sin(p.Angle * MathF.PI / 90f) * 0.8f;
            p.Angle = (p.Angle + p.Spin) % 360;
            if (p.Y > Height + 20 || p.X < -25 || p.X > Width + 25)
            {
                Particle fresh = NewParticle(false);
                p.X = fresh.X; p.Y = fresh.Y; p.Speed = fresh.Speed; p.Drift = fresh.Drift;
                p.Angle = fresh.Angle; p.Spin = fresh.Spin; p.Width = fresh.Width; p.Height = fresh.Height;
                p.ColorIndex = fresh.ColorIndex;
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        foreach (Particle p in _particles)
            if (p.Y < 0 || p.X > Width) { p.X = (float)(_random.NextDouble() * Math.Max(1, Width)); p.Y = (float)(_random.NextDouble() * Math.Max(1, Height)); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            foreach (SolidBrush brush in _brushes) brush.Dispose();
        }
        base.Dispose(disposing);
    }
}
