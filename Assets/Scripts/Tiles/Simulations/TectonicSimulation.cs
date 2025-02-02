using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class TectonicSimulation : MonoBehaviour{
    public GenerateWorld world;
    public MapTextureManager mapTexture;
    Dictionary<Vector2Int, float> elevations;

    Vector2Int worldSize;
    List<Plate> plates = new List<Plate>();
    WorldTile[,] tiles;
    int years = 0;

    void Start(){
        worldSize = world.worldSize;

        CreatePlates(4, 2);
        //InitHeightMap(Random.Range(1, 1000), 20);
        
    }

    void Update(){
        years++;
        print((years + 1) + " Million Years");
        SimulateStep();
        UpdateVisuals();
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

                    int moveX = x + dx;
                    int moveY = y + dy;

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
                    // Allows crust to move next frame
                    crust.moved = false;
                }
                // Divergent crust
                if (tile.crust.Count < 1){
                    CrustTile newCrust = new CrustTile(){
                        plate = tile.lastPlate,
                        pos = new Vector2Int(x, y)
                    };
                    tile.crust.Add(newCrust);
                    tile.topCrust = newCrust;
                }
                tile.topCrust = tile.crust[0];      
                // Convergent crust
                if (tile.crust.Count > 1){
                    CrustTile topCrust = null;
                    int highestAge = int.MinValue;
                    foreach (CrustTile crust in tile.crust.ToArray()){
                        if (crust.age + crust.plate.density > highestAge){
                            topCrust = crust;
                            highestAge = crust.age + crust.plate.density;
                        }
                    }
                    tile.topCrust = topCrust;
                    foreach (CrustTile crust in tile.crust.ToArray()){
                        if (crust != tile.topCrust){
                            // Oceanic subduction
                            crust.lostElevation += Random.Range(0.01f, 0.04f);

                            if (crust.lostElevation >= 0.2f){
                                // Island chain volcanoes
                                topCrust.elevation += Random.Range(0.0025f, 0.0125f);
                                tile.crust.Remove(crust);
                            }
                        }
                    }
                }     
            }
        }            
    }

    void UpdateVisuals(){
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                WorldTile tile = tiles[x,y];
                if (tile.crust.Count > 0){
                    //mapTexture.SetPixelColor(x, y, Color.Lerp(Color.black, tile.topCrust.plate.color, tile.topCrust.elevation));
                    
                    mapTexture.SetPixelColor(x, y, Color.Lerp(Color.black, Color.blue, tile.topCrust.elevation + 0.4f));
                    if (tile.topCrust.elevation > 0.6f){
                        mapTexture.SetPixelColor(x, y, Color.green);
                        if (tile.topCrust.elevation > 0.8f){
                            mapTexture.SetPixelColor(x, y, Color.green);
                        }
                    }
                    
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
                        tiles[x,y].crust.Add(new CrustTile(){
                        plate = plate,
                        pos = new Vector2Int(x, y)
                        });
                        tiles[x,y].topCrust = tiles[x,y].crust[0];
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
                                int moveX = x + dx;
                                int moveY = y + dy;

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

                                WorldTile tile2 = tiles[moveX,moveY];
                                if (tile2.crust.Count < 1 && Random.Range(0f, 1f) < 0.2f){
                                    tile2.crust.Add(new CrustTile(){
                                    plate = tile.topCrust.plate,
                                    pos = new Vector2Int(x, y)
                                    });
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

            
        // Plate selection
        /*
        for (int x = 0; x < worldSize.x; x++){
            for (int y = 0; y < worldSize.y; y++){
                tiles[x,y] = new WorldTile();
                float closestDist = Mathf.Infinity;
                Vector2Int nearestPoint = new Vector2Int();

                for (int dx = -1; dx < 2; dx++){
                    for (int dy = -1; dy < 2; dy++){

                        int gridX = x / ppcX;
                        int gridY = y / ppcY;

                        int tx = gridX + dx;
                        int ty = gridY + dy;

                        if (tx < 0 || ty < 0 || tx >= gridSizeX || ty >= gridSizeY){
                            continue;
                        }

                        float dist = Vector2Int.Distance(new Vector2Int(x, y), points[tx, ty]);

                        if (dist < closestDist){
                            closestDist = dist;
                            nearestPoint = points[tx, ty];
                        }
                    }
                }
                foreach (Plate plate in plates){
                    if (plate.origin == nearestPoint){
                        tiles[x,y].crust.Add(new CrustTile(){
                            plate = plate,
                            pos = new Vector2Int(x, y)
                        });
                    }
                }
                tiles[x,y].topCrust = tiles[x,y].crust[0];
                mapTexture.SetPixelColor(x, y, tiles[x,y].crust[0].plate.color);
            }
        }
        */
    }

    public enum CrustTypes{
        OCEANIC,
        CONTINENTAL,
    }

    class WorldTile{
        public float elevation = 0.5f;
        public List<CrustTile> crust = new List<CrustTile>();
        public CrustTile topCrust;
        public Plate lastPlate;
    }
    class CrustTile{
        public bool moved = false;
        public int age = 0;
        public int densityOffset;
        public Vector2Int pos = new Vector2Int();
        public CrustTypes crustType = CrustTypes.OCEANIC;
        public Plate plate;
        public float elevation = 0.5f;
        public float lostElevation = 0f;
    }

    class Plate{
        public Vector2Int origin;
        public Color color;
        public Vector2 dir;
        public Vector2Int diagDir;
        public Vector2Int moveStep;
        public int density;
    }
}