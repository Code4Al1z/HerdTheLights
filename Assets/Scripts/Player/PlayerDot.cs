using UnityEngine;

public class PlayerDot : MonoBehaviour
{
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private MonoBehaviour playerSourceBehaviour;
    [SerializeField] private int playerIndex;
    [SerializeField] private float smoothing = 25f;
    [SerializeField] private Renderer dotRenderer;

    private IPlayerSource source;

    private void Awake()
    {
        source = playerSourceBehaviour as IPlayerSource;
        if (dotRenderer == null) dotRenderer = GetComponentInChildren<Renderer>();
        if (source == null || mapper == null)
            Debug.LogError("PlayerDot needs a playfield mapper and a player source.", this);
    }

    private void Update()
    {
        bool active = source != null && mapper != null && playerIndex < source.Count;
        if (dotRenderer != null) dotRenderer.enabled = active;
        if (!active) return;

        Vector3 target = mapper.NormalizedToWorld(source.GetNormalizedPosition(playerIndex));
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
    }
}