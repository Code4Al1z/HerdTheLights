using UnityEngine;
using UnityEngine.InputSystem;

public class MousePlayerSource : MonoBehaviour, IPlayerSource
{
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private Camera viewCamera;

    private Vector2 normalized = new Vector2(0.5f, 0.5f);

    public int Count => 1;

    public Vector2 GetNormalizedPosition(int index)
    {
        return normalized;
    }

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (mapper == null) Debug.LogError("MousePlayerSource needs a playfield mapper.", this);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || mapper == null) return;

        if (mapper.TryScreenToNormalized(viewCamera, mouse.position.ReadValue(), out Vector2 n))
            normalized = n;
    }
}