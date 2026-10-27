using System.Collections.Generic;
using UnityEngine;

public enum GateSide { Left, Right, Bottom, Top }

public interface IGateState
{
    bool IsOpenAt(Vector3 point);
}

public class PenZone : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(4f, 6f);
    [SerializeField] private GateSide gateSide = GateSide.Right;
    [SerializeField] private float gateWidth = 3f;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Color wallColor = new Color(0.4f, 0.9f, 1f);
    [SerializeField] private float wallThickness = 0.15f;
    [SerializeField] private float wallEmission = 2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private IGateState gate;
    private bool wallsBuilt;

    public Vector3 Center => transform.position;
    public Vector2 Size => size;
    public GateSide Side => gateSide;
    public Vector3 GateOutward => Outward(gateSide);
    public Vector3 GateAxis => IsVertical(gateSide) ? Vector3.forward : Vector3.right;
    public float GateLength => Mathf.Min(gateWidth, SideLength(gateSide));
    public Vector3 GateCenter => Center + Outward(gateSide) * HalfExtentTowards(gateSide);

    private void Start()
    {
        wallsBuilt = true;
        BuildWalls();
    }

    public void SetGateWidth(float width)
    {
        gateWidth = Mathf.Max(0.5f, width);
        if (wallsBuilt) RebuildWalls();
    }

    public void AttachGate(IGateState state)
    {
        gate = state;
    }

    public bool Contains(Vector3 point)
    {
        return Contains(point, 0f);
    }

    public bool Contains(Vector3 point, float padding)
    {
        Vector3 local = point - Center;
        return Mathf.Abs(local.x) <= size.x * 0.5f + padding && Mathf.Abs(local.z) <= size.y * 0.5f + padding;
    }

    public int CountInside(IReadOnlyList<GlimmerAgent> agents)
    {
        int count = 0;
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && Contains(agents[i].transform.position)) count++;
        }
        return count;
    }

    public Vector3 Constrain(Vector3 from, Vector3 to, float bodyRadius, out Vector3 blockedAxes)
    {
        blockedAxes = Vector3.zero;
        float clearance = bodyRadius + wallThickness * 0.5f;
        bool inside = Contains(from);

        if (!Allowed(from, inside, clearance)) from = Eject(from, inside, clearance);

        Vector3 result = from;
        Vector3 stepX = new Vector3(to.x, to.y, result.z);
        if (Allowed(stepX, inside, clearance)) result = stepX;
        else if (Mathf.Abs(to.x - from.x) > 0f) blockedAxes.x = 1f;

        Vector3 stepZ = new Vector3(result.x, to.y, to.z);
        if (Allowed(stepZ, inside, clearance)) result = stepZ;
        else if (Mathf.Abs(to.z - from.z) > 0f) blockedAxes.z = 1f;

        return result;
    }

    private bool Allowed(Vector3 point, bool inside, float clearance)
    {
        if (InCorridor(point, clearance)) return true;
        return inside ? Contains(point, -clearance) : !Contains(point, clearance);
    }

    private bool InCorridor(Vector3 point, float clearance)
    {
        Vector3 relative = point - GateCenter;
        float along = Vector3.Dot(relative, GateAxis);
        float depth = Vector3.Dot(relative, GateOutward);
        if (Mathf.Abs(along) > GateLength * 0.5f - clearance || Mathf.Abs(depth) > clearance) return false;
        return gate == null || gate.IsOpenAt(point);
    }

    private Vector3 Eject(Vector3 point, bool inside, float clearance)
    {
        Vector2 half = size * 0.5f;
        Vector3 relative = point - Center;

        if (inside)
        {
            Vector2 limit = half - new Vector2(clearance, clearance);
            return new Vector3(
                Center.x + Mathf.Clamp(relative.x, -limit.x, limit.x),
                point.y,
                Center.z + Mathf.Clamp(relative.z, -limit.y, limit.y));
        }

        Vector2 outer = half + new Vector2(clearance, clearance);
        float gapX = outer.x - Mathf.Abs(relative.x);
        float gapZ = outer.y - Mathf.Abs(relative.z);
        Vector3 result = point;
        if (gapX < gapZ) result.x = Center.x + (relative.x < 0f ? -outer.x : outer.x);
        else result.z = Center.z + (relative.z < 0f ? -outer.y : outer.y);
        return result;
    }

    private void RebuildWalls()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        BuildWalls();
    }

    private void BuildWalls()
    {
        if (wallMaterial == null) return;

        Vector2 half = size * 0.5f;
        Vector3 c = Center;
        Vector3 bottomLeft = c + new Vector3(-half.x, 0f, -half.y);
        Vector3 bottomRight = c + new Vector3(half.x, 0f, -half.y);
        Vector3 topRight = c + new Vector3(half.x, 0f, half.y);
        Vector3 topLeft = c + new Vector3(-half.x, 0f, half.y);
        BuildSide(bottomLeft, bottomRight, GateSide.Bottom);
        BuildSide(bottomRight, topRight, GateSide.Right);
        BuildSide(topLeft, topRight, GateSide.Top);
        BuildSide(bottomLeft, topLeft, GateSide.Left);
    }

    private void BuildSide(Vector3 a, Vector3 b, GateSide side)
    {
        if (side != gateSide)
        {
            BuildSegment(a, b);
            return;
        }

        Vector3 direction = (b - a).normalized;
        Vector3 middle = (a + b) * 0.5f;
        float reach = GateLength * 0.5f;
        BuildSegment(a, middle - direction * reach);
        BuildSegment(middle + direction * reach, b);
    }

    private void BuildSegment(Vector3 a, Vector3 b)
    {
        float length = Vector3.Distance(a, b);
        if (length < 0.01f) return;

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(wall.GetComponent<Collider>());
        wall.name = "Wall";
        wall.transform.SetParent(transform, true);
        wall.transform.position = (a + b) * 0.5f;
        wall.transform.rotation = Quaternion.LookRotation(b - a);
        wall.transform.localScale = new Vector3(wallThickness, wallThickness, length);

        Renderer wallRenderer = wall.GetComponent<Renderer>();
        wallRenderer.sharedMaterial = wallMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, wallColor);
        block.SetColor(EmissionColorId, wallColor * wallEmission);
        wallRenderer.SetPropertyBlock(block);
    }

    private static bool IsVertical(GateSide side)
    {
        return side == GateSide.Left || side == GateSide.Right;
    }

    private static Vector3 Outward(GateSide side)
    {
        switch (side)
        {
            case GateSide.Left: return Vector3.left;
            case GateSide.Right: return Vector3.right;
            case GateSide.Top: return Vector3.forward;
            default: return Vector3.back;
        }
    }

    private float SideLength(GateSide side)
    {
        return IsVertical(side) ? size.y : size.x;
    }

    private float HalfExtentTowards(GateSide side)
    {
        return IsVertical(side) ? size.x * 0.5f : size.y * 0.5f;
    }

    private void DrawSide(Vector3 a, Vector3 b, GateSide side)
    {
        Color wall = new Color(0.4f, 0.9f, 1f);
        Color opening = new Color(1f, 0.8f, 0.2f);
        if (side != gateSide)
        {
            Gizmos.color = wall;
            Gizmos.DrawLine(a, b);
            return;
        }

        Vector3 direction = (b - a).normalized;
        Vector3 middle = (a + b) * 0.5f;
        float reach = GateLength * 0.5f;
        Gizmos.color = wall;
        Gizmos.DrawLine(a, middle - direction * reach);
        Gizmos.DrawLine(middle + direction * reach, b);
        Gizmos.color = opening;
        Gizmos.DrawLine(middle - direction * reach, middle + direction * reach);
    }

    private void OnDrawGizmos()
    {
        Vector2 half = size * 0.5f;
        Vector3 c = Center;
        Vector3 bottomLeft = c + new Vector3(-half.x, 0f, -half.y);
        Vector3 bottomRight = c + new Vector3(half.x, 0f, -half.y);
        Vector3 topRight = c + new Vector3(half.x, 0f, half.y);
        Vector3 topLeft = c + new Vector3(-half.x, 0f, half.y);
        DrawSide(bottomLeft, bottomRight, GateSide.Bottom);
        DrawSide(bottomRight, topRight, GateSide.Right);
        DrawSide(topLeft, topRight, GateSide.Top);
        DrawSide(bottomLeft, topLeft, GateSide.Left);
    }
}