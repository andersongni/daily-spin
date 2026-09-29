using System.Drawing.Drawing2D;

namespace RoletaDaDaily;

internal sealed class RoundedButton : Control
{
    private Region? _roundedRegion;
    private bool _hovered;
    private bool _pressed;
    private Color _topColor = Color.FromArgb(235, 188, 100);
    private Color _bottomColor = Color.FromArgb(188, 135, 53);
    private Color _borderColor = Color.FromArgb(255, 224, 157);
    private Color _textColor = Color.FromArgb(35, 29, 23);

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                 ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.StandardClick, false);
        TabStop = true;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
        MinimumSize = new Size(70, 40);
    }

    public Color TopColor { get => _topColor; set { _topColor = value; Invalidate(); } }
    public Color BottomColor { get => _bottomColor; set { _bottomColor = value; Invalidate(); } }
    public Color BorderColor { get => _borderColor; set { _borderColor = value; Invalidate(); } }
    public Color TextColor { get => _textColor; set { _textColor = value; Invalidate(); } }
    public int CornerRadius { get; set; } = 15;

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

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _pressed = true; Focus(); Invalidate(); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        bool clicked = e.Button == MouseButtons.Left && _pressed && ClientRectangle.Contains(e.Location);
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
        if (clicked) OnClick(EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            _pressed = true;
            Invalidate();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space && _pressed)
        {
            _pressed = false;
            Invalidate();
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF bounds = new(1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3));
        using GraphicsPath path = ThemePalette.RoundedRectangle(bounds, CornerRadius);
        Color top = _topColor, bottom = _bottomColor, text = _textColor;
        if (!Enabled)
        {
            top = Color.FromArgb(90, top);
            bottom = Color.FromArgb(85, bottom);
            text = Color.FromArgb(135, text);
        }
        else if (_pressed) (top, bottom) = (bottom, top);
        else if (_hovered)
        {
            top = ThemePalette.Lighten(top, 0.1f);
            bottom = ThemePalette.Lighten(bottom, 0.08f);
        }
        using (var brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical))
            e.Graphics.FillPath(brush, path);
        using (var pen = new Pen(_borderColor, _hovered && Enabled ? 1.6f : 1f))
            e.Graphics.DrawPath(pen, path);
        if (Focused)
        {
            using var focusPen = new Pen(Color.FromArgb(170, _borderColor), 2);
            e.Graphics.DrawPath(focusPen, path);
        }
        Rectangle textBounds = Rectangle.Round(bounds);
        textBounds.Inflate(-10, -3);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
