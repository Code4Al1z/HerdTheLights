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
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || viewCamera == null || mapper == null) return;

        Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
        Plane plane = new Plane(Vector3.up, mapper.Center);
        if (!plane.Raycast(ray, out float distance)) return;

        Vector2 n = mapper.WorldToNormalized(ray.GetPoint(distance));
        normalized = new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y));
    }
}
