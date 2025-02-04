using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public static class Noise
{
    public static float[,] GenerateNoiseMap(int width, int height, int seed, float scale = 20, int octaves = 8, float persistence = 0.5f, float lacunarity = 2.0f){
        float[,] noiseMap = new float[width, height];

        if (scale <= 0){
            scale = 0.0001f;
        }

        System.Random random = new System.Random(seed);
        
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++){
            float offsetX = random.Next(-10000,10000);
            float offsetY = random.Next(-10000,10000);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        float maxNoiseValue = float.MinValue;
        float minNoiseValue = float.MaxValue;
        

        for (int y = 0; y < height; y++){
            for (int x = 0; x < width; x++){

                float amplitude = 1;
                float frequency = 1;
                float noiseValue = 0;

                for (int i = 0; i < octaves; i++){
                    float sampleX = x / scale * frequency + octaveOffsets[i].x;
                    float sampleY = y / scale * frequency + octaveOffsets[i].y;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                    
                    noiseValue += perlinValue * amplitude;
                    

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                if (noiseValue > maxNoiseValue) {
                    maxNoiseValue = noiseValue;
                }
                if (noiseValue < minNoiseValue) {
                    minNoiseValue = noiseValue;
                }
                noiseMap[x,y] = noiseValue;
            }
        }

        for (int y = 0; y < height; y++){
            for (int x = 0; x < width; x++){
                noiseMap[x,y] = Mathf.InverseLerp(minNoiseValue, maxNoiseValue, noiseMap[x,y]);
            }
        }
            
        return noiseMap;
    }
    
    public static float[,] GenerateFalloffMap(int width, int height, float b = 7.2f, bool includeX = true){
        float[,] map = new float[width,height];

        for (int i = 0; i < width; i++){
            for (int j = 0; j < height; j++){
                float x = i / (float)width * 2 - 1;
                float y = j / (float)height * 2 - 1;
                float val = Mathf.Abs(y);
                if (includeX){
                    val = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                }
                
                map[i,j] = Evaluate(val, b);
            }
        }

        return map;
    }

    public static float Evaluate(float value, float b = 7.2f, float a = 3f){
        return Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
    }
}
