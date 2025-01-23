using UnityEngine;
using UnityEngine.Rendering;

public enum BiomeTerrainType {
    LAND,
    WATER,
    ICE
}

[CreateAssetMenu(fileName = "Biome", menuName = "ScriptableObjects/Biome", order = 1)]
public class Biome : ScriptableObject
{    
    [Header("Biome Requirements")]
    [Range(0f,1f)] public float minTemperature;
    [Range(0f,1f)] public float maxTemperature;
    [Range(0f,1f)] public float minMoisture;
    [Range(0f,1f)] public float maxMoisture;
    [Range(0f,1f)] public float minAltitude;
    [Range(0f,1f)] public float maxAltitude;

    [Header("Biome Stats")]
    [Range(0f,1f)] public float fertility;
    public Color color;
    public BiomeTerrainType terrainType;
}
