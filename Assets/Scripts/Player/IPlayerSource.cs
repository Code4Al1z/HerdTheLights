using UnityEngine;

public interface IPlayerSource
{
    int Count { get; }
    Vector2 GetNormalizedPosition(int index);
}
