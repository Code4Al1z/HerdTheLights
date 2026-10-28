using UnityEngine;
using UnityEngine.InputSystem;

public class MousePlayerSource : MonoBehaviour, IPlayerSource
{
    [SerializeField] private PlayfieldMapper mapper;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private BotTeam bots;
    [SerializeField] private bool includeHuman = true;

    private Vector2 normalized = new Vector2(0.5f, 0.5f);

    public int Count => (includeHuman ? 1 : 0) + (bots != null ? bots.Count : 0);

    public Vector2 GetNormalizedPosition(int index)
    {
        if (includeHuman)
        {
            if (index == 0) return normalized;
            index--;
        }
        return bots != null ? bots.GetNormalizedPosition(index) : normalized;
    }

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (mapper == null) Debug.LogError("MousePlayerSource needs a playfield mapper.", this);
        if (bots != null) bots.SetHuman(includeHuman, Vector3.zero);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mapper != null && mapper.TryScreenToNormalized(viewCamera, mouse.position.ReadValue(), out Vector2 n))
            normalized = n;

        if (bots != null && mapper != null) bots.SetHuman(includeHuman, mapper.NormalizedToWorld(normalized));
    }
}