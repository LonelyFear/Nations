using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public enum BiomeTerrainType {
    LAND,
    WATER,
    ICE
}

[CreateAssetMenu(fileName = "Biome", menuName = "ScriptableObjects/Biome", order = 1)]
[Serializable]
public class Biome : ScriptableObject
{    
    [Header("Biome Stats")]
    public string id;
    public string[] mergedIds;
    [Range(0f,1f)] public float fertility;
    public string color;
    public BiomeTerrainType terrainType;
}

[Serializable]
public class BiomeImport : ScriptableObject 
{
    public string modId = "base";
    public Biome[] biomes;
}
