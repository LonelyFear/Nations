using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public struct Pop
{
    // Population
    public int size;
    public int dependents;
    public int workforce;
    public float birthRate;
    public float deathRate;

    // Stats
    public Culture culture;
    public Tech tech;

    // References
    public int index;

    public static bool SimilarPops(Pop a, Pop b){
         if (Tech.CheckSimilarity(a.tech, b.tech) && Culture.CheckSimilarity(a.culture, b.culture)){
             return false;
         }
         return true;
    }
}