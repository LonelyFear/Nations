using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using Random = UnityEngine.Random;

public class State
{
    public string stateName { get; private set; } = "New State";
    public string govtName { get; private set; } = "State";
    public Tile capital { get; private set; }
    public List<Tile> tiles { get; private set; } = new List<Tile>();
    public int population;
    public int totalPopulation;
    public int workforce;
    public int manpower;

    public TileManager tileManager;
    public Color stateColor = Color.red;
    public Color capitalColor = Color.red;
    public Color mapColor = Color.red;

    // Vassalage & Relations
    public State liege;
    public Dictionary<State, StateTypes> vassals = new Dictionary<State, StateTypes>();
    public Dictionary<State, Relation> relations = new Dictionary<State, Relation>();
    // StateType is us them
    // Eg. Us <- Liege: Vassal
    // Liege -> Us: Independent
    public StateTypes stateType = StateTypes.INDEPENDENT;
    public enum StateTypes {
        INDEPENDENT,
        PROVINCE,
        COLONY,
        PUPPET
    }

    public List<State> borderingStates { get; private set; } = new List<State>();
    public Dictionary<State, Front> fronts = new Dictionary<State, Front>();

    // Stats
    public Tech tech = new Tech();
    public Culture culture;
    public Pop rulingPop;

    public bool atWar;
    public bool atTensions;

    public void getBorders(){
        // Clears our saved borders
        borderingStates.Clear();
        // Goes through our tiles
        foreach (Tile tile in tiles){
            // If the tile borders another nation
            if (tile.nationalBorder){
                // Goes through the nations it borders
                foreach (State state in tile.borderingStates){
                    if (!borderingStates.Contains(state)){
                        // If we dont already have it down, adds it to bordering states
                        borderingStates.Add(state);
                        fixRelations();
                    }
                    // Checks if we dont have a front with the state
                    if (!fronts.ContainsKey(state)){
                        // Makes a new front
                        Front newFront = new Front(){
                            targetState = state,
                            state = this
                        };
                        Events.tick += newFront.Tick;

                        fronts.Add(state, newFront);
                        
                    }
                    // Adds tiles to the front
                    Front front = fronts[state];
                    if (!front.tiles.ContainsKey(tile)){
                        front.tiles.Add(tile, 0);
                    }
                }
            }
        }
    }

    public void AddTile(int x, int y){
        Tile tile = tileManager.getTile(x, y);
        if (tiles.Count < 1 && tile.anarchy){
            capital = tile;
            rulingPop = tile.pops[0];
        }
        
        tile.anarchy = false;
        if (tile.state != null){
            tile.state.RemoveTile(x, y);
        }
        tiles.Add(tile);

        ChangePopulation(tile.population);
        workforce += tile.workforce;

        tile.state = this;
        tileManager.anarchy.Remove(tile);
        
        tileManager.updateColor(x, y);
        tileManager.updateBorders(x, y);
    }

    public void RemoveTile(int x, int y){
        Tile tile = tileManager.getTile(x, y);
        if (tiles.Contains(tile)){
            tiles.Remove(tile);

            ChangePopulation(-1 * tile.population);
            workforce -= tile.workforce;

            tile.state = null;
            tile.anarchy = true;

            tileManager.anarchy.Add(tile);

            tileManager.updateColor(x, y);
            tileManager.updateBorders(x, y);
        }
    }
    
    public void VassalizeState(State state, StateTypes type = StateTypes.PUPPET){
        if (type != StateTypes.INDEPENDENT && !vassals.ContainsKey(state) && liege != state && state.liege != this){

            if (state.liege != null){
                state.liege.ReleaseState(state);   
            }

            if (state.vassals.Count > 0){
                foreach (var vassalRelation in state.vassals.ToArray<KeyValuePair<State, StateTypes>>()){
                    state.ReleaseState(vassalRelation.Key);
                }
            }

            state.liege = this;
            vassals.Add(state, type);

            state.CreateRelations(this, type, true);

            state.UpdateStateType();
            state.fixRelations();
            state.updateCapital();
            state.updateColor();
            

            // Give us relations with all our puppets relations
            foreach (var entry in state.relations){
                Relation relation = entry.Value;
                State state1 = entry.Key;
                if (!relations.ContainsKey(state1)){
                    CreateRelations(state1);
                }
            }
            
            totalPopulation += state.population;
        }
    }

    public void CreateRelations(State state, StateTypes type = StateTypes.INDEPENDENT, bool mutual = false){
        // Mutual creates relations both ways
        if (!relations.ContainsKey(state)){
            relations.Add(state, new Relation(){
                relationType = type,
                opinion = 0
            });
        } else if (relations.ContainsKey(state)) {
            relations[state].relationType = type;
        }
        if (mutual){
            state.CreateRelations(this);
        }
    }

    public void UpdateStateType(){
        if (liege != null && relations.ContainsKey(liege)){
            stateType = relations[liege].relationType;
        } else {
            stateType = StateTypes.INDEPENDENT;
        }
    }

    public void ReleaseState(State state){
        if (vassals.ContainsKey(state)){
            state.relations[this].relationType = StateTypes.INDEPENDENT;
            vassals.Remove(state);
            

            state.UpdateStateType();
            state.liege = null;

            state.updateCapital();
            state.updateColor();
            state.fixRelations();

            totalPopulation -= state.population;
        }
    }

