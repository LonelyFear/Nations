using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine.EventSystems;
using System.Linq;
using Unity.Collections;

public class TileManager : MonoBehaviour
{
    public Vector2Int worldSize;
    public Tilemap tilemap;
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
    public Dictionary<Vector3Int, Tile> tiles = new Dictionary<Vector3Int, Tile>();

    public List<State> states = new List<State>();
    public List<Tile> anarchy = new List<Tile>();

    public void Awake(){
        Events.tick += Tick;
    }
    public void Init(){
        // Goes thru the tiles
        for (int i = 0; i < tiles.Count; i++){
            Tile tile = tiles.ElementAt(i).Value;
            Vector3Int tilePos = tiles.ElementAt(i).Key;

            Events.tick += tile.Tick;
            tile.tileManager = this;
            tile.GetMaxPopulation();
            tile.terrainColor = Tile.Hetx2RGB(tile.biome.color);

            for (int x = -1; x <= 1; x++){
                for (int y = -1; y <= 1; y++){
                    Tile borderTile = getTile(new Vector3Int(x, y) + tilePos);
                    if (borderTile != null && borderTile != tile){
                        tile.borderingTiles.Append(borderTile);
                        tile.borderingPositions.Append(new Vector3Int(x, y) + tilePos);
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
        updateAllColors(true);
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
                addAnarchy(tile.tilePos);
 
                for (int x = -3; x <= 3; x++){
                    for (int y = -3; y <= 3; y++){
                        Vector3Int newAnarchyPos = new Vector3Int(tile.tilePos.x + x, tile.tilePos.y + y);
                        Tile tile1 = getTile(newAnarchyPos);
                        if (tile1 != null && tile1.claimable && Random.Range(0f, 1f) < 0.2f){
                            addAnarchy(newAnarchyPos);
                        }
                    }
                }
            }
        }

    }

    Tile getRandomTile(){
        // Picks a random tile
        return tiles.Values.ElementAt(Random.Range(0, tiles.Keys.Count - 1));
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
        neutralExpansion();
    }
    void creationTick(){
        // Checks if there are any anarchic tiles
        if (anarchy.Count > 0){
            // Selects a random one
            Tile tile = anarchy[Random.Range(0, anarchy.Count)];
            // If the tile is anarchy, has sufficient population, and passes the random check
            if (tile.anarchy && Random.Range(0f, 1f) < stateSpawnChance * Mathf.Clamp01(tile.fertility + 0.2f) && tile.population >= minNationPopulation){
                // Creates a new random state at that tile
                createRandomState(tile.tilePos);
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


    public void neutralExpansion(){
        foreach (var entry in tiles){
            // Goes through every tile
            Tile tile = entry.Value;
            
            // Sets the expansion chance
            float expandChance = 0.02f;

            // If the tile is a frontier and if it has an owner
            if (tile.frontier && tile.state != null || tile.anarchy){
                // Goes through its borders
                for (int xd = -1; xd <= 1; xd++){

                    for (int yd = -1; yd <= 1; yd++){
                        if (yd != 0 && xd != 0){
                            continue;
                        }
                        // Does a first random check to save performance
                        if ((Random.Range(0f, 1f) < expandChance || tile.coastal && Random.Range(0f, 1f) < expandChance * 4f) && tile.population > 0){
                            // Gets the tilemap pos of this adjacent tile
                            Vector3Int pos = new Vector3Int(xd,yd) + entry.Key;
                            // Checks if the tile even exists
                            if (tiles.ContainsKey(pos)){
                                Tile target = getTile(pos);

                                bool isState = tile.state != null;
                                // Checks if we can expand (Random)
                                bool canExpand = Random.Range(0f, 1f) < target.fertility;

                                bool anarchy = target.anarchy;
                                // Checks if the tile we want to expand to is claimable (If it is neutral and if it has suitable terrain)
                                bool claimable = target.claimable && target.state == null;
                                // If both of these are true
                                if (claimable){
                                    // If the tile isnt yet anarchic
                                    // if (!anarchy && canExpand){
                                    //     if (tile.state != null){
                                    //         // Uhh, controlled anarchy?
                                    //         addAnarchy(pos);
                                    //     }
                                    // }
                                    if (anarchy && isState && Random.Range(0f, 1f) < anarchyConquestChance * target.fertility){
                                        // COLONIALISM!!!!!!!!!!!!!!
                                        tile.state.AddTile(pos);
                                    }
                                }
                            }
                        }   
                    }
                }
            }
        }
    }

    public void addAnarchy(Vector3Int pos){
        if (tiles.ContainsKey(pos)){
            Tile tile = getTile(pos);
            // If the tile doesnt have anarchy add anarchy
            if (!tile.anarchy){
                tile.anarchy = true;
                updateColor(pos);
                anarchy.Add(tile);
            }
        }
    }

    public void RemoveAnarchy(Vector3Int pos){
        if (tiles.ContainsKey(pos)){
            Tile tile = getTile(pos);
            // If the tile doesnt have anarchy add anarchy
            if (tile.anarchy){
                tile.anarchy = false;
                updateColor(pos);
                anarchy.Remove(tile);
            }
        }
    }

    void createRandomState(Vector3Int pos){
        State newState = State.CreateRandomState();
        // Adds it to the nations list
        if (!states.Contains(newState)){
            states.Add(newState);
        }
        // Sets the parent of the nation to the nationholder object
        newState.tileManager = this;
        // And adds the very first tile :D
        newState.AddTile(pos);
        // Connects the tile to ticks
        Events.stateTick += newState.Tick;
    }

    public void Border(Vector3Int position){
        // Gets a tile
        Tile tile = getTile(position);
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
                    Vector3Int pos = new Vector3Int(xd,yd) + position;
                    // Makes sure that tile exists
                    if (getTile(pos) != null && (getTile(pos).state != tile.state)){
                        // If it does and it doesnt have the same owner as us, makes this tile a border :D
                        // But wait, theres more!
                        tile.border = true;

                        if (tile.state != null && getTile(pos).state != tile.state && getTile(pos).state != null){
                            tile.nationalBorder = true;

                            //var nation = tile.owner;
                            var borderState = getTile(pos).state;

                            if (!tile.borderingStates.Contains(borderState)){
                                tile.borderingStates.Add(borderState);
                            }
                        }

                        if (getTile(pos).state == null){
                            // If the tested border is neutral
                            if (getTile(pos).claimable){
                                // Makes it a frontier
                                // Frontier tiles are the only ones that can colonize neutral tiles
                                tile.frontier = true;
                            }
                        }
                        // Updates the color (For border shading)
                        updateColor(position);
                    }
                }
            }
            // Updates the color (In case the tile no longer has special properties :<)
            updateColor(position);
            
        } else {
            return;
        }
    }

    public void updateBorders(Vector3Int position){
        // Gets the tile at a position
        Tile tile = getTile(position);
        if (tile != null){
            // If the tile exists goes through its adjacent tiles
            for (int xd = -1; xd <= 1; xd++){
                    for (int yd = -1; yd <= 1; yd++){
                        // gets the tiles key of this offset
                        Vector3Int pos = new Vector3Int(xd,yd) + position;
                        if (getTile(pos) != null){
                            // Makes that tile check its borders
                            // NOTE: Also runs on self :D
                            Border(pos);
                            if (tile.state != null){
                                if (getTile(pos).state != null){
                                    getTile(pos).state.getBorders();
                                }
                            }
                        }
                    }
                }
        } else {
            return;
        }
    }

    public Tile getTile(Vector3Int position){
        // makes sure the key we are getting exists
        if (tiles.ContainsKey(position)){
            // If it does, returns the tile
            return tiles[position];
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

    public void updateAllColors(bool updateOcean = false){
        // LAGGY
        foreach (var entry in tiles){
            if (entry.Value.biome.terrainType == BiomeTerrainType.WATER && !updateOcean){
                continue;
            }
            // Goes through every tile and updates its color
            updateColor(entry.Key);
        }
    }


    // COLOR
    public void updateColor(Vector3Int position){
        tilemap.SetTileFlags(position, TileFlags.None);
        // Gets the final color
        Color finalColor = new Color();
        // Gets the tile we want to paint
        Tile tile = getTile(position);
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
        
        // Finally sets the color on the tilemap
        tilemap.SetColor(position, finalColor);
        tilemap.SetTileFlags(position, TileFlags.LockColor);
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
        Vector3Int mouseGridPos = tilemap.WorldToCell(globalMousePos);
        if (tiles.ContainsKey(mouseGridPos)){
            if (!overUI){
                // If the mouse isnt over a ui element, gets the tile
                Tile tile = tiles[mouseGridPos];
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
