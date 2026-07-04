using System;
using System.Collections.Generic;
using Godot;

// Code-built UI: a scenario menu on the left, a big centered status line (countdown /
// "FIGHT!" / winner), and a result card with a "Back to Menu" button.
public partial class BattleHud : CanvasLayer
{
    public event Action<int> ScenarioSelected;
    public event Action MenuRequested;

    private ScrollContainer _menuScroll;
    private VBoxContainer _menu;
    private Label _status;
    private Control _resultCard;
    private Label _resultLabel;
    private Label _hint;
    private Label _info;
    private ProgressBar _pBar;
    private ProgressBar _eBar;
    private Label _ko;

    public override void _Ready()
    {
        // A scrollable host so the wheel scrolls the list (and is consumed here) rather than
        // zooming the camera behind it. Anchored down the left edge, full height.
        _menuScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = 28f, OffsetRight = 440f, OffsetTop = 24f, OffsetBottom = -24f,
        };
        AddChild(_menuScroll);

        _menu = new VBoxContainer();
        _menu.AddThemeConstantOverride("separation", 4);
        _menuScroll.AddChild(_menu);

        _status = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _status.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _status.OffsetTop = 24f;
        _status.OffsetBottom = 100f;
        _status.AddThemeFontSizeOverride("font_size", 44);
        _status.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_status);

        _hint = new Label
        {
            Text = "Wheel: zoom    Right-drag: pan    C: cinematic    R: restart    Esc: menu",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _hint.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hint.OffsetTop = -34f;
        _hint.OffsetBottom = -10f;
        _hint.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 0.7f));
        _hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _hint.Visible = false;
        AddChild(_hint);

        _info = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _info.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _info.OffsetLeft = -420f;
        _info.OffsetRight = -20f;
        _info.OffsetTop = 16f;
        _info.AddThemeFontSizeOverride("font_size", 16);
        _info.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.96f, 0.85f));
        _info.MouseFilter = Control.MouseFilterEnum.Ignore;
        _info.Visible = false;
        AddChild(_info);

        BuildArmyBars();
        BuildKo();
        BuildResultCard();
    }

    // Two fighting-game-style army bars: blue depletes toward the left corner, red toward the
    // right, so damage eats each side's health outward from the center.
    private void BuildArmyBars()
    {
        _pBar = MakeBar(new Color(0.32f, 0.55f, 1f), ProgressBar.FillModeEnum.BeginToEnd);
        _pBar.AnchorLeft = 0f; _pBar.AnchorRight = 0.5f;
        _pBar.OffsetLeft = 40f; _pBar.OffsetRight = -14f;
        _pBar.OffsetTop = 20f; _pBar.OffsetBottom = 48f;
        AddChild(_pBar);

        _eBar = MakeBar(new Color(1f, 0.36f, 0.3f), ProgressBar.FillModeEnum.EndToBegin);
        _eBar.AnchorLeft = 0.5f; _eBar.AnchorRight = 1f;
        _eBar.OffsetLeft = 14f; _eBar.OffsetRight = -40f;
        _eBar.OffsetTop = 20f; _eBar.OffsetBottom = 48f;
        AddChild(_eBar);

        AddBarLabel("BLUE", HorizontalAlignment.Left, 0f, 0.5f, 46f, -14f);
        AddBarLabel("RED", HorizontalAlignment.Right, 0.5f, 1f, 14f, -46f);
    }

    private ProgressBar MakeBar(Color fill, ProgressBar.FillModeEnum mode)
    {
        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = 1,
            ShowPercentage = false,
            FillMode = (int)mode,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.5f),
            BorderColor = new Color(0f, 0f, 0f, 0.7f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        bg.SetBorderWidthAll(2);
        var f = new StyleBoxFlat
        {
            BgColor = fill,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        };
        bar.AddThemeStyleboxOverride("background", bg);
        bar.AddThemeStyleboxOverride("fill", f);
        bar.Visible = false;
        return bar;
    }

    private void AddBarLabel(string text, HorizontalAlignment align, float aLeft, float aRight, float offLeft, float offRight)
    {
        var label = new Label { Text = text, HorizontalAlignment = align };
        label.AnchorLeft = aLeft; label.AnchorRight = aRight;
        label.OffsetLeft = offLeft; label.OffsetRight = offRight;
        label.OffsetTop = 50f; label.OffsetBottom = 70f;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.96f, 0.85f));
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(label);
    }

    private void BuildKo()
    {
        _ko = new Label
        {
            Text = "K.O.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _ko.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _ko.AddThemeFontSizeOverride("font_size", 130);
        _ko.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        _ko.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0f, 0f));
        _ko.AddThemeConstantOverride("outline_size", 12);
        _ko.MouseFilter = Control.MouseFilterEnum.Ignore;
        _ko.Visible = false;
        AddChild(_ko);
    }

    public void BuildMenu(IReadOnlyList<Scenario> scenarios)
    {
        foreach (Node child in _menu.GetChildren()) child.QueueFree();

        var title = new Label { Text = "BATTLE SIMULATOR" };
        title.AddThemeFontSizeOverride("font_size", 30);
        _menu.AddChild(title);

        var pick = new Label { Text = "Pick a matchup to watch:" };
        pick.AddThemeFontSizeOverride("font_size", 15);
        pick.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
        _menu.AddChild(pick);
        _menu.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 8f) });

        for (int i = 0; i < scenarios.Count; i++)
        {
            var s = scenarios[i];
            int index = i;
            var btn = new Button
            {
                Text = s.Name,
                TooltipText = s.Description,
                CustomMinimumSize = new Vector2(360f, 40f),
                Alignment = HorizontalAlignment.Left,
            };
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.Pressed += () => ScenarioSelected?.Invoke(index);
            _menu.AddChild(btn);
            _menu.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 6f) });
        }
    }

    private void BuildResultCard()
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(320f, 0f) };
        panel.AddChild(box);

        _resultLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _resultLabel.AddThemeFontSizeOverride("font_size", 40);
        box.AddChild(_resultLabel);

        var back = new Button { Text = "Back to Menu", CustomMinimumSize = new Vector2(0f, 40f) };
        back.Pressed += () => MenuRequested?.Invoke();
        box.AddChild(back);

        _resultCard = center;
        _resultCard.Visible = false;
    }

    public void ShowMenu()
    {
        _menuScroll.Visible = true;
        _resultCard.Visible = false;
        _hint.Visible = false;
        _info.Visible = false;
        _pBar.Visible = false;
        _eBar.Visible = false;
        _ko.Visible = false;
        _status.Text = "";
    }

    public void ShowBattle()
    {
        _menuScroll.Visible = false;
        _resultCard.Visible = false;
        _hint.Visible = false;
        _info.Visible = true;
        _pBar.Visible = true;
        _eBar.Visible = true;
        _ko.Visible = false;
        _pBar.Value = 1;
        _eBar.Value = 1;
    }

    public void SetStatus(string text) => _status.Text = text;

    public void SetInfo(string text) => _info.Text = text;

    public void SetArmyHp(float playerRatio, float enemyRatio)
    {
        _pBar.Value = Mathf.Clamp(playerRatio, 0f, 1f);
        _eBar.Value = Mathf.Clamp(enemyRatio, 0f, 1f);
    }

    public void ShowKo() => _ko.Visible = true;

    public void HideKo() => _ko.Visible = false;

    public void ShowResult(string text)
    {
        _ko.Visible = false;
        _resultLabel.Text = text;
        _resultCard.Visible = true;
        _status.Text = "";
    }
}
