using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class TectonicSimulation : MonoBehaviour{
    public GenerateWorld world;
    public MapTextureManager mapTexture;
    public float oceanDepth = 0.2f;
    public float seaLevel = 0.6f;


    Vector2Int worldSize;
    List<Plate> plates = new List<Plate>();
    WorldTile[,] tiles;
    int years = 0;

    void Start(){
        worldSize = world.worldSize;

        CreatePlates(4, 4);
        InitHeightMap();
        
    }

    void Update(){
        years++;
        print((years + 1) + " Million Years");
        SimulateStep();
        HydraulicErosion();
        UpdateVisuals();
    }

    void HydraulicErosion(){
        // Hydraulic erosion
        List<Vector2Int> rivers = new List<Vector2Int>();
        Vector2Int[] samplePoints = new Vector2Int[Mathf.RoundToInt((worldSize.x * worldSize.y) / 8)];
        int attempts = 0;
        for (int i = 0; i < samplePoints.Length; i++){
            Vector2Int samplePos = new Vector2Int(Random.Range(0, worldSize.x - 1), Random.Range(0, worldSize.y - 1));
            while (!samplePoints.Contains(samplePos) && attempts < 50){
                attempts++;
                samplePos = new Vector2Int(Random.Range(0, worldSize.x - 1), Random.Range(0, worldSize.y - 1));
            }
            samplePoints[i] = samplePos;
        }
        // Hydraulic
        foreach (Vector2Int point in samplePoints){
            WorldTile tile = tiles[point.x, point.y];
            if (tile.topCrust.elevation > 0.7f){
                rivers.Add(point);
                Vector2Int riverPos = point;
                bool riverEnd = false;
                int steps = 0;
                while (!riverEnd || steps < 50){
                    steps++;
                    bool canContinue = false;
                    for (int dx = -1; dx < 2; dx++){
                        for (int dy = -1; dy < 2; dy++){
                            Vector2Int newPos = GetNewPos(new Vector2Int(riverPos.x, riverPos.y), new Vector2Int(dx, dy));
                            int x = newPos.x;
                            int y = newPos.y;
                            WorldTile target = tiles[x, y];
                            if (target.topCrust.elevation < tile.topCrust.elevation && !canContinue && target.topCrust.elevation > seaLevel && !rivers.Contains(newPos) && Random.Range(0f, 1f) < 0.25f){
                                float waterFlow = tile.topCrust.elevation - target.topCrust.elevation;
                                canContinue = true;
                                riverPos = newPos;
                                if (waterFlow > 0.01f){
                                    tile.topCrust.elevation = Mathf.Lerp(tile.topCrust.elevation, seaLevel + 0.1f, waterFlow/10f);
                                    //target.topCrust.elevation += waterFlow/100f;
                                }
                                
                                rivers.Add(riverPos);
                            } else {
                                canContinue = false;
                                continue;
                            }
                        }
                    }
                    riverEnd = !canContinue;                    
                }
                
            }

        }
    }
    void InitHeightMap(){
        System.Random rand = new System.Random(world.noiseSeed);
        float[,] heights = Noise.GenerateNoiseMap(worldSize.x, worldSize.y, rand.Next(-10000, 10000), world.totalNoiseScale, 8);
        float[,] falloff = Noise.GenerateFalloffMap(worldSize.x, worldSize.y);
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                float height = heights[x,y] - falloff[x,y];
                if (height < 0f){
                    height = 0f;
                }
                WorldTile tile = tiles[x,y];
                if (height > seaLevel){
                    tile.topCrust.crustType = CrustTypes.CONTINENTAL;
                } else {
                    height = Mathf.Lerp(seaLevel - oceanDepth, seaLevel, calcInverseFallof(Mathf.InverseLerp(seaLevel, 0f, height)));
                }
                    
                tile.topCrust.elevation = height;//Mathf.Max(height, -oceanDepth + height * 0.15f);

            }
        }

        float calcInverseFallof(float value){
            float a = 3f;
            float b = .15f;
            return 1f - Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
        }       
    }

    Vector2Int GetNewPos(Vector2Int pos, Vector2Int dir){
        int dx = dir.x;
        int dy = dir.y;

        int moveX = pos.x + dx;
        int moveY = pos.y + dy;

        if (moveX > worldSize.x - 1){
            moveX = 0;
        }
        if (moveX < 0){
            moveX = worldSize.x - 1;
        }
        if (moveY > worldSize.y - 1){
            moveY = 0;
        }
        if (moveY < 0){
            moveY = worldSize.y - 1;
        }    

        return new Vector2Int(moveX, moveY);    
    }
    void SimulateStep(){
        foreach (Plate plate in plates){
            plate.diagDir = new Vector2Int(0,0);
            plate.moveStep += new Vector2Int(1,1);
            if (plate.moveStep.x > Mathf.Abs(1 / Mathf.Abs(plate.dir.x % 1))){
                plate.moveStep.x = 0;
                plate.diagDir.x = (int) Mathf.Sign(plate.dir.x);
            }   
            if (plate.moveStep.y > Mathf.Abs(1 / Mathf.Abs(plate.dir.y % 1))){
                plate.moveStep.y = 0;
                plate.diagDir.y = (int) Mathf.Sign(plate.dir.y);
            }       
        }
        // Moves plates
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                WorldTile tile = tiles[x,y];
                tile.lastPlate = tile.topCrust.plate;
                
                foreach (CrustTile crust in tile.crust.ToArray()){
                    crust.age++;

                    int dx = (int) crust.plate.dir.x;
                    int dy = (int) crust.plate.dir.y;
                    dx += crust.plate.diagDir.x;
                    dy += crust.plate.diagDir.y;

                    int moveX = GetNewPos(new Vector2Int(x,y), new Vector2Int(dx, dy)).x;
                    int moveY = GetNewPos(new Vector2Int(x,y), new Vector2Int(dx, dy)).y;

                    if (!crust.moved){
                        crust.pos = new Vector2Int(moveX, moveY);
                        tile.crust.Remove(crust);   
                        tiles[moveX, moveY].crust.Add(crust);
                        crust.moved = true;                        
                    }                 
                }
            }
        }
        
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                WorldTile tile = tiles[x,y];
                foreach (CrustTile crust in tile.crust.ToArray()){
                    // Mid ocean ridge generation
                    if (crust.age <= 3){
                        crust.elevation = Mathf.Lerp(crust.elevation, seaLevel - oceanDepth, 0.7f);
                    }
                    // Allows crust to move next frame
                    crust.moved = false;
                }
                // Divergent crust
                if (tile.crust.Count < 1){
                    CrustTile newCrust = new CrustTile(){
                        plate = tile.lastPlate,
                        pos = new Vector2Int(x, y),
                        age = 0,
                        elevation = Random.Range(seaLevel - oceanDepth + 0.2f, seaLevel - oceanDepth + 0.25f)
                    };
                    tile.crust.Add(newCrust);
                    newCrust.plate.tiles.Add(newCrust);
                    tile.topCrust = newCrust;
                }
                tile.topCrust = tile.crust[0];      
                // Convergent crust
                if (tile.crust.Count > 1){
                    // Gets plate on top
                    CrustTile topCrust = null;
                    int lowestAge = int.MaxValue;
                    foreach (CrustTile crust in tile.crust.ToArray()){
                        int continentalFactor = 0;
                        if (crust.crustType == CrustTypes.CONTINENTAL){
                            continentalFactor = 1000000;
                        }
                        int ageModified = crust.age + crust.plate.density - continentalFactor;
                        if (ageModified < lowestAge){
                            topCrust = crust;
                            lowestAge = ageModified;
                        }
                    }
                    tile.topCrust = topCrust;
                    foreach (CrustTile crust in tile.crust.ToArray()){
                        if (crust != tile.topCrust){
                            // Oceanic subduction
                            if (crust.crustType == CrustTypes.OCEANIC){
                                crust.lostElevation += Random.Range(0.1f, 0.2f);

                                if (crust.lostElevation >= crust.elevation){
                                    // Island chain volcanoes
                                    topCrust.elevation += Random.Range(0.01f, 0.04f);
                                    DeleteCrust(tile, crust);
                                }                                
                            } else {
                                // Continental Collision
                                if (!crust.plate.velChanged){
                                    crust.plate.velChanged = true;
                                    crust.plate.dir = Vector2.Lerp(crust.plate.dir, topCrust.plate.dir, Random.Range(0.1f, 0.5f));
                                }
                                
                                topCrust.elevation += Random.Range(0.005f, 0.025f);
                                crust.lostElevation += Random.Range(0.1f, 0.02f);

                                if (crust.lostElevation >= crust.elevation){
                                    DeleteCrust(tile, crust);
                                }
                            }
                        }
                    }
                }     
            }
        } 
        
        foreach (Plate plate in plates.ToArray()){
            foreach (Plate merger in plates.ToArray()){
                if (plate.dir == merger.dir){
                    foreach (CrustTile tile in plate.tiles.ToArray()){
                        tile.plate = merger;
                        merger.tiles.Add(tile);
                            
                        plate.tiles.Remove(tile);                            

                    }
                }
            }
            if (plate.tiles.Count < 1){
                plates.Remove(plate);
            }
        }
        // Modifies plate velocity
        foreach (Plate plate in plates){
            plate.velChanged = false;
            if (Random.Range(0f, 1f) <= 0.25f){
                plate.dir += new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f));
                if (plate.dir.magnitude > 1f){
                    plate.dir = plate.dir.normalized * 1f;
                }
            }
        }           
    }

    void DeleteCrust(WorldTile tile, CrustTile crust){
        tile.crust.Remove(crust);
        crust.plate.tiles.Remove(crust);       
    }

    void UpdateVisuals(){
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                WorldTile tile = tiles[x,y];
                if (tile.crust.Count > 0){
                    
                    
                    mapTexture.SetPixelColor(x, y, Color.Lerp(Color.black, Color.blue, tile.topCrust.elevation + 0.4f));
                    if (tile.topCrust.elevation > seaLevel){
                        mapTexture.SetPixelColor(x, y, Color.Lerp(Color.green, Color.yellow, (tile.topCrust.elevation - 0.6f)/(0.4f)));
                    }
                    //mapTexture.SetPixelColor(x, y, Color.Lerp(Color.black, Color.white, tile.topCrust.elevation));
                    //mapTexture.SetPixelColor(x, y, Color.Lerp(Color.black, tile.topCrust.plate.color, tile.topCrust.elevation));
                    /*
                    if (tile.topCrust.crustType == CrustTypes.CONTINENTAL){
                        mapTexture.SetPixelColor(x, y, Color.red);
                    } else {
                        mapTexture.SetPixelColor(x, y, Color.blue);
                    }
                    */
                    //mapTexture.SetPixelColor(x, y, Color.Lerp(Color.red, Color.black, (float)tile.crust[0].age / 200f));
                } else {
                    mapTexture.SetPixelColor(x, y, Color.Lerp(Color.black, Color.red, 0.05f));
                }

                
            }
        }
    }

    void CreatePlates(int gridSizeX, int gridSizeY){
        tiles = new WorldTile[worldSize.x,worldSize.y];
        
        List<int> densities = new List<int>();
        for (int i = 0; i < gridSizeX * gridSizeY; i++){
            densities.Add(i);
        }

        // Points per grid cell
        int ppcX = worldSize.x / gridSizeX;
        int ppcY = worldSize.y / gridSizeY;

        Vector2Int[,] points = new Vector2Int[gridSizeX, gridSizeY];
        for (int gx = 0; gx < gridSizeX; gx++){
            for (int gy = 0; gy < gridSizeY; gy++){
                
                points[gx,gy] = new Vector2Int(gx * ppcX + Random.Range(0, ppcX), gy * ppcY + Random.Range(0, ppcY));
                Plate newPlate = new Plate(){
                    color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f)),
                    origin = points[gx,gy],
                    dir = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f))
                };

                int densityIndex = Random.Range(0, densities.Count - 1);
                newPlate.density = densities[densityIndex];
                densities.Remove(densities[densityIndex]);

                plates.Add(newPlate);
                mapTexture.SetPixelColor(points[gx,gy], Color.black);
            }
        }
        int freeTiles = (worldSize.x * worldSize.y) - plates.Count;

        for (int x = 0; x < worldSize.x; x++){
            for (int y = 0; y < worldSize.y; y++){
                tiles[x,y] = new WorldTile();
                foreach (Plate plate in plates){
                    if (plate.origin == new Vector2Int(x,y)){
                        CrustTile newCrust = new CrustTile(){
                        plate = plate,
                        pos = new Vector2Int(x, y)
                        };
                        tiles[x,y].crust.Add(newCrust);
                        tiles[x,y].topCrust = tiles[x,y].crust[0];
                        plate.tiles.Add(newCrust);
                    }
                    
                }
            }
        }

        // floodFill
        bool[,] moved = new bool[worldSize.x, worldSize.y];
        while (freeTiles > 0){
            for (int x = 0; x < worldSize.x; x++){
                for (int y = 0; y < worldSize.y; y++){
                    WorldTile tile = tiles[x,y];
                    if (tile.topCrust != null && !moved[x,y]){
                        for (int dx = -1; dx < 2; dx++){
                            for (int dy = -1; dy < 2; dy++){
                                if (dx != 0 && dy != 0){
                                    continue;
                                }
                                int moveX = GetNewPos(new Vector2Int(x,y), new Vector2Int(dx, dy)).x;
                                int moveY = GetNewPos(new Vector2Int(x,y), new Vector2Int(dx, dy)).y;

                                WorldTile tile2 = tiles[moveX,moveY];
                                if (tile2.crust.Count < 1 && Random.Range(0f, 1f) < 0.2f){
                                    CrustTile newCrust = new CrustTile(){
                                    plate = tile.topCrust.plate,
                                    pos = new Vector2Int(x, y)
                                    };
                                    tile2.crust.Add(newCrust);
                                    tile.topCrust.plate.tiles.Add(newCrust);
                                    moved[moveX,moveY] = true;  
                                    freeTiles--;
                                    tile2.topCrust = tile2.crust[0]; 
                                }
                                                     
                            }
                        }

                    }
                }
            }
            for (int x = 0; x < worldSize.x; x++){
                for (int y = 0; y < worldSize.y; y++){
                    moved[x,y] = false;
                }  
            }            
        }
    }

    public enum CrustTypes{
        OCEANIC,
        CONTINENTAL,
    }

    class WorldTile{
        public List<CrustTile> crust = new List<CrustTile>();
        public CrustTile topCrust;
        public Plate lastPlate;
    }
    class CrustTile{
        public bool moved = false;
        public int age = 10;
        public int densityOffset;
        public Vector2Int pos = new Vector2Int();
        public CrustTypes crustType = CrustTypes.OCEANIC;
        public Plate plate;
        public float elevation = 0.5f;
        public float lostElevation = 0f;
    }

    class Plate{
        public bool velChanged = false;
        public Vector2Int origin;
        public Color color;
        public Vector2 dir;
        public Vector2Int diagDir;
        public Vector2Int moveStep;
        public List<CrustTile> tiles = new List<CrustTile>();
        public int density;
    }
}