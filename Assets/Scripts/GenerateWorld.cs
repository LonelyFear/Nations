using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum TempType {
    EARTH
}
public enum MoistType {
    EARTH
}
public enum TempTypes {
    POLAR,
    ALPINE,
    BOREAL,
    COOL,
    WARM,
    SUBTROPICAL,
    TROPICAL,
    INVALID
}
public enum HumidTypes {
    SUPER_ARID,
    PER_ARID,
    ARID,
    SEMI_ARID,
    SUB_HUMID,
    HUMID,
    PER_HUMID,
    SUPER_HUMID,
    INVALID
}
public class GenerateWorld : MonoBehaviour
{
    public MapTextureManager map;

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
    [SerializeField] String biomePath;
    [Range(-0.5f, 0.5f)]
    [SerializeField]  float moistureOffset;
    [Range(-0.5f, 0.5f)]
    [SerializeField]  float tempOffset;
    [Range(0f, 1f)]

    // Constrains & Local values
    Biome[] loadedBiomeAggregates;
    string[,] biomes;
    float[] temps = {0.874f, 0.765f, 0.594f, 0.439f, 0.366f, 0.124f};
    float[] humids = {0.941f, 0.778f, 0.507f, 0.236f, 0.073f, 0.014f, 0.002f};

    float[,] tempVals;
    float[,] humidVals;

    float landTiles = 0;
    public Tile[,] tiles;

