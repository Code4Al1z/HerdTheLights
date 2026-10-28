using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MenuController : MonoBehaviour
{
    private static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);
    private static readonly Color Orange = new Color(1f, 0.6f, 0.15f);
    private static readonly Color Dark = new Color(0.02f, 0.05f, 0.09f, 0.94f);
    private static readonly Color Muted = new Color(0.7f, 0.8f, 0.85f);

    [SerializeField] private RoundController round;
    [SerializeField] private UIDocument document;
    [SerializeField] private int minTeam = 1;
    [SerializeField] private int maxTeam = 6;

    private readonly List<Button> teamButtons = new List<Button>();
    private VisualElement root;
    private Label teamLabel;
    private Button windowButton;
    private int selected = 1;
    private float shownAt;
    private bool wasVisible;
    private bool ready;

    private void Start()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (round == null || document == null)
        {
            Debug.LogError("MenuController needs a RoundController and a UIDocument.", this);
            enabled = false;
            return;
        }

        selected = Mathf.Clamp(round.TeamSize, minTeam, maxTeam);
        root = document.rootVisualElement;
        Build();
        Refresh();
        ready = true;
    }

    private void Update()
    {
        if (!ready) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f11Key.wasPressedThisFrame) ToggleFullscreen();

        bool visible = round.Phase == RoundPhase.Ready;
        root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible && !wasVisible) shownAt = Time.unscaledTime;
        wasVisible = visible;
        if (!visible) return;

        windowButton.text = Screen.fullScreen ? "SWITCH TO WINDOW  (F11)" : "SWITCH TO FULLSCREEN  (F11)";
        if (Time.unscaledTime - shownAt < 0.3f || keyboard == null) return;

        for (int n = minTeam; n <= maxTeam; n++)
        {
            if (keyboard[(Key)((int)Key.Digit1 + n - 1)].wasPressedThisFrame) Select(n);
        }
        if (keyboard.leftArrowKey.wasPressedThisFrame) Select(selected - 1);
        if (keyboard.rightArrowKey.wasPressedThisFrame) Select(selected + 1);
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) StartGame();
        if (keyboard.escapeKey.wasPressedThisFrame) Quit();
    }

    private void Select(int n)
    {
        selected = Mathf.Clamp(n, minTeam, maxTeam);
        Refresh();
    }

    private void StartGame()
    {
        round.SetTeamSize(selected);
        round.StartRound(0);
    }

    private static void ToggleFullscreen()
    {
        if (Screen.fullScreen)
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            return;
        }

        Resolution display = Screen.currentResolution;
        Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Refresh()
    {
        for (int i = 0; i < teamButtons.Count; i++)
        {
            bool on = minTeam + i == selected;
            Button button = teamButtons[i];
            button.style.backgroundColor = on ? Cyan : new Color(1f, 1f, 1f, 0.06f);
            button.style.color = on ? new Color(0.02f, 0.05f, 0.09f) : Color.white;
        }

        teamLabel.text = selected == 1 ? "Solo: just you" : $"You and {selected - 1} bot teammate{(selected > 2 ? "s" : string.Empty)} (demo stand-ins for a real group)";
    }

    private void Build()
    {
        root.Clear();
        root.pickingMode = PickingMode.Position;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.backgroundColor = Dark;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        Label title = MakeLabel(root, 120, Color.white, true);
        title.text = "HERD THE LIGHTS";
        title.style.letterSpacing = 10f;

        Label subtitle = MakeLabel(root, 34, Cyan, false);
        subtitle.text = "Three rounds. Herd the glimmers into the pen, then seal the gate together.";
        subtitle.style.marginBottom = 50;

        Label players = MakeLabel(root, 30, Muted, true);
        players.text = "PLAYERS";
        players.style.letterSpacing = 6f;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginTop = 14;
        root.Add(row);
        for (int n = minTeam; n <= maxTeam; n++)
        {
            int value = n;
            Button button = new Button(() => Select(value));
            button.text = n.ToString();
            button.style.width = 96;
            button.style.height = 96;
            button.style.fontSize = 48;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.marginLeft = 8;
            button.style.marginRight = 8;
            Border(button, 3, Cyan);
            Radius(button, 16);
            row.Add(button);
            teamButtons.Add(button);
        }

        teamLabel = MakeLabel(root, 30, Muted, false);
        teamLabel.style.marginTop = 18;
        teamLabel.style.marginBottom = 44;

        Button start = new Button(StartGame);
        start.text = "START";
        start.style.width = 420;
        start.style.height = 96;
        start.style.fontSize = 48;
        start.style.letterSpacing = 10f;
        start.style.unityFontStyleAndWeight = FontStyle.Bold;
        start.style.backgroundColor = Orange;
        start.style.color = new Color(0.02f, 0.05f, 0.09f);
        Border(start, 0, Orange);
        Radius(start, 20);
        root.Add(start);

        windowButton = new Button(ToggleFullscreen);
        windowButton.style.width = 420;
        windowButton.style.height = 56;
        windowButton.style.fontSize = 24;
        windowButton.style.marginTop = 22;
        windowButton.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
        windowButton.style.color = Color.white;
        Border(windowButton, 2, Cyan);
        Radius(windowButton, 14);
        root.Add(windowButton);

        Label how = MakeLabel(root, 26, Muted, false);
        how.text = "Move the mouse to herd. Left click drops a lure.\nWhen the seal phase starts, stand in your gate slot to close the gate.";
        how.style.marginTop = 40;
        how.style.unityTextAlign = TextAnchor.MiddleCenter;

        Label keys = MakeLabel(root, 22, new Color(0.55f, 0.65f, 0.7f), false);
        keys.text = "1-6 or arrow keys: players      Enter: start      F11: window      Esc: quit";
        keys.style.marginTop = 24;
    }

    private static Label MakeLabel(VisualElement parent, int size, Color color, bool bold)
    {
        Label label = new Label();
        label.pickingMode = PickingMode.Ignore;
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        parent.Add(label);
        return label;
    }

    private static void Radius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    private static void Border(VisualElement element, float width, Color color)
    {
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
    }
}