using UnityEngine;

public class GateSlots : MonoBehaviour, IGateState
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private PenZone pen;
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private MonoBehaviour playerSourceBehaviour;
    [SerializeField] private int slotCount = 1;
    [SerializeField] private float standDepth = 1.2f;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private float markerScale = 1.5f;
    [SerializeField] private Color idleColor = new Color(0.5f, 0.5f, 0.6f);
    [SerializeField] private Color emptyColor = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private Color filledColor = new Color(0.2f, 1f, 0.5f);
    [SerializeField] private float emissionIntensity = 2f;

    private IPlayerSource source;
    private bool[] filled = new bool[0];
    private Renderer[] markerRenderers = new Renderer[0];
    private MaterialPropertyBlock block;
    private bool armed;

    public int SlotCount => slotCount;
    public bool Armed => armed;

    public int FilledCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < filled.Length; i++)
            {
                if (filled[i]) count++;
            }
            return count;
        }
    }

    public bool AllFilled => filled.Length > 0 && FilledCount == filled.Length;

    private float SlotWidth => pen.GateLength / slotCount;

    private void Awake()
    {
        source = playerSourceBehaviour as IPlayerSource;
        block = new MaterialPropertyBlock();
        if (pen != null) pen.AttachGate(this);
        if (pen == null || mapper == null || source == null)
            Debug.LogError("GateSlots needs a pen, a playfield mapper and a player source.", this);
    }

    private void Start()
    {
        Rebuild();
    }

    public void SetArmed(bool value)
    {
        armed = value;
    }

    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        Rebuild();
    }

    public bool IsOpenAt(Vector3 point)
    {
        if (!armed || filled.Length == 0 || pen == null) return true;
        return !filled[SlotIndexAt(point)];
    }

    private int SlotIndexAt(Vector3 point)
    {
        float along = Vector3.Dot(point - pen.GateCenter, pen.GateAxis);
        int index = Mathf.FloorToInt((along + pen.GateLength * 0.5f) / SlotWidth);
        return Mathf.Clamp(index, 0, filled.Length - 1);
    }

    private Vector3 SlotCenter(int index)
    {
        float offset = (index + 0.5f) * SlotWidth - pen.GateLength * 0.5f;
        return pen.GateCenter + pen.GateAxis * offset;
    }

    private void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        slotCount = Mathf.Max(1, slotCount);
        filled = new bool[slotCount];
        markerRenderers = new Renderer[slotCount];
        if (pen == null || markerPrefab == null) return;

        for (int i = 0; i < slotCount; i++)
        {
            GameObject marker = Instantiate(markerPrefab, SlotCenter(i), Quaternion.identity, transform);
            marker.transform.localScale = Vector3.one * markerScale;
            markerRenderers[i] = marker.GetComponentInChildren<Renderer>();
        }
    }

    private void Update()
    {
        if (pen == null || mapper == null || source == null || filled.Length == 0) return;

        for (int i = 0; i < filled.Length; i++)
        {
            filled[i] = false;
        }

        float halfLength = pen.GateLength * 0.5f;
        for (int p = 0; p < source.Count; p++)
        {
            Vector3 world = mapper.NormalizedToWorld(source.GetNormalizedPosition(p));
            Vector3 relative = world - pen.GateCenter;
            float along = Vector3.Dot(relative, pen.GateAxis);
            float depth = Mathf.Abs(Vector3.Dot(relative, pen.GateOutward));
            if (Mathf.Abs(along) > halfLength || depth > standDepth) continue;
            filled[SlotIndexAt(world)] = true;
        }

        for (int i = 0; i < markerRenderers.Length; i++)
        {
            if (markerRenderers[i] == null) continue;
            Color color = !armed ? idleColor : (filled[i] ? filledColor : emptyColor);
            markerRenderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionColorId, color * emissionIntensity);
            markerRenderers[i].SetPropertyBlock(block);
        }
    }

    private void OnDrawGizmos()
    {
        if (pen == null) return;
        int count = Mathf.Max(1, slotCount);
        float width = pen.GateLength / count;
        for (int i = 0; i < count; i++)
        {
            float offset = (i + 0.5f) * width - pen.GateLength * 0.5f;
            Vector3 center = pen.GateCenter + pen.GateAxis * offset;
            bool isFilled = Application.isPlaying && i < filled.Length && filled[i];
            Gizmos.color = isFilled ? filledColor : emptyColor;
            Vector3 box = pen.GateAxis * width * 0.9f + pen.GateOutward * standDepth * 2f;
            Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(box.x), 0.05f, Mathf.Abs(box.z)));
        }
    }
}