    void Start()
    {
        //worldSize = Vector2Int.RoundToInt(map.GetComponent<RectTransform>().sizeDelta);
        /*
        biomes = new string[worldSize.x, worldSize.y];
        tempVals = new float[worldSize.x, worldSize.y];
        humidVals = new float[worldSize.x, worldSize.y];
        tiles = new Tile[worldSize.x, worldSize.y];
        // /loadedBiomeAggregates = ;

        loadedBiomeAggregates = JsonUtility.FromJson<BiomeWrapper>(File.ReadAllText(biomePath)).biomes.ToArray();
        print(loadedBiomeAggregates);
        if (randomizeSeed){
            noiseSeed = UnityEngine.Random.Range(0, 99999);
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
        print(JsonUtility.ToJson(new Biome()));
        */
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

    float getAltitude(int x, int y){
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

    float getHumid(int x, int y){
        float clouds = getNoise(x,y, 0.01f, noiseSeed + 642);
        float systems = getNoise(x,y, 0.2f, noiseSeed + 753);
        float seas = getNoise(x,y, 0.1f, noiseSeed + 257);
        float shape = getNoise(x, y, 0.3f, noiseSeed);

        float totalNoise = (shape * 0.9f) + (seas * 0.1f);
        totalNoise = (totalNoise * 0.3f) + (tempVals[x,y] * 0.7f);
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
        float temp = Mathf.Lerp(getTempRandomNoise(x, y), tempValue, 0.7f);
        temp = Mathf.Clamp01(temp - ((getAltitude(x,y) - preset.oceanThreshold)/(1.0f/preset.oceanThreshold)));
        return temp;
    }

    Tile getTile(int x, int y){
        if (x < worldSize.x && y < worldSize.y && x >= 0 && y >= 0){
            return tiles[x, y];
        }
        return null;
    }
    void generateWorld(){
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                tempVals[x,y] = getTemp(x,y);
                humidVals[x,y] = getHumid(x,y);
            }
        }
            
        // Worldsize works like lists, so 0 is the first index and the last index is worldsize - 1
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                Vector3Int cellPos = new Vector3Int(x,y);

                // Sets terrain to default
                
                // Instantiates a tile
                Tile newTile = new Tile();

                biomes[x,y] = SetBiome(x, y);
                tiles[x, y] = newTile;
            }
        }
        FinalChecks();

        print("Land Tiles: " + landTiles);
        print("Total Tiles: " + (worldSize.x * worldSize.y));
        print("Land %: " + Mathf.RoundToInt(landTiles / (worldSize.x * worldSize.y) * 100) + "%");

        GetComponent<TileManager>().tiles = tiles;
        
    }

    void FinalChecks(){
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                Tile tile = tiles[x, y];
                tile.terrainColor = Color.Lerp(Color.black, Color.blue, humidVals[x,y]);
                string biome = biomes[x,y].ToLower();
                
                foreach (Biome aggregate in loadedBiomeAggregates){
                    if (aggregate.mergedIds.Contains(biome)){
                        tile.biome = aggregate;
                        break;
                    }
                }
            }
        }

        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                Tile tile = tiles[x, y];
                for (int ox = -1; ox <= 1; ox++){
                    for (int oy = -1; oy <= 1; oy++){
                        if (getTile(ox + x, oy + y) != null && getTile(ox + x, oy + y).biome.terrainType == BiomeTerrainType.WATER){
                            tile.coastal = true;
                            break;
                        }
                    }
                }                
            }  
        }   
    }
    String SetBiome(int x, int y){

        float altitude = getAltitude(x,y);
        float seaLevel = preset.oceanThreshold;

        String biome;

        // If we are below the ocean threshold
        if (altitude <= seaLevel){
            switch (getTempType(x, y)){
                case TempTypes.POLAR:
                    biome = "polar ice";
                    break;
                default:
                    biome = "ocean";
                    break;
            }
        } else {
            landTiles++;
            switch (getTempType(x, y)){
                case TempTypes.POLAR:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "polar desert";
                            break;
                        default:
                            biome = "polar ice";
                            break;
                    }
                    break;
                case TempTypes.ALPINE:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "subpolar dry tundra";
                            break;
                        case HumidTypes.PER_ARID:
                            biome = "subpolar moist tundra";
                            break;
                        case HumidTypes.ARID:
                            biome = "subpolar wet tundra";
                            break;
                        default:
                            biome = "subpolar rain tundra";
                            break;
                    }
                    break;
                case TempTypes.BOREAL:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "boreal desert";
                            break;
                        case HumidTypes.PER_ARID:
                            biome = "boreal dry scrub";
                            break;
                        case HumidTypes.ARID:
                            biome = "boreal moist forest";
                            break;
                        case HumidTypes.SEMI_ARID:
                            biome = "boreal wet forest";
                            break;
                        default:
                            biome = "boreal rain forest";
                            break;
                    }
                    break;
                case TempTypes.COOL:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "cool temperate desert";
                            break;
                        case HumidTypes.PER_ARID:
                            biome = "cool temperate desert scrub";
                            break;
                        case HumidTypes.ARID:
                            biome = "cool temperate steppe";
                            break;
                        case HumidTypes.SEMI_ARID:
                            biome = "cool temperate moist forest";
                            break;
                        case HumidTypes.SUB_HUMID:
                            biome = "cool temperate wet forest";
                            break;
                        default:
                            biome = "cool temperate rain forest";
                            break;
                    }
                    break;
                case TempTypes.WARM:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "warm temperate desert";
                            break;
                        case HumidTypes.PER_ARID:
                            biome = "warm temperate desert scrub";
                            break;
                        case HumidTypes.ARID:
                            biome = "warm temperate thorn scrub";
                            break;
                        case HumidTypes.SEMI_ARID:
                            biome = "warm temperate dry forest";
                            break;
                        case HumidTypes.SUB_HUMID:
                            biome = "warm temperate moist forest";
                            break;
                        case HumidTypes.HUMID:
                            biome = "warm temperate wet forest";
                            break;
                        default:
                            biome = "warm temperate rain forest";
                            break;
                    }
                    break;
                case TempTypes.SUBTROPICAL:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "subtropical desert";
                            break;
                        case HumidTypes.PER_ARID:
                            biome = "subtropical desert scrub";
                            break;
                        case HumidTypes.ARID:
                            biome = "subtropical thorn woodland";
                            break;
                        case HumidTypes.SEMI_ARID:
                            biome = "subtropical dry forest";
                            break;
                        case HumidTypes.SUB_HUMID:
                            biome = "subtropical moist forest";
                            break;
                        case HumidTypes.HUMID:
                            biome = "subtropical wet forest";
                            break;
                        default:
                            biome = "subtropical rain forest";
                            break;
                    }
                    break;
                case TempTypes.TROPICAL:
                    switch (getHumidType(x, y)){
                        case HumidTypes.SUPER_ARID:
                            biome = "tropical desert";
                            break;
                        case HumidTypes.PER_ARID:
                            biome = "tropical desert scrub";
                            break;
                        case HumidTypes.ARID:
                            biome = "tropical thorn woodland";
                            break;
                        case HumidTypes.SEMI_ARID:
                            biome = "tropical very dry forest";
                            break;
                        case HumidTypes.SUB_HUMID:
                            biome = "tropical dry forest";
                            break;
                        case HumidTypes.HUMID:
                            biome = "tropical moist forest";
                            break;
                        case HumidTypes.PER_HUMID:
                            biome = "tropical wet forest";
                            break;
                        default:
                            biome = "tropical rain forest";
                            break;
                    }
                    break;
                default:
                    biome = "rock";
                    break;
            }
        }

        return biome;

        TempTypes getTempType(int x, int y){
            float temp = tempVals[x,y];
            if (temp < temps[5]){
                return TempTypes.POLAR;
            } else if (temp >= temps[5] && temp < temps[4]){
                return TempTypes.ALPINE;
            } else if (temp >= temps[4] && temp < temps[3]){
                return TempTypes.BOREAL;
            } else if (temp >= temps[3] && temp < temps[2]){
                return TempTypes.COOL;
            } else if (temp >= temps[2] && temp < temps[1]){
                return TempTypes.WARM;
            } else if (temp >= temps[1] && temp < temps[0]){
                return TempTypes.SUBTROPICAL;
            } else if (temp >= temps[0]){
                return TempTypes.TROPICAL;
            } else {
                return TempTypes.INVALID;
            }
        }

        HumidTypes getHumidType(int x, int y){
            float humid = humidVals[x,y];
            if ( humid < humids[6]){
                return  HumidTypes.SUPER_ARID;
            } else if (humid >= humids[6] && humid < humids[5]){
                return HumidTypes.PER_ARID;
            } else if (humid >= humids[5] && humid < humids[4]){
                return HumidTypes.ARID;
            } else if (humid >= humids[4] && humid < humids[3]){
                return HumidTypes.SEMI_ARID;
            } else if (humid >= humids[3] && humid < humids[2]){
                return HumidTypes.SUB_HUMID;
            } else if (humid >= humids[2] && humid < humids[1]){
                return HumidTypes.HUMID;
            } else if (humid >= humids[1] && humid < humids[0]){
                return HumidTypes.PER_HUMID; 
            } else if (humid >= humids[0]){
                return HumidTypes.SUPER_HUMID;
            } else {
                return HumidTypes.INVALID;
            }
        }
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
