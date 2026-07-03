using System;
using System.Collections.Generic;
using Godot;

// Code-built UI: a scenario menu on the left, a big centered status line (countdown /
// "FIGHT!" / winner), and a result card with a "Back to Menu" button.
public partial class BattleHud : CanvasLayer
{
    public event Action<int> ScenarioSelected;
    public event Action MenuRequested;

    private VBoxContainer _menu;
    private Label _status;
    private Control _resultCard;
    private Label _resultLabel;
    private Label _hint;

    public override void _Ready()
    {
        _menu = new VBoxContainer { Position = new Vector2(28f, 30f) };
        AddChild(_menu);

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
            Text = "Mouse wheel: zoom   ·   Right-drag: pan   ·   C: cinematic   ·   R: restart   ·   Esc: menu",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _hint.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hint.OffsetTop = -34f;
        _hint.OffsetBottom = -10f;
        _hint.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 0.7f));
        _hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _hint.Visible = false;
        AddChild(_hint);

        BuildResultCard();
    }

    public void BuildMenu(IReadOnlyList<Scenario> scenarios)
    {
        foreach (Node child in _menu.GetChildren()) child.QueueFree();

        var title = new Label { Text = "⚔  BATTLE SIMULATOR" };
        title.AddThemeFontSizeOverride("font_size", 32);
        _menu.AddChild(title);

        var pick = new Label { Text = "Pick a matchup to watch:" };
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
                CustomMinimumSize = new Vector2(300f, 40f),
                Alignment = HorizontalAlignment.Left,
            };
            btn.Pressed += () => ScenarioSelected?.Invoke(index);
            _menu.AddChild(btn);

            var desc = new Label { Text = "   " + s.Description };
            desc.AddThemeFontSizeOverride("font_size", 12);
            desc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            _menu.AddChild(desc);
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
        _menu.Visible = true;
        _resultCard.Visible = false;
        _hint.Visible = false;
        _status.Text = "";
    }

    public void ShowBattle()
    {
        _menu.Visible = false;
        _resultCard.Visible = false;
        _hint.Visible = true;
    }

    public void SetStatus(string text) => _status.Text = text;

    public void ShowResult(string text)
    {
        _resultLabel.Text = text;
        _resultCard.Visible = true;
        _status.Text = "";
    }
}
