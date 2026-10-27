using UnityEngine;

[CreateAssetMenu(fileName = "Archetype", menuName = "Herd the Lights/Archetype")]
public class ArchetypeDefinition : ScriptableObject
{
    [field: SerializeField] public string DisplayName { get; private set; } = "Obedient";
    [field: SerializeField] public int Multiplier { get; private set; } = 1;
    [field: SerializeField] public Color Color { get; private set; } = Color.cyan;
    [field: SerializeField] public float EmissionIntensity { get; private set; } = 2f;
    [field: SerializeField] public float Scale { get; private set; } = 0.5f;
    [field: SerializeField] public Mesh Mesh { get; private set; }

    [field: SerializeField] public float FleeRadius { get; private set; } = 3f;
    [field: SerializeField] public float FleeStrength { get; private set; } = 1f;
    [field: SerializeField] public float FleeSpeed { get; private set; } = 3f;
    [field: SerializeField] public float MaxSpeed { get; private set; } = 3.5f;
    [field: SerializeField] public float Acceleration { get; private set; } = 8f;
    [field: SerializeField] public float CruiseSpeed { get; private set; } = 1f;

    [field: SerializeField] public float NeighborRadius { get; private set; } = 4f;
    [field: SerializeField] public float SeparationRadius { get; private set; } = 0.8f;
    [field: SerializeField] public float CohesionWeight { get; private set; } = 0.8f;
    [field: SerializeField] public float SeparationWeight { get; private set; } = 1f;
    [field: SerializeField] public float WanderWeight { get; private set; } = 0.3f;
    [field: SerializeField] public float WanderJitter { get; private set; } = 4f;
    [field: SerializeField] public float EdgeDriftWeight { get; private set; }
    [field: SerializeField] public float WallMargin { get; private set; } = 1.5f;

    [field: SerializeField] public float PanicDuration { get; private set; }
    [field: SerializeField] public float PanicSpreadRadius { get; private set; }
    [field: SerializeField] public float PanicSpreadDecay { get; private set; } = 0.7f;
    [field: SerializeField] public float PanicSpeedBoost { get; private set; } = 1f;
}
