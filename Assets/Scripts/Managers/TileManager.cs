using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine.EventSystems;
using System.Linq;
using Unity.Collections;
using UnityEngine.InputSystem.Interactions;
using Unity.VisualScripting;

public class TileManager : MonoBehaviour
{
    public Vector2Int worldSize;

    public MapTextureManager map;
    public NationPanel nationPanel;

    [Header("Nation Spawning")]
    [SerializeField]
    [Range(0f,1f)]
    float stateSpawnChance = 0.1f;

    [SerializeField]
    [Range(0f,1f)]
    float anarchyConquestChance = 0.5f;

    [SerializeField]
    public int minNationPopulation = 500;
    [SerializeField]
    int initialAnarchy = 20;

    [Header("Pops")]
    [SerializeField]
    int popsToCreate = 1;

    [Header("Debug")]

    [SerializeField]
    bool updateMap = false;

    // Lists & Stats
    //public int worldPopulation;
    public Tile[,] tiles = new Tile[0,0];

    public List<State> states = new List<State>();
    public List<Tile> anarchy = new List<Tile>();

    public void Awake(){
        Events.tick += Tick;
    }
    public void Init(){
        // Goes thru the tiles
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                Tile tile = tiles[x,y];
                tile.tilePos = new Vector2Int(x, y);

                Events.tick += tile.Tick;
                tile.tileManager = this;
                tile.GetMaxPopulation();
                tile.terrainColor = Tile.Hetx2RGB(tile.biome.color);

                for (int x2 = -1; x2 <= 1; x2++){
                    for (int y2 = -1; y2 <= 1; y2++){
                        Tile borderTile = getTile(x2 + x, y2 + y);
                        if (borderTile != null && borderTile != tile){
                            tile.borderingTiles.Append(borderTile);
                        }
                    }
                }    
            }
            // Adds initial anarchy
            addInitialAnarchy(initialAnarchy);
            // Initializes tile populations
            foreach (Tile tile in anarchy){
                initPopulation(tile, popsToCreate);
            }
            // Sets the map colors
            updateAllColors();                
        }

    }

    void addInitialAnarchy(int seedAmount){
        
        for (int i = 0; i < seedAmount; i++) {
            int attempts = 300;
            // gets the tile
            Tile tile = getRandomTile();

            // Checks if the tile has conditions that makes anarchy impossible
            while (!tile.claimable || tile.anarchy || tile.fertility < 0.5f){
                tile = null;
                attempts--;
                // If we run out of attempts break to avoid forever loops
                if (attempts < 0){
                    break;
                }
                // Picks another tile
                tile = getRandomTile();
                
            }

            if (tile != null){
                // Adds anarchy to the tile
                SetAnarchy(tile.tilePos.x, tile.tilePos.y, true);
 
                for (int x = -3; x <= 3; x++){
                    for (int y = -3; y <= 3; y++){
                        Tile tile1 = getTile(tile.tilePos.x + x, tile.tilePos.y + y);
                        if (tile1 != null && tile1.claimable && Random.Range(0f, 1f) < 0.2f){
                            SetAnarchy(tile.tilePos.x + x, tile.tilePos.y + y, true);
                        }
                    }
                }
            }
        }

    }

    Tile getRandomTile(){
        // Picks a random tile
        return tiles[Random.Range(0, worldSize.x - 1), Random.Range(0, worldSize.y - 1)];
    }

    public void Tick(){
        // Each tick new nations can spawn out of anarchy
        if (Random.Range(0f, 1f) < 0.75f){
            //creationTick();
        }
        Events.TickPops();
        Events.TickTiles();
        Events.TickStates();

        // Each game tick nations can expand into neutral lands
    }
    void creationTick(){
        // Checks if there are any anarchic tiles
        if (anarchy.Count > 0){
            // Selects a random one
            Tile tile = anarchy[Random.Range(0, anarchy.Count)];
            // If the tile is anarchy, has sufficient population, and passes the random check
            if (tile.anarchy && Random.Range(0f, 1f) < stateSpawnChance * Mathf.Clamp01(tile.fertility + 0.2f) && tile.population >= minNationPopulation){
                // Creates a new random state at that tile
                createRandomState(tile.tilePos.x, tile.tilePos.y);
            }   
        }
          
    }

    void initPopulation(Tile tile, int amountToCreate = 50){
        for (int i = 0; i < amountToCreate; i++){
            float r = (tile.tilePos.x + 0.001f) / (worldSize.x + 0.001f);
            float g = (tile.tilePos.y + 0.001f) / (worldSize.y + 0.001f);
            float b = (worldSize.x - tile.tilePos.x + 0.001f) / (worldSize.x + 0.001f);
            Culture culture = new Culture(){
                color = new Color(r, g, b, 1f)
            };
            print(tile.fertility);
            tile.CreatePop(Mathf.FloorToInt(500 * tile.fertility), culture);
        }
    }

    public void SetAnarchy(int x, int y, bool state){
        Tile tile = getTile(x, y);
        if (tile != null){
            // If the tile doesnt have anarchy add anarchy
            tile.anarchy = state;
            updateColor(x, y);

            if (state){
                anarchy.Add(tile);
            } else {
                anarchy.Remove(tile);
            }
        }
    }

    void createRandomState(int x, int y){
        State newState = State.CreateRandomState();
        // Adds it to the nations list
        if (!states.Contains(newState)){
            states.Add(newState);
        }
        // Sets the parent of the nation to the nationholder object
        newState.tileManager = this;
        // And adds the very first tile :D
        newState.AddTile(x, y);
        // Connects the tile to ticks
        Events.stateTick += newState.Tick;
    }

    public void Border(int x, int y){
        // Gets a tile
        Tile tile = getTile(x, y);
        if (tile != null){
            // If a tile is a border at all
            tile.border = false;
            // If a tile borders a neutral tile
            tile.frontier = false;
            
            tile.nationalBorder = false;
            tile.borderingStates.Clear();
            // Goes through the tiles adjacents
            for (int xd = -1; xd <= 1; xd++){
                for (int yd = -1; yd <= 1; yd++){
                    if (yd == 0 && xd == 0){
                        // If the tile is self skip (You cant border yourself silly!)

                        continue;
                    }
                    // If not, gets the adjacent tiles positon
                    Vector2Int pos = new Vector2Int(xd + x, yd + y);
                    // Makes sure that tile exists
                    if (getTile(pos.x, pos.y) != null && (getTile(pos.x, pos.y).state != tile.state)){
                        // If it does and it doesnt have the same owner as us, makes this tile a border :D
                        // But wait, theres more!
                        tile.border = true;

                        if (tile.state != null && getTile(pos.x, pos.y).state != tile.state && getTile(pos.x, pos.y).state != null){
                            tile.nationalBorder = true;

                            //var nation = tile.owner;
                            var borderState = getTile(pos.x, pos.y).state;

                            if (!tile.borderingStates.Contains(borderState)){
                                tile.borderingStates.Add(borderState);
                            }
                        }

                        if (getTile(pos.x, pos.y).state == null){
                            // If the tested border is neutral
                            if (getTile(pos.x, pos.y).claimable){
                                // Makes it a frontier
                                // Frontier tiles are the only ones that can colonize neutral tiles
                                tile.frontier = true;
                            }
                        }
                        // Updates the color (For border shading)
                        updateColor(x, y);
                    }
                }
            }
            // Updates the color (In case the tile no longer has special properties :<)
            updateColor(x, y);
            
        } else {
            return;
        }
    }

    public void updateBorders(int x, int y){
        // Gets the tile at a position
        Tile tile = getTile(x, y);
        if (tile != null){
            // If the tile exists goes through its adjacent tiles
            for (int xd = -1; xd <= 1; xd++){
                    for (int yd = -1; yd <= 1; yd++){
                        // gets the tiles key of this offset
                        Vector2Int pos = new Vector2Int(xd + x, yd + y);
                        if (getTile(pos.x, pos.y) != null){
                            // Makes that tile check its borders
                            // NOTE: Also runs on self :D
                            Border(pos.x, pos.y);
                            if (tile.state != null){
                                if (getTile(pos.x, pos.y).state != null){
                                    getTile(pos.x, pos.y).state.getBorders();
                                }
                            }
                        }
                    }
                }
        } else {
            return;
        }
    }

    public Tile getTile(int x, int y){
        // makes sure the key we are getting exists
        if (x < worldSize.x && y < worldSize.y && x >= 0 && y >= 0){
            return tiles[x,y];
        }
        return null;
    }

    // Map Rendering

    public enum MapModes {
        POLITICAL,
        CULTURE,
        POPULATION,
        TERRAIN,
        TECH,
        POPS
    }

    public MapModes mapMode = MapModes.POLITICAL;

    public void updateAllColors(){
        Color32[] colors = map.texture.GetPixels32();
        int index = 0;
        for (int y = 0; y < worldSize.y; y++){
            for (int x = 0; x < worldSize.x; x++){
                index = (y * worldSize.x) + x;
                Color newColor = getColor(x, y);
                //print(newColor);
                colors[index] = newColor;
            }
        }
        map.texture.SetPixels32(colors);
        map.texture.Apply();
    }


    // COLOR
    public void updateColor(int x, int y){
        map.SetPixelColor(x, y, getColor(x, y));
    }
    public Color getColor(int x, int y){
        // Gets the final color
        Color finalColor = new Color();
        // Gets the tile we want to paint
        Tile tile = getTile(x, y);
        State state = tile.state;
        State liege = null;
        bool isCapital = false;
        if (state != null && state.capital == tile){
            isCapital = true;
        }

        if (state != null && state.liege != null){
            liege = state.liege;
        }
        switch (mapMode){
            case MapModes.POLITICAL:
                if (state != null){
                    // If the tile has an owner, colors it its nation
                    finalColor = state.mapColor;
                    // If the tile is a border
                    if (tile.border){
                        // Colors it slightly darker to show where nation boundaries are
                        finalColor = state.mapColor * 0.7f + Color.black * 0.3f;
                    }
                    if (tile.state.capital == tile){
                        finalColor = state.capitalColor;
                    }
                } else {
                    ColorTerrain();
                    // Or if we are anarchy visualize it
                    if (tile.anarchy){
                        finalColor = Color.black;
                    }
                }
            break;
            case MapModes.TERRAIN:
                if (state != null && tile.border){
                    // Colors it slightly darker to show where nation boundaries are
                    finalColor = state.mapColor;
                } else {
                    ColorTerrain();
                }
            break;
            case MapModes.POPULATION:
                if (tile.biome.terrainType != BiomeTerrainType.WATER && tile.pops.Length > 0){
                    finalColor = new Color(0f, tile.population / 100000f, 0f);
                } else {
                    ColorTerrain();
                }           
            break;
            case MapModes.POPS:
                if (tile.biome.terrainType != BiomeTerrainType.WATER && tile.pops.Length > 0){
                    finalColor = new Color(0f, tile.pops.Length / 10f, 0f);
                } else {
                    ColorTerrain();
                }  
            break;  
            case MapModes.CULTURE:
                if (tile.pops.Length > 0){
                    Pop largest = new Pop(){
                        size = -1
                    };
                    foreach (Pop pop in tile.pops.ToArray()){
                        if (pop.size > largest.size){
                            largest = pop;
                        }
                    }
                    finalColor = largest.culture.color;
                } else {
                    ColorTerrain();
                }
            break;
            case MapModes.TECH:
                if (tile.pops.Length > 0){
                    finalColor = new Color(tile.tech.militaryLevel / 20f, tile.tech.industryLevel / 20f, tile.tech.societyLevel / 20f);
                } else {
                    ColorTerrain();
                }
            break;

        }

        void ColorTerrain(){
            // If the tile isnt owned, just sets the color to the color of the terrain
            finalColor = tile.terrainColor;   
        }
        
        // Higlights selected nation
        if (nationPanel.tileSelected != null){
            // Sets all the selected data

            Tile selectedTile = nationPanel.tileSelected;
            State selectedState = nationPanel.tileSelected.state;
            State selectedLiege = null;

            if (selectedState != null && selectedState.liege != null){
                selectedLiege = selectedState.liege;
            }
            // If the tile is one that is colored by the mapmode
            bool colored = false;
            switch (mapMode){
                case MapModes.POLITICAL:
                    // If the tile isnt the selected nation
                    if (state != selectedState){
                        // Checks if we share a liege with the selected state
                        bool sharesLiege = liege != null && liege == selectedLiege;
                        // Checks if we are a vassal of the selected state
                        bool isVassal = state != null && selectedState.vassals.ContainsKey(state);
                        // Checks if we are related in any way to the selected state
                        bool related = state == selectedLiege || isVassal || sharesLiege;

                        // Checks if we are related to the tile and if we arent the capital
                        if (state != null && related){
                            // Checks if we are a vassal of or we share a liege of the selected state
                            if (!isCapital){
                                if (isVassal || sharesLiege){
                                    finalColor = finalColor * 0.5f + Color.yellow * 0.5f;
                                }
                                // Otherwise checks if we are the liege of the selected state
                                else if (state == selectedLiege){
                                finalColor = finalColor * 0.5f + Color.magenta * 0.5f;
                                }
                            }
                            colored = true;
                        }
                    } else {
                        colored = true;
                    }                
                break;
            }
            if (!colored){
                // Darkens tiles that werent colored by mapmode
                finalColor = finalColor * 0.5f + Color.black * 0.5f;
            }


        }
        // Finally gets our color
        return finalColor;
    }

    void ChangeMapMode(MapModes newMode){
        mapMode = newMode;
        updateAllColors();
    }

    void CheckMapModeSwitch(KeyCode key, MapModes mode){
        if (Input.GetKeyDown(key)){
            if (mapMode != mode){
                nationPanel.tileSelected = null;
                nationPanel.Disable();
                ChangeMapMode(mode);
            } else {
                ChangeMapMode(MapModes.POLITICAL);
            }
        }          
    }
    void Update(){
        if (updateMap){
            updateAllColors();
            updateMap = false;
        }
        if (nationPanel && Input.GetMouseButtonDown(0) && mapMode == MapModes.POLITICAL){
            // Stops everything from running if there isnt input or if the panel doesnt even exist
            detectTileClick();
        }
        CheckMapModeSwitch(KeyCode.C, MapModes.CULTURE);
        CheckMapModeSwitch(KeyCode.X, MapModes.TERRAIN);
        CheckMapModeSwitch(KeyCode.Z, MapModes.POPULATION);
        CheckMapModeSwitch(KeyCode.V, MapModes.TECH);
        CheckMapModeSwitch(KeyCode.P, MapModes.POPS);
    }

    void detectTileClick(){
        // Gets the mouse pos on the world
        Vector3 globalMousePos = FindAnyObjectByType<Camera>().ScreenToWorldPoint(Input.mousePosition);

        // Checks if the mouse is over a ui element
        bool overUI = EventSystem.current.IsPointerOverGameObject();

        // Converts the mouse position to a grid position
        int x = Mathf.RoundToInt(Mathf.Round(Input.mousePosition.x / 6.5f) * 6.5f);
        int y = Mathf.RoundToInt(Mathf.Round(Input.mousePosition.y / 6.5f) * 6.5f);
        Vector2Int mouseGridPos = new Vector2Int(x, y);
        Tile tile = getTile(mouseGridPos.x, mouseGridPos.y);
        if (tile != null){
            if (!overUI){
                // If the mouse isnt over a ui element, gets the tile
                
                // If the tile has an owner
                if (tile != null && tile.state != null){
                    // Checks if we arent just clicking on the same tile
                    if (nationPanel.tileSelected == null || nationPanel.tileSelected.state != tile.state){
                        // Sets the selected tile and makes the panel active
                        nationPanel.Enable(tile);
                    }
                    
                } else {
                    // Makes sure we arent just continually clicking on neutral tiles
                    if (nationPanel.tileSelected != null){
                        // Hides the ui and makes it not display anything >:)
                        nationPanel.Disable();
                    }
                        
                }
            }
        }
    }
}
