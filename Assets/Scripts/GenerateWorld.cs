using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using Unity.VisualScripting;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GenerateWorld : MonoBehaviour
{
    [Header("Variables And Lists")]
    public Tilemap tilemap;
    public TileBase tileBase;
    
    [Header("World Generation Settings")]
    public Vector2Int worldSize = new Vector2Int(100, 100);

    [Header("Noise Texture Settings")]
    [Tooltip("Overrides all other noise settings")]
    public NoiseMapPreset preset;
    [Tooltip("If true, makes the world size y scale with the proportion to texture y")]
    public bool fixToTexture = true;
    [Header("Random Noise Settings")]
    public float noiseSeed = 0;
    public bool randomizeSeed = false;
    [Tooltip("The noise scale for RANDOM NOISE | Higher scale = Smoother")]
    public int totalNoiseScale;
    [Tooltip("Higher scale = Smoother")]
    public float[] scales = new float[4];
    [Tooltip("Weights the different noise maps")]
    public float[] weights = new float[4];

    [Header("Terrain Settings")]
    /*
    [SerializeField] Color oceanColor;
    [SerializeField] Color hotColor;
    [SerializeField] Color temperateColor;
    [SerializeField] Color coolColor;
    [SerializeField] Color moistColor;
    [SerializeField] Color dryColor;
    */
    [SerializeField]
    Biome[] landBiomes;
    [SerializeField] Biome oceanBiome;
    [SerializeField] Biome seaIceBiome;
    [SerializeField] Biome defaultBiome;
    [Range(-0.5f, 0.5f)]
    [SerializeField]  float moistureOffset;
    [Range(-0.5f, 0.5f)]
    [SerializeField]  float tempOffset;
    [Range(0f, 1f)]

    float landTiles = 0;
    public Dictionary<Vector3Int, Tile> tiles = new Dictionary<Vector3Int, Tile>();

    void Start()
    {
        if (randomizeSeed){
            noiseSeed = Random.Range(0, 99999);
        }
        if (fixToTexture && preset.noiseTexture){
            fitYToTexture();
        }
        generateWorld();

        // Connects relevant scripts to worldgen finished
        WorldgenEvents.onWorldgenFinished += FindAnyObjectByType<TimeManager>().startTimers;
        Events.tick += GetComponent<TileManager>().Tick;
        GetComponent<TileManager>().worldSize = worldSize;
        GetComponent<TileManager>().Init();

        // Sends worldgen finished signal
        GetComponent<WorldgenEvents>().worldgenFinish();
    }
    void fitYToTexture(){
        float texScale = preset.noiseTexture.Size().x / worldSize.x;
        worldSize.y = Mathf.RoundToInt(preset.noiseTexture.Size().y / texScale);
    }

    float getNoise(int x, int y, float scale, float noiseSeed){
        // Higher scale means less smooth
        var totalScale = scale * totalNoiseScale;
        var val = Mathf.PerlinNoise((x + 0.1f + noiseSeed)/totalScale,(y + 0.1f + noiseSeed)/totalScale);
        return val;
    }

    float getHeight(int x, int y){
        float totalNoise;
        // If there isnt a predifined noise texture
        if (!preset.noiseTexture){
            
            float grains = getNoise(x,y, scales[3], noiseSeed + 7852);
            float detail = getNoise(x,y,scales[2], noiseSeed);
            float definition = getNoise(x,y,scales[1], noiseSeed + 9830);
            float shape = getNoise(x, y,scales[0], noiseSeed - 3573);

            // Merges the different noise maps and weights them to get more interesting terrain
            totalNoise = (shape * weights[0]) + (definition * weights[1]) + (detail * weights[2]) + (grains * weights[3]);
        } else {
            // Stretches or squashes the values to represent the whole noise texture
            Vector2 texScale = preset.noiseTexture.Size() / worldSize;
            // Gets the pixel pos (Rounds tex scale multiplied by the current x and y)
            Vector2Int pixel = Vector2Int.RoundToInt(new Vector2(x * texScale.x, y * texScale.y));
            // Gets the value of the red channel at the pixel position
            totalNoise = preset.noiseTexture.GetPixel(pixel.x, pixel.y).r;
        }
        return totalNoise;
    }

    float getMoisture(int x, int y){
        float clouds = getNoise(x,y, 0.01f, noiseSeed + 642);
        float systems = getNoise(x,y, 0.2f, noiseSeed + 753);
        float seas = getNoise(x,y, 0.5f, noiseSeed + 257);
        float shape = getNoise(x, y, 0.75f, noiseSeed);

        float totalNoise = (shape * 0.6f) + (seas * 0.2f) + (systems * 0.15f) + (clouds * 0.05f) + moistureOffset;
        return totalNoise;
    }

    float getTempRandomNoise(int x, int y){
        float steam = getNoise(x, y, 0.1f, noiseSeed - 332);
        float drifts = getNoise(x, y, 0.5f, noiseSeed - 986);
        float winds = getNoise(x, y, 1f, noiseSeed - 9862);

        return (winds * 0.6f) + (drifts * 0.3f) + (steam * 0.1f);
    }
    float getTemp(int x, int y){
        float equatorPos = worldSize.y / 2;
        float tempValue = 1f - Mathf.Abs(equatorPos - y) / equatorPos;
        tempValue = Mathf.Clamp(tempValue + tempOffset, 0f, 1f);
        return (tempValue * 0.85f) + (getTempRandomNoise(x,y) * 0.15f);
    }

    Tile getTile(Vector3Int pos){
        if (tiles.ContainsKey(pos)){
            return tiles[pos];
        }
        return null;
    }
    void generateWorld(){
        // Worldsize works like lists, so 0 is the first index and the last index is worldsize - 1
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                Vector3Int cellPos = new Vector3Int(x,y);

                tilemap.SetTile(cellPos, tileBase);

                // Sets terrain to default
                
                // Instantiates a tile
                Tile newTile = new Tile();


                // Adds the tile to the tile manager
                

                SetBiome(x, y, newTile);
                
                tiles.Add(cellPos, newTile);
            }
        }
        FinalChecks();

        print("Land Tiles: " + landTiles);
        print("Total Tiles: " + (worldSize.x * worldSize.y));
        print("Land %: " + Mathf.RoundToInt(landTiles / (worldSize.x * worldSize.y) * 100) + "%");

        GetComponent<TileManager>().tiles = tiles;
        
    }

    void FinalChecks(){
        foreach (var entry in tiles){
            Tile tile = entry.Value;
            Vector3Int pos = entry.Key;
            for (int ox = -1; ox <= 1; ox++){
                for (int oy = -1; oy <= 1; oy++){
                    if (getTile(pos + new Vector3Int(ox, oy)) != null && getTile(pos + new Vector3Int(ox, oy)).biome.terrainType == BiomeTerrainType.WATER){
                        tile.coastal = true;
                        break;
                    }
                }
            }
        }        
    }
    void SetBiome(int x, int y, Tile tile){

        float altitude = getHeight(x,y);
        float temp = getTemp(x,y);
        float moisture = getMoisture(x,y);
        float seaLevel = preset.oceanThreshold;

        Color color;
        Biome biome;

        // If we are below the ocean threshold
        if (altitude <= seaLevel){
            biome = oceanBiome;
            color = Color.Lerp(Color.black, biome.color, altitude/seaLevel);
            if (temp <= seaIceBiome.maxTemperature){
                biome = seaIceBiome;
                color = biome.color;
            }
        } else {
            landTiles++;
            biome = defaultBiome;
            foreach (Biome potentialBiome in landBiomes){
                bool withinTemp =  temp >= potentialBiome.minTemperature && temp < potentialBiome.maxTemperature;
                bool withinMoist = moisture >= potentialBiome.minMoisture && moisture < potentialBiome.maxMoisture;
                bool withinAlt = true; // Mathf.Clamp01((altitude - seaLevel) / (1f - seaLevel)) >= potentialBiome.minAltitude && Mathf.Clamp01((altitude - seaLevel) / (1f - seaLevel)) < potentialBiome.maxAltitude
                if (withinAlt && withinMoist && withinTemp){
                    biome = potentialBiome;
                    break;
                }
            }
            color = biome.color;
        }

        switch (biome.terrainType){
            case BiomeTerrainType.LAND:
                tile.claimable = true;
                break;
            case BiomeTerrainType.WATER:
                tile.claimable = false;
                break;
            case BiomeTerrainType.ICE:
                tile.claimable = true;
                break;
        }
        //float fertility = CalcFertility(height, moisture, temp);
        
        //if (water){
            //fertility = 0;
        //}
    
        tile.terrainColor = color;
        tile.biome = biome;
        tile.fertility = biome.fertility;
        tile.altitude = altitude;
        tile.temperature = temp;
        tile.moisture = moisture;
    }
    /*
    float CalcFertility(float height, float moisture, float temperature){
        float seaLevel = preset.oceanThreshold;

        float fertility;
        // Bell curves
        float moistureScore = Mathf.Exp(-Mathf.Pow((moisture - 0.5f) / 0.15f, 2f));
        
        // Temp Score
        float adjustedTemp = Mathf.Clamp01((temperature - freezingTemp)/(1 - freezingTemp));
        float tempScore = Mathf.Exp(-Mathf.Pow((adjustedTemp - 0.5f) / 0.2f, 2f));

        
        // Altitude score
        float adjustedHeight = Mathf.Clamp01((height - seaLevel) / (1f - seaLevel));
        float optimalHeight = 0.3f;
        float altitudeScore = Mathf.Exp(-Mathf.Pow((adjustedHeight - optimalHeight) / 0.15f, 2f));
        // If we are above the optimal height uses a different curve
        if (adjustedHeight > optimalHeight){
            altitudeScore = Mathf.Exp(-Mathf.Pow((adjustedHeight - optimalHeight) / 0.3f, 2f));
        }

        if (temperature > freezingTemp){
            fertility = Mathf.Clamp((moistureScore * tempScore * 0.6f) + (moistureScore * altitudeScore * 0.4f), 0.01f, 1f); 
        } else {
            fertility = moistureScore * 0.01f;
        }

        return fertility;
    }
    */
}
