using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine.EventSystems;
using Unity.VisualScripting;
using System.Linq;

public class TileManager : MonoBehaviour
{
    public Tilemap tilemap;
    public NationPanel nationPanel;
    public Dictionary<Vector3Int, Tile> tiles = new Dictionary<Vector3Int, Tile>();
    public List<State> states = new List<State>();
    GenerateWorld world;
    public void Init(){
        // Gets our world generation script
        world = GetComponent<GenerateWorld>();
        // Goes thru the tiles
        foreach (var entry in tiles){
            // Sets their initial color
            //updateColor(entry.Key);
            // Sets their tile positions
            entry.Value.tilePos = entry.Key;

            // Initializes their populations
            initPopulation(entry.Value);
        }
        // Adds random nations to populate our world :>
        //addRandomNations(startingNationCount);
        addInitialAnarchy(100);
        updateAllColors();
    }

    void addInitialAnarchy(int amount){
        int totalAnarchy = 0;
        
        for (int i = 0; i < amount; i++) {
            int attempts = 300;
            // gets the tile
            Tile tile = getRandomTile();

            bool underpopulated = tile.totalPopulation > 500;
            while (!tile.terrain.biome.claimable || tile.anarchy || !tile.coastal || tile.totalPopulation < 500){
                tile = null;
                attempts--;
                if (attempts < 0){
                    break;
                }
                tile = getRandomTile();
                
            }

            if (tile != null){
                addAnarchy(tile.tilePos);
                totalAnarchy++;
                for (int x = -3; x <= 3; x++){
                    for (int y = -3; y <= 3; y++){
                        Vector3Int newAnarchyPos = new Vector3Int(tile.tilePos.x + x, tile.tilePos.y + y);
                        Tile tile1 = getTile(newAnarchyPos);
                        if (tile1 != null && tile1.terrain.biome.claimable && !tile1.terrain.biome.water && Random.Range(0f, 1f) < 0.5f && !underpopulated){
                            addAnarchy(newAnarchyPos);
                        }
                    }
                }
            }
        }

    }

    Tile getRandomTile(){
        return tiles.Values.ElementAt(Random.Range(0, tiles.Keys.Count - 1));
    }
    public void OnTick(){
        // Each month nations can expand into neutral lands
        neutralExpansion();
        tickTiles();
        tickNations();
        creationTick();
    }

    void creationTick(){
        for (int i = 0; i < 20; i++){
            Tile tile = tiles.Values.ElementAt(Random.Range(0, tiles.Keys.Count - 1));
            if (tile.anarchy){
                createRandomState(tile.tilePos);
            }
        }
        
    }
    void tickNations(){
        foreach (State state in states){
            state.OnTick();
        }
    }

    void initPopulation(Tile tile){

        Pop newPop = new Pop(){
            home = tile,
            culture = Culture.createRandomCulture()
        };
        newPop.changePopulation(Mathf.FloorToInt(Random.Range(0, 2000) * tile.terrain.biome.fertility));
        newPop.migrateToTile(newPop.population, tile);
    }

    void tickTiles(){
        foreach (var entry in tiles){
            Tile tile = entry.Value;
            if (tile.totalPopulation > 0 && tile.pops.Count > 0){
                tile.growPopulation();
            }
        }
    }

    public void populationGrowth(){
        foreach (var entry in tiles){
            Tile tile = entry.Value;
            if (tile.totalPopulation > 0 && tile.pops.Count > 0){
                tile.growPopulation();
            }
        }
    }

