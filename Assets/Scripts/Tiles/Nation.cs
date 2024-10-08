using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Events;

public class Nation : MonoBehaviour
{
    public string nationName = "New Nation";
    public Color nationColor = Color.red;
    public List<Tile> tiles = new List<Tile>();
    public int population;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void RandomizeNation(){
        nationColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
    }
    public void addTile(Tile tile){
        tiles.Add(tile);
        population += tile.tilePop.population;
    }
    public void removeTile(Tile tile){
        tiles.Remove(tile);
        population -= tile.tilePop.population;
    }
}
