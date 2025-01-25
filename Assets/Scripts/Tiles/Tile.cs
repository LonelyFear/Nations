using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public class Tile
{
    public TileManager tileManager;
    public State state = null;
    public Front front;
    public bool border;
    public bool frontier;
    public bool nationalBorder;
    public List<State> borderingStates = new List<State>();
    public Tile[] borderingTiles = new Tile[0];
    public Vector3Int[] borderingPositions = new Vector3Int[0];
    // Population
    public NativeList<Pop> pops = new NativeList<Pop>(Allocator.Persistent);
    public const int maxPops = 50;

    // Stats & Data
    public Pop rulingPop;
    public int population;
    public int maxPopulation;
    public int workforce;
    public Culture rulingCulture;
    public Culture majorityCulture;
    public Tech tech;
    public float development;
    public Vector3Int tilePos;
    public bool coastal;
    public bool anarchy;

    // Terrain Stats
    public bool claimable;
    public Biome biome;
    public Color terrainColor;
    public float altitude;
    public float temperature;
    public float moisture;
    public float fertility;

    public void Tick(){
        GetMaxPopulation();
        if (population > 600){
            float developmentIncrease = (population + 0.001f) / 1000000f;
            //Debug.Log(developmentIncrease);
            development += developmentIncrease;
            if (development > 1){
                development = 1f;
            }
            //Debug.Log(development);
        };
        if (population >= 50 && state == null && !anarchy){ 
            tileManager.addAnarchy(tilePos);
        } else if (population < 50 && anarchy){
            tileManager.RemoveAnarchy(tilePos);
        }

        if (biome.terrainType != BiomeTerrainType.WATER){
            //Debug.Log(population);
            for (int i = 0; i < pops.Length; i++){
                Pop pop = pops[i];
                float birthRate = pop.birthRate;
                if (population > maxPopulation){
                    birthRate *= 0.75f;
                }
                if (pop.size < 2){
                    birthRate = 0f;
                }
                float naturalGrowthRate = birthRate - pop.deathRate;
                int totalChange = (int)((float)pop.size * naturalGrowthRate);

                if (UnityEngine.Random.Range(0f, 1f) < pop.size * naturalGrowthRate % 1){
                    totalChange += 1;
                }

                ChangePopulation(totalChange);
                pop.size += totalChange;
                pops[i] = pop;
            }            
        }
    }

    public static Color Hetx2RGB(string hex)
    {
        Color newColor = Color.white;

        //Make sure we dont have any alpha values
        if (hex.Length != 6)
        {
            return newColor;
        }

        var hexRed = int.Parse(hex[0].ToString() + hex[1].ToString(),
        System.Globalization.NumberStyles.HexNumber);

        var hexGreen = int.Parse(hex[2].ToString() + hex[3].ToString(),
        System.Globalization.NumberStyles.HexNumber);

        var hexBlue = int.Parse(hex[4].ToString() + hex[5].ToString(),
        System.Globalization.NumberStyles.HexNumber);


        newColor = new Color(hexRed / 255f, hexGreen / 255f, hexBlue / 255f);

        
        return newColor;

    }

    public void GetMaxPopulation(){
        // 10k times the fertility is the maximum population a tile can support
        maxPopulation = Mathf.RoundToInt(10000 * fertility);
    }


    public void ChangePopulation(int amount){
        population += amount;

        if (state != null){
            // Updates our state
            state.ChangePopulation(amount);
        }

        if (tileManager.mapMode == TileManager.MapModes.POPULATION){
            UpdateColor();
        }
    }

    public void ChangeWorkforce(int amount){
        workforce += amount;
        if (state != null){
            state.workforce += amount;
        }
    }

    public void UpdateColor(){
        tileManager.updateColor(tilePos);
    }

   public void CreatePop(int amount, Culture culture, Tech tech = new Tech(), float workforceRatio = 0.25f){
        //Debug.Log(amount);
        Pop pop = new Pop(){
            size = amount,
            dependents = amount - Mathf.RoundToInt((float)amount * workforceRatio),
            workforce = Mathf.RoundToInt((float)amount * workforceRatio),
            birthRate = 0.04f / TimeManager.ticksPerYear,
            deathRate = 0.036f / TimeManager.ticksPerYear,
            culture = culture,
            tech = tech
        };
        pops.Add(pop);
        ChangePopulation(amount);

        Debug.Log(amount);
    }
}