    public void neutralExpansion(){
        foreach (var entry in tiles){
            // Goes through every tile
            Tile tile = entry.Value;
            
            // Sets the expansion chance
            float expandChance = 0.02f;

            // If the tile is a frontier and if it has an owner
            if (tile.frontier && tile.state != null){
                // Goes through its borders
                for (int xd = -1; xd <= 1; xd++){
                    for (int yd = -1; yd <= 1; yd++){
                        if (yd != 0 && xd != 0){
                            continue;
                        }
                        // Does a first random check to save performance
                        if (Random.Range(0f, 1f) < expandChance || (tile.coastal && Random.Range(0f, 1f) < expandChance * 4f)){
                            // Gets the tilemap pos of this adjacent tile
                            Vector3Int pos = new Vector3Int(xd,yd) + entry.Key;
                            // Checks if the tile even exists
                            if (tiles.ContainsKey(pos)){
                                // Checks if we can expand (Random)
                                bool canExpand = Random.Range(0f, 1f) < getTile(pos).terrain.biome.navigability;

                                bool anarchy = getTile(pos).anarchy;
                                // Checks if the tile we want to expand to is claimable (If it is neutral and if it has suitable terrain)
                                bool claimable = getTile(pos).terrain.biome.claimable && getTile(pos).state == null;
                                // If both of these are true
                                if (claimable){
                                    if (!anarchy && canExpand){
                                        addAnarchy(pos);
                                    } else if (anarchy && tile.totalPopulation >= getTile(pos).totalPopulation * 0.5f){
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

    void addAnarchy(Vector3Int pos){
        if (tiles.ContainsKey(pos)){
            Tile tile = getTile(pos);
            tile.anarchy = true;
            updateColor(pos);
        }
    }
    /*
    public void addRandomNations(int amount){
        // Add random nations
        for (int i = 0; i < amount; i++){
            // For each nation we a want to add
            // Gives 300 attempts to place a nation
            int attempts = 300;
            // Gets the position of the tile we want to put the nation at
            Vector3Int pos = new Vector3Int(Random.Range(0, world.worldSize.x), Random.Range(0, world.worldSize.y));
            // gets the tile
            Tile nationTile = getTile(pos);
            bool openArea = true;
            for (int x = -4; x <= 4; x++){
                for (int y = -4; y <= 4; y++){
                    Tile check = getTile(new Vector3Int(nationTile.tilePos.x + x, nationTile.tilePos.y + y));
                    if (check != null && check.state != null){
                        openArea = false;
                    }
                }
            }
            bool canSpawn = nationTile.coastal && Random.Range(0f,1f) < 0.3f;

            while (nationTile == null || !nationTile.terrain.biome.claimable || nationTile.terrain.biome.fertility < 0.5f || nationTile.totalPopulation < 500 || !openArea || (!nationTile.coastal && !canSpawn && openArea)){
                // If the tile doesnt exist or if it is owned or if it just cant be claimed
                // Picks a new position
                pos = new Vector3Int(Random.Range(0, world.worldSize.x), Random.Range(0, world.worldSize.y));
                // And a new tile
                nationTile = getTile(pos);
                // And reduces the attempts by 1 (To prevent infinite loops that crash the game :) )
                attempts--;

                if (attempts <= 0){
                    // If the attempts run out
                    print("Attempts ran out");
                    nationTile = null;
                    // Breaks the loop and prevents a nation from spawning
                    break;
                }
                
            }
            // If the nation wasnt stopped from spawning
            if (nationTile != null){
                createRandomState(pos);
            }
                
        }
    }
    */

    void createRandomState(Vector3Int pos){
        State newState = State.CreateRandomState();
        // Adds it to the nations list
        if (!states.Contains(newState)){
            states.Add(newState);
        }
        // Sets the parent of the nation to the nationholder object
        //newNation.transform.SetParent(GameObject.FindGameObjectWithTag("NationHolder").transform);
        newState.tileManager = this;
        // And adds the very first tile :D
        newState.AddTile(pos);
    }

    public void updateAllColors(){
        // LAGGY
        foreach (var entry in tiles){
            // Goes through every tile and updates its color
            updateColor(entry.Key);
        }
    }

    public void updateColor(Vector3Int position){
        tilemap.SetTileFlags(position, TileFlags.None);
        // Gets the final color
        Color finalColor = new Color();
        // Gets the tile we want to paint
        Tile tile = getTile(position);

        if (tile.state != null){
            // If the tile has an owner, colors it its nation
            finalColor = tile.state.stateColor;
            // If the tile is a border
            if (tile.border){
                // Colors it slightly darker to show where nation boundaries are
                finalColor = tile.state.stateColor * 0.7f + Color.black * 0.3f;
            }
            if (tile.state.capital == tile){
                finalColor = tile.state.capitalColor;
            }
        } else if (tile.anarchy && tile.terrain.biome.claimable){
            finalColor = Color.black;
        } else {
            // If the tile isnt owned, just sets the color to the color of the terrain
            finalColor = tile.terrain.biome.biomeColor;
        }
        // Higlights selected nation
        if (nationPanel != null && nationPanel.tileSelected != null && nationPanel.tileSelected.state != null){
            // Sets the selected nation to the, selected nation
            State selectedState = nationPanel.tileSelected.state;
            // If the tile isnt the selected nation
            if (tiles[position].state != selectedState){
                // Darkens it
                finalColor = finalColor * 0.5f + Color.black * 0.5f;
            }
        }
        // Finally sets the color on the tilemap
        tilemap.SetColor(position, finalColor);
        tilemap.SetTileFlags(position, TileFlags.LockColor);
    }

    // Updates the tile's border bool
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
                            if (getTile(pos).terrain.biome.claimable){
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

    void Update(){
        if (nationPanel && Input.GetMouseButtonDown(0)){
            // Stops everything from running if there isnt input or if the panel doesnt even exist
            detectTileClick();
        }   
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
