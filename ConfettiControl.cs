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
    private readonly System.Diagnostics.Stopwatch _elapsed = new();
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
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    /// <summary>Altura em pixels ocupada pelos botões, excluída da área de texto.</summary>
    public int ReservedBottom { get; set; }
    public ThemePalette Palette { get => _palette; set => SetPalette(value); }
    public void StartAnimation()
    {
        _elapsed.Reset();
        _elapsed.Start();
        _timer.Start();
    }
    public void StopAnimation()
    {
        _timer.Stop();
        _elapsed.Reset();
    }

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
        Color top = Theme.BackgroundTop;
        Color bottom = Theme.BackgroundBottom;
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

    private static readonly string[] InstructionLines =
    [
        "Fale o que pretende fazer nos próximos dias.",
        "Avise se há algum impedimento."
    ];

    private void DrawCelebration(Graphics g)
    {
        float w = ClientSize.Width;
        // O conteúdo ocupa apenas a faixa livre acima dos botões, em frações da altura disponível.
        float area = Math.Max(120, ClientSize.Height - ReservedBottom);
        float unit = Math.Min(w, area * 1.45f);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Color gold = Theme.Gold;
        Color foreground = Theme.Text;
        Color muted = Theme.Muted;

        float crownSize = Math.Clamp(area * 0.055f, 16, 30);
        DrawCrown(g, new RectangleF((w - crownSize * 1.25f) / 2f, area * 0.025f, crownSize * 1.25f, crownSize), gold);

        using var badgeFont = new Font("Segoe UI Semibold", Math.Clamp(unit * 0.013f, 8.5f, 15), FontStyle.Bold, GraphicsUnit.Pixel);
        float badgeHeight = Math.Clamp(area * 0.072f, 24, 40);
        float badgeWidth = Math.Min(w * 0.5f, Math.Max(200, unit * 0.27f));
        RectangleF badge = new((w - badgeWidth) / 2f, area * 0.105f, badgeWidth, badgeHeight);
        FillPanel(g, badge, badgeHeight / 2f, gold, 1.4f);
        using (var badgeBrush = new SolidBrush(gold))
            g.DrawString("✦  RODADA CONCLUÍDA  ✦", badgeFont, badgeBrush, badge, sf);

        float cardWidth = Math.Min(w * 0.78f, Math.Max(300, unit * 0.86f));
        RectangleF card = new((w - cardWidth) / 2f, area * 0.235f, cardWidth, area * 0.50f);
        FillPanel(g, card, Math.Clamp(area * 0.035f, 12, 20), Theme.PanelBorder, 1.4f);

        using var headingFont = new Font("Segoe UI Semibold", Math.Clamp(unit * 0.027f, 13, 32), FontStyle.Bold, GraphicsUnit.Pixel);
        var headingBounds = new RectangleF(card.X, card.Y + card.Height * 0.06f, card.Width, card.Height * 0.20f);
        using (var headingBrush = new SolidBrush(foreground))
            g.DrawString("E O PRÓXIMO A FALAR É", headingFont, headingBrush, headingBounds, sf);
        DrawHeadingRules(g, g.MeasureString("E O PRÓXIMO A FALAR É", headingFont).Width, card, headingBounds, gold);

        float nameHeight = card.Height * 0.30f;
        RectangleF nameBounds = new(card.X + card.Width * 0.14f, card.Y + card.Height * 0.26f,
            card.Width * 0.72f, nameHeight);
        using Font winnerFont = FitWinnerFont(g, Winner, Math.Clamp(unit * 0.062f, 20, 74), nameBounds.Width, nameBounds.Height);
        DrawGlowingName(g, nameBounds, winnerFont, sf, gold);
        DrawFlourishes(g, card, nameBounds, gold);

        float pillHeight = Math.Clamp(card.Height * 0.24f, 32, 58);
        float pillWidth = Math.Min(card.Width * 0.48f, Math.Max(150, unit * 0.23f));
        RectangleF pill = new((w - pillWidth) / 2f, card.Bottom - card.Height * 0.10f - pillHeight, pillWidth, pillHeight);
        FillPanel(g, pill, pillHeight / 2f, gold, 2f);
        float iconSize = pillHeight * 0.52f;
        DrawStopwatch(g, new RectangleF(pill.X + pillWidth * 0.14f, pill.Y + (pillHeight - iconSize) / 2f, iconSize, iconSize), gold);
        TimeSpan elapsed = _elapsed.Elapsed;
        using var timerFont = new Font("Segoe UI Semibold", Math.Clamp(pillHeight * 0.46f, 15, 30), FontStyle.Bold, GraphicsUnit.Pixel);
        using (var timerBrush = new SolidBrush(foreground))
            g.DrawString($"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}", timerFont, timerBrush,
                new RectangleF(pill.X + pillWidth * 0.26f, pill.Y, pillWidth * 0.68f, pillHeight), sf);

        DrawInstructions(g, w, area, unit, gold, muted);
    }

    private void FillPanel(Graphics g, RectangleF bounds, float radius, Color borderColor, float borderWidth)
    {
        if (bounds.Width < 4 || bounds.Height < 4) return;
        using GraphicsPath path = ThemePalette.RoundedRectangle(bounds, radius);
        // Painéis quase opacos impedem que o confete atravesse o texto.
        Color top = Color.FromArgb(246, ThemePalette.Lighten(Theme.Panel, LightMode ? 0.02f : 0.06f));
        Color bottom = Color.FromArgb(246, Theme.Panel);
        using var fill = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
        using var border = new Pen(Color.FromArgb(LightMode ? 170 : 210, borderColor), borderWidth);
        g.FillPath(fill, path);
        g.DrawPath(border, path);
    }

    private static void DrawHeadingRules(Graphics g, float textWidth, RectangleF card, RectangleF heading, Color gold)
    {
        float centerY = heading.Y + heading.Height / 2f;
        float gap = textWidth / 2f + card.Width * 0.035f;
        float ruleLength = Math.Max(0, Math.Min(card.Width * 0.14f, card.Width * 0.5f - gap - card.Width * 0.08f));
        if (ruleLength < 8) return;
        using var pen = new Pen(Color.FromArgb(190, gold), 1.6f);
        float center = card.X + card.Width / 2f;
        g.DrawLine(pen, center - gap - ruleLength, centerY, center - gap, centerY);
        g.DrawLine(pen, center + gap, centerY, center + gap + ruleLength, centerY);
    }

    private void DrawGlowingName(Graphics g, RectangleF bounds, Font font, StringFormat format, Color gold)
    {
        if (string.IsNullOrEmpty(Winner)) return;
        using var path = new GraphicsPath();
        path.AddString(Winner, font.FontFamily, (int)font.Style, font.Size, bounds, format);
        // O halo é formado por contornos largos e translúcidos antes do preenchimento sólido.
        foreach ((float width, int alpha) in new[] { (10f, 26), (6f, 42), (3f, 70) })
        {
            using var glow = new Pen(Color.FromArgb(alpha, gold), width) { LineJoin = LineJoin.Round };
            g.DrawPath(glow, path);
        }
        using var fill = new SolidBrush(gold);
        g.FillPath(fill, path);
    }

    private static void DrawFlourishes(Graphics g, RectangleF card, RectangleF nameBounds, Color gold)
    {
        float size = Math.Min(card.Width * 0.06f, nameBounds.Height * 0.55f);
        if (size < 10) return;
        float centerY = nameBounds.Y + nameBounds.Height / 2f;
        DrawFlourish(g, nameBounds.X - size * 1.15f, centerY, size, gold, false);
        DrawFlourish(g, nameBounds.Right + size * 1.15f, centerY, size, gold, true);
    }

    private static void DrawFlourish(Graphics g, float x, float centerY, float size, Color gold, bool mirrored)
    {
        using var pen = new Pen(Color.FromArgb(215, gold), Math.Max(1.4f, size * 0.07f));
        int direction = mirrored ? -1 : 1;
        for (int i = -1; i <= 1; i++)
        {
            float spread = i * size * 0.46f;
            float startX = x + direction * size * 0.08f;
            float midX = x + direction * size * 0.48f;
            float endX = x + direction * size * 0.88f;
            PointF[] curve =
            [
                new(startX, centerY + spread * 0.30f),
                new(midX, centerY + spread * 0.70f),
                new(endX, centerY + spread)
            ];
            g.DrawCurve(pen, curve, 0.5f);
        }
    }

    private static void DrawCrown(Graphics g, RectangleF bounds, Color gold)
    {
        if (bounds.Width < 8) return;
        float x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
        PointF[] points =
        [
            new(x, y + h),
            new(x, y + h * 0.22f),
            new(x + w * 0.27f, y + h * 0.62f),
            new(x + w * 0.5f, y + h * 0.02f),
            new(x + w * 0.73f, y + h * 0.62f),
            new(x + w, y + h * 0.22f),
            new(x + w, y + h)
        ];
        using var fill = new SolidBrush(gold);
        g.FillPolygon(fill, points);
    }

    private static void DrawStopwatch(Graphics g, RectangleF bounds, Color gold)
    {
        if (bounds.Width < 8) return;
        float stroke = Math.Max(1.4f, bounds.Width * 0.1f);
        RectangleF dial = new(bounds.X, bounds.Y + bounds.Height * 0.14f, bounds.Width, bounds.Height * 0.86f);
        using var pen = new Pen(gold, stroke);
        g.DrawEllipse(pen, dial);
        float stemWidth = bounds.Width * 0.26f;
        g.DrawLine(pen, bounds.X + (bounds.Width - stemWidth) / 2f, bounds.Y + bounds.Height * 0.06f,
            bounds.X + (bounds.Width + stemWidth) / 2f, bounds.Y + bounds.Height * 0.06f);
        float cx = dial.X + dial.Width / 2f, cy = dial.Y + dial.Height / 2f;
        using var hands = new Pen(gold, Math.Max(1.1f, stroke * 0.8f));
        g.DrawLine(hands, cx, cy, cx, cy - dial.Height * 0.28f);
        g.DrawLine(hands, cx, cy, cx + dial.Width * 0.22f, cy);
    }

    private void DrawInstructions(Graphics g, float w, float area, float unit, Color gold, Color muted)
    {
        float boxWidth = Math.Min(w * 0.68f, Math.Max(280, unit * 0.66f));
        RectangleF box = new((w - boxWidth) / 2f, area * 0.775f, boxWidth, area * 0.18f);
        FillPanel(g, box, Math.Clamp(area * 0.022f, 8, 14), Theme.PanelBorder, 1.4f);

        float iconSize = Math.Clamp(box.Height * 0.46f, 14, 30);
        DrawInfoIcon(g, new RectangleF(box.X + box.Width * 0.045f, box.Y + (box.Height - iconSize) / 2f, iconSize, iconSize), gold);

        using var font = new Font("Segoe UI", Math.Clamp(unit * 0.019f, 11, 21), FontStyle.Regular, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(LightMode ? muted : Color.FromArgb(235, Theme.Text));
        using var bulletBrush = new SolidBrush(gold);
        using var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        float textLeft = box.X + box.Width * 0.045f + iconSize + box.Width * 0.05f;
        float lineHeight = box.Height / (InstructionLines.Length + 0.6f);
        float firstTop = box.Y + (box.Height - lineHeight * InstructionLines.Length) / 2f;
        float bulletSize = Math.Max(3.5f, font.Size * 0.42f);

        for (int i = 0; i < InstructionLines.Length; i++)
        {
            float centerY = firstTop + lineHeight * (i + 0.5f);
            g.FillEllipse(bulletBrush, textLeft, centerY - bulletSize / 2f, bulletSize, bulletSize);
            g.DrawString(InstructionLines[i], font, textBrush,
                new RectangleF(textLeft + bulletSize * 2.2f, centerY - lineHeight / 2f,
                    box.Right - textLeft - bulletSize * 2.2f - box.Width * 0.04f, lineHeight), left);
        }
    }

    private static void DrawInfoIcon(Graphics g, RectangleF bounds, Color gold)
    {
        if (bounds.Width < 8) return;
        using var pen = new Pen(gold, Math.Max(1.3f, bounds.Width * 0.09f));
        g.DrawEllipse(pen, bounds);
        using var font = new Font("Segoe UI Semibold", Math.Max(7f, bounds.Height * 0.62f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(gold);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("i", font, brush, bounds, format);
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
