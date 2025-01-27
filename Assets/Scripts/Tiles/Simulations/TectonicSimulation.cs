using System;
using System.Collections.Generic;
using UnityEngine;

using Random = UnityEngine.Random;

public class TectonicSimulation : MonoBehaviour{
    public GenerateWorld world;
    public MapTextureManager mapTexture;
    Dictionary<Vector2Int, float> elevations;

    Vector2Int worldSize;
    List<Plate> plates = new List<Plate>();
    Tile[,] tiles;

    public void Start(){
        worldSize = world.worldSize;

        CreatePlates(4, 2);
        InitHeightMap(Random.Range(1, 1000), 0.8f);
    }

    void InitHeightMap(float seed, float scale){
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                float elevation = Mathf.PerlinNoise(x * 0.1f + seed, y * 0.1f + seed);
                Tile tile = tiles[x,y];
                tile.elevation = elevation;
                mapTexture.SetPixelColor(x, y, Color.Lerp(tile.plate.displayColor, Color.black, tile.elevation));
                if (tile.elevation <= 0.4f){
                    mapTexture.SetPixelColor(x, y, Color.Lerp(tile.plate.displayColor, Color.blue, 0.8f));
                }

            }
        }
    }

    void CreatePlates(int gridSizeX, int gridSizeY){
        tiles = new Tile[worldSize.x,worldSize.y];

        // Points per grid cell
        int ppcX = worldSize.x / gridSizeX;
        int ppcY = worldSize.y / gridSizeY;

        Vector2Int[,] points = new Vector2Int[gridSizeX, gridSizeY];
        for (int gx = 0; gx < gridSizeX; gx++){
            for (int gy = 0; gy < gridSizeY; gy++){
                
                points[gx,gy] = new Vector2Int(gx * ppcX + Random.Range(0, ppcX), gy * ppcY + Random.Range(0, ppcY));
                plates.Add(new Plate(){
                    displayColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f)),
                    origin = points[gx,gy]
                });
                mapTexture.SetPixelColor(points[gx,gy], Color.black);
            }
        }

        // Plate selection
        for (int x = 0; x < worldSize.x; x++){
            for (int y = 0; y < worldSize.y; y++){
                tiles[x,y] = new Tile();
                float closestDist = Mathf.Infinity;
                Vector2Int nearestPoint = new Vector2Int();

                for (int dx = -1; dx < 2; dx++){
                    for (int dy = -1; dy < 2; dy++){
                        Debug.Log(ppcX);
                        Debug.Log(x / ppcX);
                        int gridX = x / ppcX;
                        int gridY = y / ppcY;

                        Debug.Log(gridX + dx);
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
                        tiles[x,y].plate = plate;
                    }
                }
                mapTexture.SetPixelColor(x, y, tiles[x,y].plate.displayColor);
            }
        }
            
    }

    class Tile{
        
        public Plate plate;
        public float elevation;
    }

    class Plate{
        public Vector2Int origin;
        public Color displayColor;
        public Vector2Int dir;
    }
}