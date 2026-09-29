using System.Drawing.Drawing2D;

namespace RoletaDaDaily;

internal sealed class RoundedPanel : Panel
{
    private Region? _roundedRegion;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Padding = new Padding(18);
    }

    public Color BorderColor { get; set; } = Color.FromArgb(60, 70, 90);
    public int CornerRadius { get; set; } = 18;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRoundedRegion();
    }

    private void UpdateRoundedRegion()
    {
        if (Width < 2 || Height < 2) return;
        using GraphicsPath path = ThemePalette.RoundedRectangle(new RectangleF(0, 0, Width, Height), CornerRadius);
        var nextRegion = new Region(path);
        Region = nextRegion;
        _roundedRegion?.Dispose();
        _roundedRegion = nextRegion;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        RectangleF bounds = new(0.5f, 0.5f, Width - 1, Height - 1);
        using GraphicsPath path = ThemePalette.RoundedRectangle(bounds, CornerRadius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(fill, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2) return;
        RectangleF bounds = new(0.5f, 0.5f, Width - 1, Height - 1);
        using GraphicsPath path = ThemePalette.RoundedRectangle(bounds, CornerRadius);
        using var border = new Pen(BorderColor, 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(border, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Region = null;
            _roundedRegion?.Dispose();
            _roundedRegion = null;
        }
        base.Dispose(disposing);
    }
}