    public static State CreateRandomState(){
        State newState = new State(){
            stateColor = new Color(Random.Range(0.3f, 0.8f), Random.Range(0.3f, 0.8f), Random.Range(0.3f, 0.8f)),
            stateName = "New State"
        };
        string[] statePrefixes = System.IO.File.ReadAllLines("Assets/Text Files/NationPrefixes.txt");
        string[] stateSyllables = System.IO.File.ReadAllLines("Assets/Text Files/NationSyllables.txt");
        string[] stateSuffixes = System.IO.File.ReadAllLines("Assets/Text Files/NationSuffixes.txt");
        string[] stateGovernments = System.IO.File.ReadAllLines("Assets/Text Files/TribalGovernments.txt");

        newState.mapColor = newState.stateColor;

        if (stateGovernments.Length > 0){

            string name = "";
            // Adds our prefix
            name += statePrefixes[Random.Range(0, statePrefixes.Length - 1)];
            int syllables = 0;
            while (Random.Range(0f, 1f) < 0.5f && syllables <= 3){
                // Adds syllables
                syllables++;
                name += stateSyllables[Random.Range(0, stateSyllables.Length - 1)];
            }
            // Adds our suffix
            name += stateSuffixes[Random.Range(0, stateSuffixes.Length - 1)];
            // Gets the governmment (Cosmetic for now)
            string govt = stateGovernments[Random.Range(0, stateGovernments.Length - 1)];

            // Sets the value
            newState.stateName = name;
            newState.govtName = govt;
        }
        return newState;
    }

    public void Tick(){
        // Updates our capital every tick
        updateCapital();

        //DebugVassalize();
        if (capital != null){
            UpdateRelations();
        }
        
    }

    void UpdateRelations(){
        foreach (var entry in relations){
            State state = entry.Key;
            Relation relation = entry.Value;
            //relation.percievedThreat = CalcPercievedThreat(state);
            relation.opinion = Mathf.RoundToInt(CalcPercievedThreat(state));
        }
    }

    float CalcPercievedThreat(State state){
        float percievedThreat;

        float armyFactor = ((state.manpower + 0.1f) / (manpower + 0.1f)) - 1f;
        if (state.manpower > manpower){
            armyFactor = (((manpower + 0.1f) / (state.manpower + 0.1f)) - 1f) * -1f;
        }

        float techFactor = ((state.tech.militaryLevel + 0.1f) / (tech.militaryLevel + 0.1f)) - 1f;
        if (state.tech.militaryLevel > tech.militaryLevel){
            armyFactor = (((tech.militaryLevel + 0.1f) / (state.tech.militaryLevel + 0.1f)) - 1f) * -1f;
        }

        float sizeFactor = ((state.tiles.Count + 0.1f) / (tiles.Count + 0.1f)) - 1f;
        if (state.tiles.Count > tiles.Count){
            sizeFactor = (((tiles.Count + 0.1f) / (state.tiles.Count + 0.1f)) - 1f) * -1;
        }

        float distanceFactor = 1f;
        if (!borderingStates.Contains(state)){
            distanceFactor = 1 / (1 + Vector2Int.Distance(state.capital.tilePos, capital.tilePos));
        }
        percievedThreat = ((armyFactor + techFactor + sizeFactor) / 3f) * distanceFactor * 100f;

        return percievedThreat;
    }

    // Makes automatic vassalage of neighbors
    void DebugVassalize(){
        foreach (State state in borderingStates){
            if (relations.ContainsKey(state) && state.liege == null && tiles.Count > state.tiles.Count && tiles.Count > 25 && liege == null){
                VassalizeState(state, StateTypes.PROVINCE);
            }
        }        
    }

    public void fixRelations(){
        foreach (State border in borderingStates){
            if (!relations.ContainsKey(border)){
                relations.Add(border, new Relation());
            }
        }

        foreach (var entry in relations){
            State state = entry.Key;
            Relation relation = entry.Value;

            // Makes relations with liege
            if (liege != null && !relations.ContainsKey(liege) && !liege.relations.ContainsKey(this)){
                Relation newRelation = new Relation(){
                    relationType = stateType,
                };
            }
            // Removes inactive relations
            if (state.tiles.Count < 1){
                if (liege != null && liege == state){
                    relations.Remove(state);
                }
                relations.Remove(state);
            }
        }
    }

    void updateCapital(){
        switch (stateType){
            case StateTypes.INDEPENDENT:
                capitalColor = Color.red;
                break;
            case StateTypes.PROVINCE:
                capitalColor = new Color(0.5f, 0f, 0.5f, 1f);
                break;
            case StateTypes.COLONY:
                capitalColor = Color.blue;
                break;
            case StateTypes.PUPPET:
                capitalColor = Color.yellow;
                break;
        }
        if (capital == null || capital.state != this){
            capital = null;
            if (tiles.Count > 0){
                capital = tiles[Random.Range(0, tiles.Count - 1)];
                tileManager.updateColor(capital.tilePos.x, capital.tilePos.y);
            }
            
        }
    }

    void updateColor(){
        //updateCapital();
        switch (stateType){
            case StateTypes.INDEPENDENT:
                mapColor = stateColor;
                break;
            case StateTypes.PROVINCE:
                mapColor = liege.mapColor;
                break;
            case StateTypes.COLONY:
                mapColor = stateColor * 0.5f + liege.mapColor * 0.5f;;
                break;
            case StateTypes.PUPPET:
                mapColor = stateColor * 0.5f + liege.mapColor * 0.5f;
                break;
        }
        foreach (Tile tile in tiles){
            tileManager.updateColor(tile.tilePos.x, tile.tilePos.y);
        }
    }

    public void ChangePopulation(int amount){
        population += amount;
        totalPopulation += amount;
        if (liege != null){
            liege.totalPopulation += amount;
        }
    }
}
