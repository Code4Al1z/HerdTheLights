using UnityEngine;

public class PlayfieldMapper : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(16f, 9f);
    [SerializeField] private Camera fitCamera;
    [SerializeField] private float cameraHeight = 20f;

    public Vector2 Size => size;
    public Vector3 Center => transform.position;

    private void Awake()
    {
        FitCamera();
    }

    public void FitCamera()
    {
        if (fitCamera == null) return;
        Transform cameraTransform = fitCamera.transform;
        cameraTransform.position = Center + Vector3.up * cameraHeight;
        cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
        fitCamera.orthographic = true;
        fitCamera.orthographicSize = size.y * 0.5f;
    }

    public Vector3 NormalizedToWorld(Vector2 normalized)
    {
        return new Vector3(
            Center.x + (normalized.x - 0.5f) * size.x,
            Center.y,
            Center.z + (normalized.y - 0.5f) * size.y);
    }

    public Vector2 WorldToNormalized(Vector3 world)
    {
        return new Vector2(
            (world.x - Center.x) / size.x + 0.5f,
            (world.z - Center.z) / size.y + 0.5f);
    }

    public bool TryScreenToNormalized(Camera camera, Vector2 screenPoint, out Vector2 normalized)
    {
        normalized = Vector2.zero;
        if (camera == null) return false;

        Ray ray = camera.ScreenPointToRay(screenPoint);
        Plane plane = new Plane(Vector3.up, Center);
        if (!plane.Raycast(ray, out float distance)) return false;

        Vector2 n = WorldToNormalized(ray.GetPoint(distance));
        normalized = new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y));
        return true;
    }

    public Vector3 Clamp(Vector3 world, float margin)
    {
        Vector2 half = size * 0.5f - new Vector2(margin, margin);
        return new Vector3(
            Mathf.Clamp(world.x, Center.x - half.x, Center.x + half.x),
            Center.y,
            Mathf.Clamp(world.z, Center.z - half.y, Center.z + half.y));
    }

    public Vector3 InwardPush(Vector3 world, float margin)
    {
        if (margin <= 0f) return Vector3.zero;
        Vector3 local = world - Center;
        Vector2 half = size * 0.5f;
        float x = Mathf.Clamp(Mathf.Abs(local.x) - (half.x - margin), 0f, margin) / margin;
        float z = Mathf.Clamp(Mathf.Abs(local.z) - (half.y - margin), 0f, margin) / margin;
        return new Vector3(-Mathf.Sign(local.x) * x, 0f, -Mathf.Sign(local.z) * z);
    }

    public Vector3 NearestEdgeDirection(Vector3 world)
    {
        Vector2 n = WorldToNormalized(world);
        float left = n.x;
        float right = 1f - n.x;
        float bottom = n.y;
        float top = 1f - n.y;
        float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        if (nearest == left) return Vector3.left;
        if (nearest == right) return Vector3.right;
        if (nearest == bottom) return Vector3.back;
        return Vector3.forward;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(Center, new Vector3(size.x, 0.05f, size.y));
    }
}