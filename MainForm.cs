using System.Drawing.Drawing2D;
using System.Security.Cryptography;

namespace RoletaDaDaily;

internal sealed class MainForm : Form
{
    private readonly UserPreferences _preferences = UserPreferences.Load();
    private readonly SoundManager _soundManager = new();
    private readonly RoundedPanel _wheelCard = new();
    private readonly RoundedPanel _participantsCard = new();
    private readonly WheelControl _wheel = new();
    private readonly TextBox _namesBox = new();
    private readonly RoundedButton _themeButton = new();
    private readonly RoundedButton _soundButton = new();
    private readonly RoundedButton _spinButton = new();
    private readonly RoundedButton _modeButton = new();
    private readonly Label _duplicateStatus = new();
    private readonly List<Label> _labels = [];
    private readonly TableLayoutPanel _root;
    private IReadOnlyList<string> _participants = Array.Empty<string>();
    private AppTheme _appTheme;
    private int _themeIndex;
    private int _soundIndex;
    private bool _lightMode;
    private bool _isSpinning;
    private bool _resultVisible;
    private bool _initializing = true;

    public MainForm()
    {
        Text = "Roleta da Daily";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Name = "RoletaDaDailyMainForm";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1000, 700);
        Size = new Size(1420, 900);
        Font = new Font("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Point);
        KeyPreview = true;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AccessibleName = "Roleta da Daily";

        _themeIndex = Random.Shared.Next(ThemePalette.All.Count);
        _soundIndex = Random.Shared.Next(SoundManager.Options.Count - 1);
        _lightMode = _preferences.LightMode;
        _appTheme = _lightMode ? AppTheme.Light : AppTheme.Dark;

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(26, 21, 26, 24),
            ColumnCount = 1,
            RowCount = 2,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(_root);

        BuildHeader();
        BuildContent();
        ConfigureCycleButtons();

        _wheel.SpinRequested += (_, _) => BeginSpin();
        _wheel.SpinStopped += (_, _) => _soundManager.Stop();
        _wheel.SpinCompleted += (_, winner) => RevealWinner(winner);
        _namesBox.TextChanged += (_, _) => UpdateParticipants();
        _namesBox.Leave += (_, _) => NormalizeNamesInEditor();
        _spinButton.Click += (_, _) => BeginSpin();
        _modeButton.Click += (_, _) => ToggleMode();
        _themeButton.Click += (_, _) => ChangeTheme();
        _soundButton.Click += (_, _) => ChangeSound();
        KeyDown += MainForm_KeyDown;
        FormClosed += (_, _) => { _wheel.CancelSpin(); _soundManager.Dispose(); };

        UpdateParticipants();
        ApplyTheme();
        _initializing = false;
        Shown += (_, _) => _spinButton.Focus();
    }

    private void BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        _root.Controls.Add(header, 0, 0);

