using UnityEngine;
using UnityEngine.InputSystem;

public class LureController : MonoBehaviour
{
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private bool useMouseInput = true;
    [SerializeField] private float duration = 3f;
    [SerializeField] private float cooldown = 6f;
    [SerializeField] private float radius = 6f;
    [SerializeField] private GameObject markerPrefab;

    private float activeTimer;
    private float cooldownTimer;
    private Vector3 position;
    private GameObject marker;

    public bool Active => activeTimer > 0f;
    public bool Ready => cooldownTimer <= 0f;
    public Vector3 Position => position;
    public float Radius => radius;
    public float CooldownRemaining => cooldownTimer;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (mapper == null) Debug.LogError("LureController needs a playfield mapper.", this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        activeTimer = Mathf.Max(0f, activeTimer - dt);
        cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);

        if (marker != null && activeTimer <= 0f)
        {
            Destroy(marker);
            marker = null;
        }

        if (useMouseInput) PollMouse();
    }

    public bool TryDrop(Vector2 normalized)
    {
        if (cooldownTimer > 0f || mapper == null) return false;

        position = mapper.NormalizedToWorld(normalized);
        activeTimer = duration;
        cooldownTimer = cooldown;

        if (marker != null) Destroy(marker);
        if (markerPrefab != null) marker = Instantiate(markerPrefab, position, Quaternion.identity, transform);
        return true;
    }

    private void PollMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || mapper == null || !mouse.leftButton.wasPressedThisFrame) return;

        if (mapper.TryScreenToNormalized(viewCamera, mouse.position.ReadValue(), out Vector2 n))
            TryDrop(n);
    }
}