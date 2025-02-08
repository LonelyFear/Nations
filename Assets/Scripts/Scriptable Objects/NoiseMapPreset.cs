using UnityEngine;

[CreateAssetMenu(fileName = "NoiseMapPreset", menuName = "ScriptableObjects/Worldgen/NoiseMapPreset", order = 1)]
public class NoiseMapPreset : ScriptableObject
{
    public Texture2D noiseTexture;
    [Range(0f, 1f)]
    public float oceanThreshold = 0.4f;
}
