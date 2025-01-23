using Unity.VisualScripting;
using UnityEngine;

//[CreateAssetMenu(fileName = "TileTerrain", menuName = "ScriptableObjects/Tile/TileTerrain", order = 1)]
public struct Terrain
{   
    public Biome biome; 
    public float height;
    public float temperature;
    public float moisture;
    public Color color;
    public float fertility;
    public bool water;
    public bool claimable;

}
