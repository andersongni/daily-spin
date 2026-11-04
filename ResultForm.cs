namespace RoletaDaDaily;

internal enum ResultAction { None, RemoveAndReturn, SpinAgain, Return }

internal sealed class ResultForm : Form
{
    private readonly ConfettiControl _canvas;
    private readonly RoundedButton _removeButton;
    private readonly RoundedButton _againButton;
    private readonly RoundedButton _returnButton;
    private readonly Dictionary<RoundedButton, Font> _scaledFonts = [];
    private bool _actionChosen;

    public ResultForm(string winner, ThemePalette palette, AppTheme appTheme)
    {
        TopLevel = false;
        FormBorderStyle = FormBorderStyle.None;
        Dock = DockStyle.Fill;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        TabStop = true;
        AccessibleName = "Resultado do sorteio";
        AccessibleDescription = $"O próximo a falar é {winner}. Fale o que pretende fazer nos próximos dias e se há algum impedimento.";

        _canvas = new ConfettiControl
        {
            Dock = DockStyle.Fill,
            Winner = winner,
            Palette = palette,
            LightMode = appTheme.IsLight,
            Theme = appTheme,
            AccessibleName = $"E o próximo a falar é {winner}"
        };
        Controls.Add(_canvas);

        _removeButton = NewButton("Remover sorteado e voltar", appTheme.Gold, appTheme.GoldDark,
            Color.FromArgb(38, 29, 21), 17, "\uE74D");
        _againButton = NewButton("Sortear novamente (manter nome)", appTheme.Panel, appTheme.Panel,
            appTheme.Text, 13, "\uE7A7");
        _returnButton = NewButton("Voltar sem remover", appTheme.Panel, appTheme.Panel,
            appTheme.Text, 13, "\uE72B");
        _againButton.BorderColor = _returnButton.BorderColor = appTheme.PanelBorder;
        _removeButton.Click += (_, _) => Choose(ResultAction.RemoveAndReturn);
        _againButton.Click += (_, _) => Choose(ResultAction.SpinAgain);
        _returnButton.Click += (_, _) => Choose(ResultAction.Return);
        _canvas.Controls.AddRange([_removeButton, _againButton, _returnButton]);
        _removeButton.BringToFront();
        _againButton.BringToFront();
        _returnButton.BringToFront();
        Resize += (_, _) => LayoutButtons();
        KeyDown += ResultForm_KeyDown;
        Shown += (_, _) =>
        {
            LayoutButtons();
            _canvas.StartAnimation();
            _removeButton.Focus();
        };
        FormClosing += (_, _) => _canvas.StopAnimation();
        Action = ResultAction.None;
    }

    public ResultAction Action { get; private set; }

    private static RoundedButton NewButton(string text, Color top, Color bottom, Color textColor,
        float fontSize, string iconGlyph) => new()
    {
        Text = text,
        TopColor = top,
        BottomColor = bottom,
        BorderColor = ThemePalette.Lighten(top, 0.26f),
        TextColor = textColor,
        IconGlyph = iconGlyph,
        Font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold, GraphicsUnit.Point),
        CornerRadius = 16,
        AccessibleName = text,
        AccessibleRole = AccessibleRole.PushButton
    };

    private void LayoutButtons()
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2) return;

        float dpi = DeviceDpi / 96f;
        float logicalWidth = ClientSize.Width / dpi;
        float logicalHeight = ClientSize.Height / dpi;
        float s = Math.Clamp(MathF.Min(logicalWidth / 1120f, logicalHeight / 760f), 0.46f, 1f);

        int Px(float value) => Math.Max(1, (int)MathF.Round(value * s * dpi));

        int margin = Math.Max(Px(24), ClientSize.Width / 40);
        int gap = Px(12);
        int width = Math.Max(80, ClientSize.Width - margin * 2);
        int removeHeight = Px(56);
        int secondaryHeight = Px(52);
        int bottomPad = Px(24);

        int secondaryTop = ClientSize.Height - bottomPad - secondaryHeight;
        int removeTop = secondaryTop - gap - removeHeight;
        _removeButton.Bounds = new Rectangle(margin, removeTop, width, removeHeight);

        int half = (width - gap) / 2;
        _againButton.Bounds = new Rectangle(margin, secondaryTop, half, secondaryHeight);
        _returnButton.Bounds = new Rectangle(margin + half + gap, secondaryTop, width - half - gap, secondaryHeight);

        ApplyScaledFont(_removeButton, 17f, s);
        ApplyScaledFont(_againButton, 13f, s);
        ApplyScaledFont(_returnButton, 13f, s);
        _removeButton.CornerRadius = _againButton.CornerRadius = _returnButton.CornerRadius = Px(16);

        _canvas.ReservedBottom = ClientSize.Height - removeTop + Px(8);
        _canvas.Invalidate();
    }

    private void ApplyScaledFont(RoundedButton button, float basePoints, float scale)
    {
        float size = MathF.Max(6.5f, MathF.Round(basePoints * scale, 1));
        if (Math.Abs(button.Font.Size - size) < 0.05f) return;
        var next = new Font(button.Font.FontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
        button.Font = next;
        if (_scaledFonts.Remove(button, out Font? previous)) previous.Dispose();
        _scaledFonts[button] = next;
    }

    private void Choose(ResultAction action)
    {
        if (_actionChosen) return;
        _actionChosen = true;
        Action = action;
        _removeButton.Enabled = _againButton.Enabled = _returnButton.Enabled = false;
        Close();
    }

    private void ResultForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape || _actionChosen) return;
        _actionChosen = true;
        Action = ResultAction.Return;
        e.Handled = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _canvas.StopAnimation();
            foreach (Font font in _scaledFonts.Values) font.Dispose();
            _scaledFonts.Clear();
        }
        base.Dispose(disposing);
    }
}
