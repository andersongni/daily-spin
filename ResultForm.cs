namespace RoletaDaDaily;

internal enum ResultAction { None, RemoveAndReturn, SpinAgain, Return }

internal sealed class ResultForm : Form
{
    private readonly ConfettiControl _canvas;
    private readonly RoundedButton _removeButton;
    private readonly RoundedButton _againButton;
    private readonly RoundedButton _returnButton;
    private bool _actionChosen;

    public ResultForm(Form owner, string winner, ThemePalette palette, AppTheme appTheme)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        TopMost = false;
        DoubleBuffered = true;
        AccessibleName = "Resultado do sorteio";
        AccessibleDescription = $"O próximo a falar é {winner}.";
        Bounds = owner.RectangleToScreen(owner.ClientRectangle);

        _canvas = new ConfettiControl
        {
            Dock = DockStyle.Fill,
            Winner = winner,
            Palette = palette,
            LightMode = appTheme.IsLight,
            AccessibleName = $"E o próximo a falar é {winner}"
        };
        Controls.Add(_canvas);

        _removeButton = NewButton("Remover sorteado e voltar", appTheme.Gold, appTheme.GoldDark,
            Color.FromArgb(38, 29, 21), 17);
        _againButton = NewButton("Sortear novamente (manter nome)", appTheme.Panel, appTheme.Panel,
            appTheme.Text, 13);
        _returnButton = NewButton("Voltar sem remover", appTheme.Panel, appTheme.Panel,
            appTheme.Text, 13);
        _againButton.BorderColor = _returnButton.BorderColor = appTheme.PanelBorder;
        _removeButton.Click += (_, _) => Choose(ResultAction.RemoveAndReturn);
        _againButton.Click += (_, _) => Choose(ResultAction.SpinAgain);
        _returnButton.Click += (_, _) => Choose(ResultAction.Return);
        Controls.AddRange([_removeButton, _againButton, _returnButton]);
        _removeButton.BringToFront();
        _againButton.BringToFront();
        _returnButton.BringToFront();
        Resize += (_, _) => LayoutButtons();
        KeyDown += ResultForm_KeyDown;
        Shown += (_, _) =>
        {
            Bounds = owner.RectangleToScreen(owner.ClientRectangle);
            LayoutButtons();
            _canvas.StartAnimation();
            _removeButton.Focus();
        };
        FormClosing += (_, _) => _canvas.StopAnimation();
        Action = ResultAction.None;
    }

    public ResultAction Action { get; private set; }

    private static RoundedButton NewButton(string text, Color top, Color bottom, Color textColor, float fontSize) => new()
    {
        Text = text,
        TopColor = top,
        BottomColor = bottom,
        BorderColor = ThemePalette.Lighten(top, 0.26f),
        TextColor = textColor,
        Font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold, GraphicsUnit.Point),
        CornerRadius = 16,
        AccessibleName = text,
        AccessibleRole = AccessibleRole.PushButton
    };

    private void LayoutButtons()
    {
        int margin = Math.Max(24, ClientSize.Width / 35);
        int gap = 12;
        int width = Math.Max(100, ClientSize.Width - margin * 2);
        _removeButton.Bounds = new Rectangle(margin, ClientSize.Height - 144, width, 56);
        int half = (width - gap) / 2;
        _againButton.Bounds = new Rectangle(margin, ClientSize.Height - 75, half, 52);
        _returnButton.Bounds = new Rectangle(margin + half + gap, ClientSize.Height - 75, width - half - gap, 52);
    }

    private void Choose(ResultAction action)
    {
        if (_actionChosen) return;
        _actionChosen = true;
        Action = action;
        _removeButton.Enabled = _againButton.Enabled = _returnButton.Enabled = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ResultForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape || _actionChosen) return;
        _actionChosen = true;
        Action = ResultAction.Return;
        DialogResult = DialogResult.Cancel;
        e.Handled = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _canvas.StopAnimation();
        base.Dispose(disposing);
    }
}