        var titleStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };
        Label eyebrow = MakeLabel("DAILY STAND-UP", 9.5f, true, true);
        eyebrow.Tag = "accent";
        eyebrow.ForeColor = Color.FromArgb(235, 191, 110);
        Label title = MakeLabel("Quem será o próximo a falar?", 24, true, false);
        title.Margin = new Padding(0, 2, 0, 0);
        titleStack.Controls.Add(eyebrow);
        titleStack.Controls.Add(title);
        header.Controls.Add(titleStack, 0, 0);

        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 17, 0, 0)
        };
        _modeButton.Text = "☼  Modo claro";
        _modeButton.Size = new Size(180, 48);
        _modeButton.CornerRadius = 15;
        _modeButton.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold, GraphicsUnit.Point);
        _modeButton.Margin = new Padding(10, 0, 0, 0);
        tools.Controls.Add(_modeButton);
        header.Controls.Add(tools, 1, 0);
    }

    private void BuildContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _root.Controls.Add(content, 0, 1);

        _wheelCard.Dock = DockStyle.Fill;
        _wheelCard.Margin = new Padding(0, 0, 10, 0);
        var wheelLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(14, 9, 14, 10),
            ColumnCount = 1,
            RowCount = 3
        };
        wheelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wheelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        wheelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        _wheel.Dock = DockStyle.Fill;
        _wheel.MinimumSize = new Size(300, 300);
        wheelLayout.Controls.Add(_wheel, 0, 0);

        Label hint = MakeLabel("TOQUE NA ROLETA PARA GIRAR", 9, true, true);
        hint.TextAlign = ContentAlignment.MiddleCenter;
        hint.Dock = DockStyle.Fill;
        hint.Margin = new Padding(0);
        wheelLayout.Controls.Add(hint, 0, 1);

        _spinButton.Text = "🎲  GIRAR A ROLETA";
        _spinButton.Dock = DockStyle.Fill;
        _spinButton.CornerRadius = 18;
        _spinButton.Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold, GraphicsUnit.Point);
        _spinButton.Margin = new Padding(8, 3, 8, 0);
        wheelLayout.Controls.Add(_spinButton, 0, 2);
        _wheelCard.Controls.Add(wheelLayout);
        content.Controls.Add(_wheelCard, 0, 0);

        _participantsCard.Dock = DockStyle.Fill;
        _participantsCard.Margin = new Padding(8, 0, 0, 0);
        var side = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 15, 18, 14),
            ColumnCount = 1,
            RowCount = 5
        };
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));

        Label heading = MakeLabel("Participantes", 17, true, false);
        heading.Dock = DockStyle.Fill;
        heading.TextAlign = ContentAlignment.MiddleLeft;
        heading.Margin = new Padding(0);
        side.Controls.Add(heading, 0, 0);

        Label subtitle = MakeLabel("Insira ou cole um nome por linha.", 9.5f, false, true);
        subtitle.Dock = DockStyle.Fill;
        subtitle.TextAlign = ContentAlignment.MiddleLeft;
        subtitle.Margin = new Padding(0);
        side.Controls.Add(subtitle, 0, 1);

        _namesBox.Multiline = true;
        _namesBox.AcceptsReturn = true;
        _namesBox.AcceptsTab = false;
        _namesBox.ScrollBars = ScrollBars.Vertical;
        _namesBox.WordWrap = true;
        _namesBox.BorderStyle = BorderStyle.FixedSingle;
        _namesBox.Dock = DockStyle.Fill;
        _namesBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        _namesBox.Margin = new Padding(0, 6, 0, 5);
        _namesBox.AccessibleName = "Nomes dos participantes, um por linha";
        _namesBox.AccessibleDescription = "Linhas vazias são ignoradas. Duplicatas ignoram maiúsculas e minúsculas; a primeira grafia permanece.";
        side.Controls.Add(_namesBox, 0, 2);

        _duplicateStatus.AutoSize = false;
        _duplicateStatus.Dock = DockStyle.Fill;
        _duplicateStatus.TextAlign = ContentAlignment.MiddleLeft;
        _duplicateStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        _duplicateStatus.Margin = new Padding(0);
        _duplicateStatus.Tag = "muted";
        side.Controls.Add(_duplicateStatus, 0, 3);

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0)
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.Controls.Add(CreateCycleButton("Tema da roleta", _themeButton), 0, 0);
        options.Controls.Add(CreateCycleButton("Som da comemoração", _soundButton), 1, 0);
        side.Controls.Add(options, 0, 4);
        _participantsCard.Controls.Add(side);
        content.Controls.Add(_participantsCard, 1, 0);
    }

    private Control CreateCycleButton(string caption, RoundedButton button)
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 7, 0)
        };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Label label = MakeLabel(caption, 9, true, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Margin = new Padding(0);
        button.Dock = DockStyle.Fill;
        button.CornerRadius = 10;
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold, GraphicsUnit.Point);
        button.Margin = new Padding(0, 2, 0, 0);
        button.TopColor = Color.FromArgb(41, 47, 64);
        button.BottomColor = Color.FromArgb(33, 39, 55);
        button.TextColor = _appTheme.Text;
        button.BorderColor = _appTheme.PanelBorder;
        button.AccessibleName = caption;
        button.AccessibleDescription = "Clique para avançar para a próxima opção.";
        stack.Controls.Add(label, 0, 0);
        stack.Controls.Add(button, 0, 1);
        return stack;
    }

    private void ConfigureCycleButtons()
    {
        UpdateCycleButtonText();
    }

    private Label MakeLabel(string text, float size, bool bold, bool muted)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size,
                bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
            BackColor = Color.Transparent,
            Tag = muted ? "muted" : null,
            Margin = new Padding(0, 1, 0, 1)
        };
        _labels.Add(label);
        return label;
    }

    private void UpdateParticipants()
    {
        ParticipantParseResult parsed = ParticipantService.Parse(_namesBox.Text);
        _participants = parsed.Names;
        _wheel.Names = _participants;
        _wheel.AccessibleDescription = _participants.Count == 0
            ? "A roleta está vazia. Adicione participantes e use o botão Girar a Roleta."
            : $"Roleta com {_participants.Count} participantes. Use o botão Girar a Roleta para sortear.";
        UpdateDuplicateStatus(parsed.DuplicateCount);
        RefreshActionState();
    }

    private void NormalizeNamesInEditor()
    {
        string normalized = string.Join(Environment.NewLine,
            _namesBox.Lines.Select(line => ParticipantService.NormalizeName(line.Trim())));
        if (normalized == _namesBox.Text) return;
        int selectionStart = Math.Min(_namesBox.SelectionStart, normalized.Length);
        _namesBox.Text = normalized;
        _namesBox.SelectionStart = selectionStart;
    }

    private void UpdateDuplicateStatus(int duplicateCount)
    {
        if (duplicateCount == 0)
        {
            _duplicateStatus.Text = "Nomes repetidos são ignorados; a primeira grafia é mantida.";
            _duplicateStatus.ForeColor = _appTheme.Muted;
        }
        else
        {
            _duplicateStatus.Text = $"{duplicateCount} duplicata(s) ignorada(s); prevalece a primeira grafia.";
            _duplicateStatus.ForeColor = _lightMode ? Color.FromArgb(143, 83, 26) : Color.FromArgb(245, 199, 118);
        }
    }

    private void RefreshActionState()
    {
        bool busy = _isSpinning || _resultVisible;
        _spinButton.Enabled = _participants.Count > 0 && !busy;
        _wheel.Enabled = _participants.Count > 0 && !busy;
        _namesBox.ReadOnly = busy;
        _themeButton.Enabled = !busy;
        _soundButton.Enabled = !busy;
        _modeButton.Enabled = !busy;
        _spinButton.Cursor = _spinButton.Enabled ? Cursors.Hand : Cursors.Default;
        _wheel.Cursor = _wheel.Enabled ? Cursors.Hand : Cursors.Default;
    }

    private void BeginSpin()
    {
        if (_isSpinning || _resultVisible || _participants.Count == 0) return;
        // A roleta recebe um retrato imutável da lista e os campos ficam bloqueados até a decisão final.
        _wheel.Names = _participants.ToArray();
        _isSpinning = true;
        RefreshActionState();
        _soundManager.PlaySpin((SpinSound)_soundIndex);
        _wheel.StartSpin();
    }

    private void RevealWinner(string winner)
    {
        _isSpinning = false;
        _soundManager.Stop();
        _resultVisible = true;
        RefreshActionState();
        _soundManager.Play((SpinSound)_soundIndex);

        ResultAction action;
        using (var result = new ResultForm(this, winner, ThemePalette.All[_themeIndex], _appTheme))
        {
            result.ShowDialog(this);
            action = result.Action;
        }

        _resultVisible = false;
        RefreshActionState();
        if (action == ResultAction.RemoveAndReturn)
        {
            IEnumerable<string> remaining = _participants.Where(n => !string.Equals(n, winner, StringComparison.OrdinalIgnoreCase));
            _namesBox.Text = ParticipantService.Format(remaining);
        }
        else if (action == ResultAction.SpinAgain)
        {
            BeginInvoke(new Action(BeginSpin));
        }
    }

    private void ChangeTheme()
    {
        if (_initializing) return;
        _themeIndex = (_themeIndex + 1) % ThemePalette.All.Count;
        UpdateCycleButtonText();
        _wheel.Palette = ThemePalette.All[_themeIndex];
        ApplyTheme();
    }

    private void ChangeSound()
    {
        if (_initializing) return;
        _soundIndex = (_soundIndex + 1) % SoundManager.Options.Count;
        UpdateCycleButtonText();
    }

    private void UpdateCycleButtonText()
    {
        _themeButton.Text = $"{ThemePalette.All[_themeIndex].Name}  ›";
        string soundName = SoundManager.Options[_soundIndex];
        _soundButton.Text = $"{soundName}  ›";
        _themeButton.AccessibleDescription = $"Tema atual: {ThemePalette.All[_themeIndex].Name}. Clique para avançar.";
        _soundButton.AccessibleDescription = $"Som atual: {soundName}. Clique para avançar.";
    }

    private void ToggleMode()
    {
        _lightMode = !_lightMode;
        _preferences.LightMode = _lightMode;
        _preferences.Save();
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        _appTheme = _lightMode ? AppTheme.Light : AppTheme.Dark;
        BackColor = _appTheme.BackgroundBottom;
        _wheel.Palette = ThemePalette.All[_themeIndex];
        _wheelCard.BackColor = _appTheme.Panel;
        _wheelCard.BorderColor = _appTheme.PanelBorder;
        _participantsCard.BackColor = _appTheme.Panel;
        _participantsCard.BorderColor = _appTheme.PanelBorder;
        _namesBox.BackColor = _appTheme.Field;
        _namesBox.ForeColor = _appTheme.Text;
        _namesBox.BorderStyle = BorderStyle.FixedSingle;
        foreach (Label label in _labels)
            label.ForeColor = Equals(label.Tag, "muted") ? _appTheme.Muted :
                Equals(label.Tag, "accent") ? Color.FromArgb(235, 191, 110) : _appTheme.Text;
        _spinButton.TopColor = _appTheme.Gold;
        _spinButton.BottomColor = _appTheme.GoldDark;
        _spinButton.BorderColor = ThemePalette.Lighten(_appTheme.Gold, 0.35f);
        _spinButton.TextColor = Color.FromArgb(38, 29, 21);
        _modeButton.Text = _lightMode ? "☾  Modo escuro" : "☼  Modo claro";
        _modeButton.TopColor = _appTheme.Panel;
        _modeButton.BottomColor = _appTheme.Panel;
        _modeButton.BorderColor = _appTheme.PanelBorder;
        _modeButton.TextColor = _appTheme.Text;
        _themeButton.TopColor = _appTheme.Field;
        _themeButton.BottomColor = _appTheme.Field;
        _themeButton.BorderColor = _appTheme.FieldBorder;
        _themeButton.TextColor = _appTheme.Text;
        _soundButton.TopColor = _appTheme.Field;
        _soundButton.BottomColor = _appTheme.Field;
        _soundButton.BorderColor = _appTheme.FieldBorder;
        _soundButton.TextColor = _appTheme.Text;
        UpdateDuplicateStatus(ParticipantService.Parse(_namesBox.Text).DuplicateCount);
        Invalidate(true);
        _wheel.Invalidate();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || _namesBox.Focused || _themeButton.Focused || _soundButton.Focused ||
            _isSpinning || _resultVisible || _participants.Count == 0)
            return;
        BeginSpin();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Rectangle bounds = ClientRectangle;
        if (bounds.Width < 2 || bounds.Height < 2) return;
        using var gradient = new LinearGradientBrush(bounds, _appTheme.BackgroundTop, _appTheme.BackgroundBottom, LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(gradient, bounds);
        RectangleF glowBounds = new(bounds.Width * 0.18f, -bounds.Height * 0.1f, bounds.Width * 0.72f, bounds.Height * 0.78f);
        using var path = new GraphicsPath();
        path.AddEllipse(glowBounds);
        using var glow = new PathGradientBrush(path)
        {
            CenterColor = _lightMode ? Color.FromArgb(45, 165, 131, 202) : Color.FromArgb(48, 105, 68, 151),
            SurroundColors = [_appTheme.BackgroundTop]
        };
        e.Graphics.FillPath(glow, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _soundManager.Dispose();
        base.Dispose(disposing);
    }
}
