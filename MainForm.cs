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
    private readonly Dictionary<Control, float> _baseFontSizes = [];
    private readonly Dictionary<Control, Font> _scaledFonts = [];
    private readonly TableLayoutPanel _root;
    private TableLayoutPanel? _contentLayout;
    private TableLayoutPanel? _wheelLayout;
    private TableLayoutPanel? _sideLayout;
    private TableLayoutPanel? _headerLayout;
    private TableLayoutPanel? _titleStack;
    private FlowLayoutPanel? _toolsStack;
    private readonly List<TableLayoutPanel> _cycleStacks = [];
    private ResultForm? _activeResult;
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
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ControlBox = true;
        Font = new Font("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Point);
        KeyPreview = true;
        DoubleBuffered = true;
        // O dimensionamento é calculado manualmente a partir do DPI, então o escalonamento automático fica desligado.
        AutoScaleMode = AutoScaleMode.None;
        AccessibleName = "Roleta da Daily";
        ApplyStartupBounds();

        // A paleta assinatura (índice 0) abre por padrão para manter a identidade azul-marinho e dourada.
        _themeIndex = 0;
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
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
        FormClosing += (_, _) =>
        {
            _wheel.CancelSpin();
            if (_activeResult is { IsDisposed: false } active)
            {
                active.Close();
                _activeResult = null;
            }
        };
        FormClosed += (_, _) => _soundManager.Dispose();

        UpdateParticipants();
        ApplyTheme();
        _initializing = false;
        Resize += (_, _) => AdaptLayoutToSize();
        Shown += (_, _) =>
        {
            AdaptLayoutToSize();
            _spinButton.Focus();
        };
    }

    // Tamanho lógico em que o layout aparece na escala cheia; abaixo disso tudo encolhe proporcionalmente.
    private const float ReferenceWidth = 1120f;
    private const float ReferenceHeight = 760f;
    private static readonly Size MinimumLogicalSize = new(640, 470);

    private float DpiScale => DeviceDpi / 96f;

    private void ApplyStartupBounds()
    {
        ApplyMinimumSize();
        Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;

        int width = Math.Min((int)(1420 * DpiScale), (int)(work.Width * 0.82));
        int height = Math.Min((int)(900 * DpiScale), (int)(work.Height * 0.86));
        width = Math.Clamp(width, Math.Min(MinimumSize.Width, work.Width), work.Width);
        height = Math.Clamp(height, Math.Min(MinimumSize.Height, work.Height), work.Height);
        Bounds = new Rectangle(
            work.Left + (work.Width - width) / 2,
            work.Top + (work.Height - height) / 2,
            width,
            height);
    }

    private void ApplyMinimumSize()
    {
        Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
        MinimumSize = new Size(
            Math.Min((int)(MinimumLogicalSize.Width * DpiScale), work.Width),
            Math.Min((int)(MinimumLogicalSize.Height * DpiScale), work.Height));
    }

    private int Px(float logicalValue, float scale) =>
        Math.Max(0, (int)MathF.Round(logicalValue * scale * DpiScale));

    private void ApplyScaledFont(Control control, float scale)
    {
        if (!_baseFontSizes.TryGetValue(control, out float basePoints)) return;
        float size = MathF.Max(6.5f, MathF.Round(basePoints * scale, 1));
        if (Math.Abs(control.Font.Size - size) < 0.05f) return;
        var next = new Font(control.Font.FontFamily, size, control.Font.Style, GraphicsUnit.Point);
        control.Font = next;
        if (_scaledFonts.Remove(control, out Font? previous)) previous.Dispose();
        _scaledFonts[control] = next;
    }

    private void AdaptLayoutToSize()
    {
        if (_contentLayout is null || ClientSize.Width < 2 || ClientSize.Height < 2) return;

        float logicalWidth = ClientSize.Width / DpiScale;
        float logicalHeight = ClientSize.Height / DpiScale;
        // Uma única escala contínua evita saltos de layout e garante que nada seja cortado ao reduzir.
        float s = Math.Clamp(MathF.Min(logicalWidth / ReferenceWidth, logicalHeight / ReferenceHeight), 0.46f, 1f);

        SuspendLayout();

        _root.Padding = new Padding(Px(26, s), Px(21, s), Px(26, s), Px(24, s));
        _root.RowStyles[0].Height = Px(102, s);

        if (_titleStack is not null)
        {
            _titleStack.RowStyles[0].Height = Px(26, s);
            _titleStack.RowStyles[1].Height = Px(44, s);
        }
        if (_toolsStack is not null)
            _toolsStack.Padding = new Padding(0, Px(17, s), 0, 0);

        _modeButton.Size = new Size(Px(180, s), Px(48, s));
        _modeButton.Margin = new Padding(Px(10, s), 0, 0, 0);
        _modeButton.CornerRadius = Px(15, s);

        if (_wheelLayout is not null)
        {
            _wheelLayout.Padding = new Padding(Px(14, s), Px(9, s), Px(14, s), Px(10, s));
            _wheelLayout.RowStyles[1].Height = Px(28, s);
            _wheelLayout.RowStyles[2].Height = Px(60, s);
        }

        _wheel.MinimumSize = Size.Empty;
        _spinButton.Margin = new Padding(Px(8, s), Px(3, s), Px(8, s), 0);
        _spinButton.CornerRadius = Px(18, s);

        if (_sideLayout is not null)
        {
            _sideLayout.Padding = new Padding(Px(18, s), Px(15, s), Px(18, s), Px(14, s));
            _sideLayout.RowStyles[0].Height = Px(33, s);
            _sideLayout.RowStyles[1].Height = Px(29, s);
            _sideLayout.RowStyles[3].Height = Px(27, s);
            _sideLayout.RowStyles[4].Height = Px(105, s);
        }

        _namesBox.Margin = new Padding(0, Px(6, s), 0, Px(5, s));
        foreach (TableLayoutPanel stack in _cycleStacks)
        {
            stack.RowStyles[0].Height = Px(28, s);
            stack.Padding = new Padding(0, 0, Px(7, s), 0);
        }
        _themeButton.CornerRadius = _soundButton.CornerRadius = Px(10, s);

        foreach (Control control in _baseFontSizes.Keys)
            ApplyScaledFont(control, s);

        // Em janelas estreitas a roleta cede espaço para o painel de participantes continuar legível.
        float wheelShare = logicalWidth < 900 ? 52f : logicalWidth < 1020 ? 57f : 62f;
        _contentLayout.ColumnStyles[0].Width = wheelShare;
        _contentLayout.ColumnStyles[1].Width = 100f - wheelShare;
        int gutter = Px(10, s);
        _wheelCard.Margin = new Padding(0, 0, gutter, 0);
        _participantsCard.Margin = new Padding(Math.Max(2, gutter - Px(2, s)), 0, 0, 0);
        _wheelCard.CornerRadius = _participantsCard.CornerRadius = Px(18, s);

        ResumeLayout(true);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyMinimumSize();
        AdaptLayoutToSize();
    }

    private void BuildHeader()
    {
        _headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        // Sem RowStyle explícito a linha vira AutoSize e os filhos estouram a altura do cabeçalho.
        _headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.Controls.Add(_headerLayout, 0, 0);

        // Rótulos ocupam células fixas com reticências para que o título nunca estoure a coluna.
        _titleStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        _titleStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _titleStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        _titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Label eyebrow = MakeLabel("DAILY STAND-UP", 9.5f, true, true);
        eyebrow.Tag = "accent";
        eyebrow.ForeColor = _appTheme.Gold;
        eyebrow.AutoSize = false;
        eyebrow.AutoEllipsis = true;
        eyebrow.Dock = DockStyle.Fill;
        eyebrow.TextAlign = ContentAlignment.BottomLeft;
        eyebrow.Margin = Padding.Empty;
        Label title = MakeLabel("Quem será o próximo a falar?", 24, true, false);
        title.AutoSize = false;
        title.AutoEllipsis = true;
        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Margin = Padding.Empty;
        _titleStack.Controls.Add(eyebrow, 0, 0);
        _titleStack.Controls.Add(title, 0, 1);
        _headerLayout.Controls.Add(_titleStack, 0, 0);

        _toolsStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 17, 0, 0)
        };
        _modeButton.Text = "Modo claro";
        _modeButton.IconGlyph = "\uE706";
        _modeButton.Size = new Size(180, 48);
        _modeButton.CornerRadius = 15;
        _modeButton.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold, GraphicsUnit.Point);
        _modeButton.Margin = new Padding(10, 0, 0, 0);
        _baseFontSizes[_modeButton] = 10f;
        _toolsStack.Controls.Add(_modeButton);
        _headerLayout.Controls.Add(_toolsStack, 1, 0);
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
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _contentLayout = content;
        _root.Controls.Add(content, 0, 1);

        _wheelCard.Dock = DockStyle.Fill;
        _wheelCard.Margin = new Padding(0, 0, 10, 0);
        _wheelLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(14, 9, 14, 10),
            ColumnCount = 1,
            RowCount = 3
        };
        _wheelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _wheelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _wheelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _wheelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        _wheel.Dock = DockStyle.Fill;
        _wheel.MinimumSize = Size.Empty;
        _wheelLayout.Controls.Add(_wheel, 0, 0);

        Label hint = MakeLabel("TOQUE NA ROLETA PARA GIRAR", 9, true, true);
        hint.AutoSize = false;
        hint.AutoEllipsis = true;
        hint.TextAlign = ContentAlignment.MiddleCenter;
        hint.Dock = DockStyle.Fill;
        hint.Margin = new Padding(0);
        _wheelLayout.Controls.Add(hint, 0, 1);

        _spinButton.Text = "GIRAR A ROLETA";
        _spinButton.IconGlyph = "\uE72C";
        _spinButton.Dock = DockStyle.Fill;
        _spinButton.CornerRadius = 18;
        _spinButton.Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold, GraphicsUnit.Point);
        _spinButton.Margin = new Padding(8, 3, 8, 0);
        _baseFontSizes[_spinButton] = 14f;
        _wheelLayout.Controls.Add(_spinButton, 0, 2);
        _wheelCard.Controls.Add(_wheelLayout);
        content.Controls.Add(_wheelCard, 0, 0);

        _participantsCard.Dock = DockStyle.Fill;
        _participantsCard.Margin = new Padding(8, 0, 0, 0);
        _sideLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 15, 18, 14),
            ColumnCount = 1,
            RowCount = 5
        };
        _sideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        _sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        _sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        _sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));

        Label heading = MakeLabel("Participantes", 17, true, false);
        heading.AutoSize = false;
        heading.AutoEllipsis = true;
        heading.Dock = DockStyle.Fill;
        heading.TextAlign = ContentAlignment.MiddleLeft;
        heading.Margin = new Padding(0);
        _sideLayout.Controls.Add(heading, 0, 0);

        Label subtitle = MakeLabel("Insira ou cole um nome por linha.", 9.5f, false, true);
        subtitle.AutoSize = false;
        subtitle.AutoEllipsis = true;
        subtitle.Dock = DockStyle.Fill;
        subtitle.TextAlign = ContentAlignment.MiddleLeft;
        subtitle.Margin = new Padding(0);
        _sideLayout.Controls.Add(subtitle, 0, 1);

        _namesBox.Multiline = true;
        _namesBox.AcceptsReturn = true;
        _namesBox.AcceptsTab = false;
        _namesBox.ScrollBars = ScrollBars.Vertical;
        _namesBox.WordWrap = true;
        _namesBox.BorderStyle = BorderStyle.FixedSingle;
        _namesBox.Dock = DockStyle.Fill;
        _namesBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        _namesBox.Margin = new Padding(0, 6, 0, 5);
        _baseFontSizes[_namesBox] = 10.5f;
        _namesBox.AccessibleName = "Nomes dos participantes, um por linha";
        _namesBox.AccessibleDescription = "Linhas vazias são ignoradas. Duplicatas ignoram maiúsculas e minúsculas; a primeira grafia permanece.";
        _sideLayout.Controls.Add(_namesBox, 0, 2);

        _duplicateStatus.AutoSize = false;
        _duplicateStatus.Dock = DockStyle.Fill;
        _duplicateStatus.TextAlign = ContentAlignment.MiddleLeft;
        _duplicateStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        _duplicateStatus.Margin = new Padding(0);
        _duplicateStatus.Tag = "muted";
        _duplicateStatus.AutoEllipsis = true;
        _baseFontSizes[_duplicateStatus] = 8.5f;
        _sideLayout.Controls.Add(_duplicateStatus, 0, 3);

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
        options.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        options.Controls.Add(CreateCycleButton("Tema da roleta", _themeButton), 0, 0);
        options.Controls.Add(CreateCycleButton("Som da comemoração", _soundButton), 1, 0);
        _sideLayout.Controls.Add(options, 0, 4);
        _participantsCard.Controls.Add(_sideLayout);
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
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _cycleStacks.Add(stack);
        Label label = MakeLabel(caption, 9, true, true);
        label.AutoSize = false;
        label.AutoEllipsis = true;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Margin = new Padding(0);
        button.Dock = DockStyle.Fill;
        button.CornerRadius = 10;
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold, GraphicsUnit.Point);
        button.Margin = new Padding(0, 2, 0, 0);
        _baseFontSizes[button] = 9f;
        button.TopColor = _appTheme.Field;
        button.BottomColor = _appTheme.Field;
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
        _baseFontSizes[label] = size;
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

    private async void RevealWinner(string winner)
    {
        _isSpinning = false;
        _soundManager.Stop();
        _resultVisible = true;
        RefreshActionState();
        _soundManager.Play((SpinSound)_soundIndex);

        ResultAction action = await ShowResultOverlayAsync(winner);
        if (IsDisposed) return;

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

    private Task<ResultAction> ShowResultOverlayAsync(string winner)
    {
        var completion = new TaskCompletionSource<ResultAction>();
        var result = new ResultForm(winner, ThemePalette.All[_themeIndex], _appTheme);
        _activeResult = result;

        void Finish()
        {
            if (!ReferenceEquals(_activeResult, result)) return;
            _activeResult = null;
            ResultAction action = result.Action == ResultAction.None ? ResultAction.Return : result.Action;
            if (Controls.Contains(result)) Controls.Remove(result);
            result.Dispose();
            completion.TrySetResult(action);
        }

        result.FormClosed += (_, _) => Finish();
        Controls.Add(result);
        result.BringToFront();
        result.Show();
        return completion.Task;
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
                Equals(label.Tag, "accent") ? _appTheme.Gold : _appTheme.Text;
        _spinButton.TopColor = _appTheme.Gold;
        _spinButton.BottomColor = _appTheme.GoldDark;
        _spinButton.BorderColor = ThemePalette.Lighten(_appTheme.Gold, 0.35f);
        _spinButton.TextColor = Color.FromArgb(38, 29, 21);
        _modeButton.Text = _lightMode ? "Modo escuro" : "Modo claro";
        _modeButton.IconGlyph = _lightMode ? "\uE708" : "\uE706";
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
            CenterColor = _lightMode ? Color.FromArgb(48, 132, 164, 220) : Color.FromArgb(54, 46, 94, 176),
            SurroundColors = [_appTheme.BackgroundTop]
        };
        e.Graphics.FillPath(glow, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _soundManager.Dispose();
            foreach (Font font in _scaledFonts.Values) font.Dispose();
            _scaledFonts.Clear();
        }
        base.Dispose(disposing);
    }
}
