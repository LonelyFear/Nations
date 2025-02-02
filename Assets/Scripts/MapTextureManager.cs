using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MapTextureManager : MonoBehaviour
{
    public Vector2Int imageSize;
    public GenerateWorld generator;
    RawImage image;

    Texture2D texture;
    float[,] heights;
    void Awake(){
        GetComponent<RectTransform>().sizeDelta = (Vector2)generator.worldSize;
        float scale = 1920 / generator.worldSize.x;

        GetComponent<RectTransform>().localScale = new Vector3(scale, scale, 1f);

        image = GetComponent<RawImage>();
        int imgX = Mathf.RoundToInt(image.GetComponent<RectTransform>().sizeDelta.x);
        int imgY = Mathf.RoundToInt(image.GetComponent<RectTransform>().sizeDelta.y);
        imageSize = new Vector2Int(imgX, imgY);
        InitializeTexture();
        //heights = Noise.GenerateNoiseMap(imageSize.x, imageSize.y, 20, generator.totalNoiseScale, 8, 0.5f, 2f);
    }
    
    void Update(){
        // heights = Noise.GenerateNoiseMap(imageSize.x, imageSize.y, generator.noiseSeed, generator.totalNoiseScale, 8, generator.persistence, generator.lacunarity);
        // for (int y = 0; y < imageSize.y; y++){
        //     for (int x = 0; x < imageSize.x; x++){
        //         //print(heights[x,y]);
        //         SetPixelColor(x, y, Color.Lerp(Color.black, Color.white, heights[x,y]));
        //     }
        // }            
    }

    public void SetPixelColor(int x, int y, Color color){
        texture.SetPixel(x,y, color);
        texture.Apply();
        image.texture = texture;
    }
    public void SetPixelColor(Vector2Int pos, Color color){
        texture.SetPixel(pos.x, pos.y, color);
        texture.Apply();
        image.texture = texture;
    }
    void InitializeTexture(){
        texture = new Texture2D(imageSize.x, imageSize.y);
        texture.filterMode = FilterMode.Point;

        for (int y = 0; y < imageSize.x; y++){
            for (int x = 0; x < imageSize.x; x++){
                texture.SetPixel(x, y, new Color(1f, 1f, 1f));
            }
        }
        texture.Apply();
        image.texture = texture;
    }
}
