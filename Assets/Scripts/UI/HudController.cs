using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HudController : MonoBehaviour
{
    [SerializeField] private RoundController round;
    [SerializeField] private UIDocument document;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float popupLife = 1.1f;
    [SerializeField] private float popupRise = 80f;

    private static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);
    private static readonly Color Orange = new Color(1f, 0.6f, 0.15f);
    private static readonly Color Red = new Color(1f, 0.3f, 0.3f);
    private static readonly Color Green = new Color(0.4f, 1f, 0.55f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    private static readonly Color Panel = new Color(0.02f, 0.05f, 0.09f, 0.82f);

    private class Popup
    {
        public Label label;
        public Vector3 world;
        public float age;
    }

    private readonly List<Popup> popups = new List<Popup>();
    private VisualElement root;
    private VisualElement topBar;
    private VisualElement sealTrack;
    private VisualElement sealFill;
    private VisualElement resultCard;
    private VisualElement centerLayer;
    private Label timerLabel;
    private Label scoreLabel;
    private Label penLabel;
    private Label countdownLabel;
    private Label bannerLabel;
    private Label gradeLabel;
    private Label totalLabel;
    private Label breakdownLabel;
    private Label rankLabel;
    private Label hintLabel;
    private int lastScore;
    private float punch;
    private bool ready;

    private void Start()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (round == null || document == null)
        {
            Debug.LogError("HudController needs a RoundController and a UIDocument.", this);
            enabled = false;
            return;
        }

        root = document.rootVisualElement;
        Build();
        round.GlimmerBanked += OnBanked;
        round.GlimmerLeaked += OnLeaked;
        ready = true;
    }

    private void OnDestroy()
    {
        if (round == null) return;
        round.GlimmerBanked -= OnBanked;
        round.GlimmerLeaked -= OnLeaked;
    }

    private void Build()
    {
        root.Clear();
        root.pickingMode = PickingMode.Ignore;
        Stretch(root);

        topBar = new VisualElement();
        topBar.pickingMode = PickingMode.Ignore;
        topBar.style.position = Position.Absolute;
        topBar.style.top = 18;
        topBar.style.left = 0;
        topBar.style.right = 0;
        topBar.style.alignItems = Align.Center;
        root.Add(topBar);

        timerLabel = MakeLabel(topBar, 96, Color.white);
        scoreLabel = MakeLabel(topBar, 64, Cyan);
        penLabel = MakeLabel(topBar, 28, new Color(0.7f, 0.85f, 0.9f), false);
        penLabel.style.letterSpacing = 3f;

        sealTrack = new VisualElement();
        sealTrack.pickingMode = PickingMode.Ignore;
        sealTrack.style.width = 520;
        sealTrack.style.height = 16;
        sealTrack.style.marginTop = 10;
        sealTrack.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
        Radius(sealTrack, 8);
        sealFill = new VisualElement();
        sealFill.pickingMode = PickingMode.Ignore;
        sealFill.style.height = Length.Percent(100f);
        sealFill.style.width = Length.Percent(0f);
        sealFill.style.backgroundColor = Green;
        Radius(sealFill, 8);
        sealTrack.Add(sealFill);
        topBar.Add(sealTrack);

        centerLayer = new VisualElement();
        centerLayer.pickingMode = PickingMode.Ignore;
        Stretch(centerLayer);
        centerLayer.style.justifyContent = Justify.Center;
        centerLayer.style.alignItems = Align.Center;
        root.Add(centerLayer);

        countdownLabel = MakeLabel(centerLayer, 300, Color.white);
        bannerLabel = MakeLabel(centerLayer, 72, Orange);
        bannerLabel.style.letterSpacing = 8f;

        resultCard = new VisualElement();
        resultCard.pickingMode = PickingMode.Ignore;
        resultCard.style.position = Position.Absolute;
        resultCard.style.left = Length.Percent(50f);
        resultCard.style.top = Length.Percent(50f);
        resultCard.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
        resultCard.style.alignItems = Align.Center;
        resultCard.style.paddingTop = 30;
        resultCard.style.paddingBottom = 30;
        resultCard.style.paddingLeft = 70;
        resultCard.style.paddingRight = 70;
        resultCard.style.backgroundColor = Panel;
        Radius(resultCard, 20);
        Border(resultCard, 3, Cyan);
        root.Add(resultCard);

        gradeLabel = MakeLabel(resultCard, 220, Gold);
        totalLabel = MakeLabel(resultCard, 80, Color.white);
        breakdownLabel = MakeLabel(resultCard, 32, new Color(0.75f, 0.9f, 0.95f), false);
        rankLabel = MakeLabel(resultCard, 36, Gold);
        hintLabel = MakeLabel(resultCard, 28, new Color(0.7f, 0.8f, 0.85f), false);
        hintLabel.style.marginTop = 16;
        hintLabel.text = "R: replay      N: next round";
    }

    private void Update()
    {
        if (!ready) return;

        RoundPhase phase = round.Phase;
        bool playing = phase == RoundPhase.Herd || phase == RoundPhase.Seal;
        bool intro = phase == RoundPhase.Intro;
        bool result = phase == RoundPhase.Result;

        Show(topBar, playing || intro || result);
        Show(centerLayer, intro || playing);
        Show(resultCard, result);
        Show(countdownLabel, intro);
        Show(penLabel, !intro);

        UpdateTopBar(playing || result);
        UpdateBanner(phase);
        if (result) UpdateResult();
        UpdatePopups();
    }

    private void UpdateTopBar(bool live)
    {
        float timeLeft = round.TimeLeft;
        int seconds = Mathf.CeilToInt(timeLeft);
        timerLabel.text = $"{seconds / 60}:{seconds % 60:00}";

        Color timerColor = Color.white;
        if (live && timeLeft <= 10f) timerColor = Red;
        else if (live && timeLeft <= round.SealWindow) timerColor = Orange;
        timerLabel.style.color = timerColor;

        int score = round.LiveScore.total;
        if (score != lastScore)
        {
            if (score > lastScore) punch = 1f;
            lastScore = score;
        }
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 4f);
        float scale = 1f + 0.25f * punch;
        scoreLabel.text = score.ToString();
        scoreLabel.style.scale = new Scale(new Vector3(scale, scale, 1f));

        penLabel.text = $"{round.GlimmersInPen} / {round.GlimmersTotal} IN THE PEN";

        bool holding = round.Phase == RoundPhase.Seal && round.GateSealed;
        Show(sealTrack, holding);
        if (holding) sealFill.style.width = Length.Percent(round.SealedProgress * 100f);
    }

    private void UpdateBanner(RoundPhase phase)
    {
        countdownLabel.text = Mathf.CeilToInt(round.IntroTimeLeft).ToString();

        string text = string.Empty;
        Color color = Orange;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f);

        if (phase == RoundPhase.Intro)
        {
            text = "GET READY";
            color = Cyan;
            pulse = 1f;
        }
        else if (phase == RoundPhase.Herd && round.RoundDuration - round.TimeLeft < 2f)
        {
            text = "HERD THEM IN";
            color = Cyan;
            pulse = 1f;
        }
        else if (phase == RoundPhase.Seal)
        {
            bool sealedNow = round.GateSealed;
            text = sealedNow ? "HOLD THE GATE" : "SEAL THE GATE";
            color = sealedNow ? Green : Orange;
        }

        bannerLabel.text = text;
        color.a = pulse;
        bannerLabel.style.color = color;
        Show(bannerLabel, text.Length > 0);
        bannerLabel.style.marginTop = phase == RoundPhase.Intro ? 0 : 560;
    }

    private void UpdateResult()
    {
        RoundResult r = round.Result;
        ScoreBreakdown s = r.score;
        gradeLabel.text = s.grade.ToString();
        gradeLabel.style.color = GradeColor(s.grade);
        totalLabel.text = s.total.ToString();
        breakdownLabel.text = $"{s.basePoints} base   +{s.sealBonus} seal   +{s.sweepBonus} sweep   +{s.timeBonus} time";
        rankLabel.text = r.rank > 0 ? $"RANK #{r.rank}" : "OUTSIDE THE TOP 10";
    }

    private void OnBanked(GlimmerAgent agent)
    {
        if (agent == null || agent.Archetype == null) return;
        SpawnPopup(agent.transform.position, $"+{agent.Archetype.Multiplier}", Green);
    }

    private void OnLeaked(GlimmerAgent agent)
    {
        if (agent == null || agent.Archetype == null) return;
        SpawnPopup(agent.transform.position, $"-{agent.Archetype.Multiplier}", Red);
    }

    private void SpawnPopup(Vector3 world, string text, Color color)
    {
        if (!ready) return;

        Label label = MakeLabel(root, 52, color);
        label.text = text;
        label.style.position = Position.Absolute;
        label.style.width = 120;
        popups.Add(new Popup { label = label, world = world });
    }

    private void UpdatePopups()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            Popup popup = popups[i];
            popup.age += Time.unscaledDeltaTime;
            if (popup.age >= popupLife || worldCamera == null)
            {
                popup.label.RemoveFromHierarchy();
                popups.RemoveAt(i);
                continue;
            }

            Vector3 screen = worldCamera.WorldToScreenPoint(popup.world);
            Vector2 local = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            float t = popup.age / popupLife;
            popup.label.style.left = local.x - 60f;
            popup.label.style.top = local.y - 30f - popupRise * t;
            popup.label.style.opacity = 1f - t * t;
        }
    }

    private static Color GradeColor(char grade)
    {
        switch (grade)
        {
            case 'S': return Gold;
            case 'A': return Green;
            case 'B': return Cyan;
            case 'C': return Orange;
            default: return Red;
        }
    }

    private static void Show(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void Stretch(VisualElement element)
    {
        element.style.position = Position.Absolute;
        element.style.left = 0;
        element.style.top = 0;
        element.style.right = 0;
        element.style.bottom = 0;
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

    private static Label MakeLabel(VisualElement parent, int size, Color color, bool bold = true)
    {
        Label label = new Label();
        label.pickingMode = PickingMode.Ignore;
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        parent.Add(label);
        return label;
    }
}
