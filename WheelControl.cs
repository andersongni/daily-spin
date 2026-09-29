using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;

namespace RoletaDaDaily;

internal sealed class WheelControl : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer _revealTimer = new() { Interval = 1400 };
    private readonly Stopwatch _clock = new();
    private IReadOnlyList<string> _names = Array.Empty<string>();
    private ThemePalette _palette = ThemePalette.All[0];
    private double _rotation;
    private double _fromRotation;
    private double _toRotation;
    private const double DurationSeconds = 5.2;
    private string? _winner;
    private bool _waitingForReveal;

    public WheelControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Roleta da Daily";
        AccessibleDescription = "Roleta de participantes. Use o botão Girar a Roleta para iniciar.";
        Cursor = Cursors.Hand;
        _timer.Tick += AnimationTick;
        _revealTimer.Tick += RevealTick;
    }

    public event EventHandler? SpinRequested;
    public event EventHandler? SpinStopped;
    public event Action<object?, string>? SpinCompleted;
    public bool IsSpinning { get; private set; }
    public IReadOnlyList<string> Names { get => _names; set { _names = value ?? Array.Empty<string>(); Invalidate(); } }
    public ThemePalette Palette { get => _palette; set { _palette = value ?? ThemePalette.All[0]; Invalidate(); } }

    public void StartSpin()
    {
        if (IsSpinning || _waitingForReveal || _names.Count == 0) return;
        int winnerIndex = RandomNumberGenerator.GetInt32(_names.Count);
        _winner = _names[winnerIndex];
        _fromRotation = _rotation;

        // Segmento i ocupa [-90 + rotação + i*fatia, -90 + rotação + (i+1)*fatia].
        // Colocamos o centro do segmento escolhido sob o ponteiro fixo de -90 graus.
        double target = NormalizeDegrees(-(winnerIndex + 0.5) * 360.0 / _names.Count);
        double extra = NormalizeDegrees(target - NormalizeDegrees(_fromRotation));
        _toRotation = _fromRotation + RandomNumberGenerator.GetInt32(5, 8) * 360.0 + extra;

        IsSpinning = true;
        _clock.Restart();
        _timer.Start();
        Invalidate();
    }

    public void CancelSpin()
    {
        _timer.Stop();
        _revealTimer.Stop();
        _clock.Stop();
        IsSpinning = false;
        _waitingForReveal = false;
    }

    private void AnimationTick(object? sender, EventArgs e)
    {
        double progress = Math.Clamp(_clock.Elapsed.TotalSeconds / DurationSeconds, 0, 1);
        // Smoothstep acelera suavemente a saída e reduz a velocidade até o repouso.
        double eased = progress * progress * (3 - 2 * progress);
        _rotation = _fromRotation + (_toRotation - _fromRotation) * eased;
        if (progress >= 1)
        {
            _rotation = _toRotation;
            _timer.Stop();
            _clock.Stop();
            IsSpinning = false;
            _waitingForReveal = true;
            _revealTimer.Start();
            Invalidate();
            SpinStopped?.Invoke(this, EventArgs.Empty);
            return;
        }
        Invalidate();
    }

    private void RevealTick(object? sender, EventArgs e)
    {
        _revealTimer.Stop();
        _waitingForReveal = false;
        // O resultado é lido da posição final sob o ponteiro, mantendo a roleta como fonte da verdade.
        int pointedIndex = SegmentAtPointer(_rotation, _names.Count);
        if (pointedIndex >= 0 && pointedIndex < _names.Count)
            SpinCompleted?.Invoke(this, _names[pointedIndex]);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) SpinRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float radius = Math.Max(60, Math.Min(Width, Height) * 0.44f - 18);
        float cx = Width / 2f, cy = Height / 2f + 1;
        RectangleF circle = new(cx - radius, cy - radius, radius * 2, radius * 2);

        for (int i = 5; i >= 1; i--)
        {
            float r = radius + i * 2.5f;
            using var shadow = new SolidBrush(Color.FromArgb(12 + (6 - i) * 5, 0, 0, 0));
            g.FillEllipse(shadow, cx - r, cy - r + i * 1.5f, r * 2, r * 2);
        }

        float ringWidth = Math.Max(10, radius * 0.075f);
        RectangleF outer = new(circle.X - ringWidth * 0.55f, circle.Y - ringWidth * 0.55f,
            circle.Width + ringWidth * 1.1f, circle.Height + ringWidth * 1.1f);
        using (var ring = new LinearGradientBrush(outer, _palette.RingStart, _palette.RingEnd, LinearGradientMode.ForwardDiagonal))
            g.FillEllipse(ring, outer);
        using (var line = new Pen(Color.FromArgb(220, Color.White), 1.1f))
            g.DrawEllipse(line, outer.X + 2, outer.Y + 2, outer.Width - 4, outer.Height - 4);
        using (var plate = new SolidBrush(Color.FromArgb(21, 25, 38)))
            g.FillEllipse(plate, circle);

        if (_names.Count == 0)
        {
            using var dashed = new Pen(Color.FromArgb(85, 194, 187, 174), 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawEllipse(dashed, circle);
            using var emptyFont = new Font("Segoe UI Semibold", Math.Max(14, radius * 0.065f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var detailFont = new Font("Segoe UI", Math.Max(11, radius * 0.043f), FontStyle.Regular, GraphicsUnit.Pixel);
            using var detailBrush = new SolidBrush(Color.FromArgb(176, 182, 198));
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Adicione participantes", emptyFont, Brushes.White,
                new RectangleF(circle.X + 20, cy - radius * 0.46f, circle.Width - 40, 36), sf);
            g.DrawString("Digite um nome por linha no painel ao lado", detailFont, detailBrush,
                new RectangleF(circle.X + 24, cy + radius * 0.29f, circle.Width - 48, 40), sf);
        }
        else
        {
            DrawSegments(g, circle);
            DrawNames(g, cx, cy, radius);
            using var rim = new Pen(Color.FromArgb(210, 255, 239, 192), 1.2f);
            g.DrawEllipse(rim, circle);
        }

        DrawHub(g, cx, cy, radius);
        DrawPointer(g, cx, cy, radius);
    }

    private void DrawSegments(Graphics g, RectangleF bounds)
    {
        float sweep = 360f / _names.Count;
        for (int i = 0; i < _names.Count; i++)
        {
            float start = (float)(-90 + _rotation + i * sweep);
            using var brush = new SolidBrush(_palette.Segments[i % _palette.Segments.Length]);
            g.FillPie(brush, bounds, start, sweep + 0.2f);
            using var segmentPen = new Pen(Color.FromArgb(165, 255, 243, 210), Math.Max(0.8f, bounds.Width / 650f));
            g.DrawPie(segmentPen, bounds, start, sweep);
        }
    }

    private void DrawNames(Graphics g, float cx, float cy, float radius)
    {
        float sweep = 360f / _names.Count;
        float fontSize = _names.Count switch
        {
            <= 3 => Math.Max(13, radius * 0.075f),
            <= 6 => Math.Max(11, radius * 0.060f),
            <= 12 => Math.Max(9, radius * 0.047f),
            _ => Math.Max(7, radius * 0.034f)
        };
        float labelRadius = radius * (_names.Count == 1 ? 0.57f : 0.62f);
        using var shadow = new SolidBrush(Color.FromArgb(135, 0, 0, 0));
        using var foreground = new SolidBrush(Color.White);
        using var sf = new StringFormat(StringFormatFlags.NoClip)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None
        };

        for (int i = 0; i < _names.Count; i++)
        {
            float middle = (float)(-90 + _rotation + (i + 0.5) * sweep);
            double radians = middle * Math.PI / 180;
            float x = cx + (float)Math.Cos(radians) * labelRadius;
            float y = cy + (float)Math.Sin(radians) * labelRadius;
            float width = _names.Count == 1 ? radius * 1.15f :
                Math.Max(14, 2 * labelRadius * (float)Math.Sin(Math.Min(Math.PI, sweep * Math.PI / 180) / 2) * 0.86f);
            float height = Math.Max(22, fontSize * 2.8f);
            float angle = NormalizeLabelAngle(middle + 90);
            float individualSize = fontSize;
            while (individualSize > 6)
            {
                using var measuredFont = new Font("Segoe UI Semibold", individualSize, FontStyle.Bold, GraphicsUnit.Pixel);
                if (g.MeasureString(_names[i], measuredFont).Width <= width * 0.94f) break;
                individualSize -= 1;
            }
            using var individualFont = new Font("Segoe UI Semibold", individualSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            var rect = new RectangleF(-width / 2, -height / 2, width, height);
            g.DrawString(_names[i], individualFont, shadow, new RectangleF(rect.X + 1, rect.Y + 1, rect.Width, rect.Height), sf);
            g.DrawString(_names[i], individualFont, foreground, rect, sf);
            g.Restore(state);
        }
    }

    private void DrawHub(Graphics g, float cx, float cy, float radius)
    {
        float hubRadius = radius * 0.225f;
        RectangleF hub = new(cx - hubRadius, cy - hubRadius, hubRadius * 2, hubRadius * 2);
        using (var halo = new Pen(Color.FromArgb(110, _palette.HubStart), radius * 0.018f))
            g.DrawEllipse(halo, hub.X - radius * 0.025f, hub.Y - radius * 0.025f, hub.Width + radius * 0.05f, hub.Height + radius * 0.05f);
        using (var fill = new LinearGradientBrush(hub, _palette.HubStart, _palette.HubEnd, LinearGradientMode.ForwardDiagonal))
            g.FillEllipse(fill, hub);
        using (var border = new Pen(Color.FromArgb(235, 255, 231, 177), Math.Max(1.2f, radius * 0.012f)))
            g.DrawEllipse(border, hub);
        using var symbolFont = new Font("Segoe UI Symbol", Math.Max(10, hubRadius * 0.25f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleFont = new Font("Segoe UI", Math.Max(9, hubRadius * 0.29f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var subFont = new Font("Segoe UI", Math.Max(7, hubRadius * 0.14f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var symbolBrush = new SolidBrush(Color.FromArgb(235, 255, 240, 200));
        using var subBrush = new SolidBrush(Color.FromArgb(220, 255, 228, 174));
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("✦", symbolFont, symbolBrush, new RectangleF(hub.X, cy - hubRadius * 0.84f, hub.Width, hubRadius * 0.30f), sf);
        g.DrawString("DAILY SPIN", titleFont, Brushes.White,
            new RectangleF(hub.X + 4, cy - hubRadius * 0.28f, hub.Width - 8, hubRadius * 0.56f), sf);
        g.DrawString("•  •  •", subFont, subBrush, new RectangleF(hub.X, cy + hubRadius * 0.43f, hub.Width, hubRadius * 0.25f), sf);
    }

    private static void DrawPointer(Graphics g, float cx, float cy, float radius)
    {
        float tipY = cy - radius * 0.82f, baseY = cy - radius - 1;
        float halfWidth = Math.Max(11, radius * 0.078f);
        PointF[] points = [new(cx, tipY), new(cx - halfWidth, baseY), new(cx + halfWidth, baseY)];
        using var shadow = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        PointF[] shadowPoints = points.Select(p => new PointF(p.X + 1.5f, p.Y + 3)).ToArray();
        g.FillPolygon(shadow, shadowPoints);
        using var fill = new LinearGradientBrush(new RectangleF(cx - halfWidth, baseY, halfWidth * 2, tipY - baseY),
            Color.FromArgb(255, 247, 205, 117), Color.FromArgb(255, 175, 112, 35), LinearGradientMode.Vertical);
        g.FillPolygon(fill, points);
        using var border = new Pen(Color.FromArgb(255, 255, 238, 190), 1.2f);
        g.DrawPolygon(border, points);
    }

    private static float NormalizeLabelAngle(float angle)
    {
        angle %= 360;
        if (angle > 180) angle -= 360;
        if (angle > 90) angle -= 180;
        if (angle < -90) angle += 180;
        return angle;
    }

    internal static int SegmentAtPointer(double rotation, int count)
    {
        if (count <= 0) return -1;
        double relative = NormalizeDegrees(-rotation);
        return Math.Min(count - 1, (int)Math.Floor(relative / (360.0 / count)));
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360;
        return value < 0 ? value + 360 : value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelSpin();
            _timer.Dispose();
            _revealTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
