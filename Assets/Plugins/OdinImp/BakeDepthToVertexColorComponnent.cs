using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using System.IO;

public class BakeDepthToVertexColorComponent
{
    [InfoBox("把深度信息映射到网格的定点色上")]
    [MinValue(1)]
    public Vector2Int Size = new Vector2Int(1024, 1024);

    [MinValue(0.1f)]
    public float UnitSize = 0.1f;
    [HorizontalGroup("深度图范围")]
    public float Bottom = -5.0f;
    [HorizontalGroup("深度图范围")]
    public float Top = 5.0f;

    public GameObject OceanObj = null;
    //public Texture2D DepthTexture = null;
    public Shader DepthRenderShader = null;
    
    [Button("生成深度图")]
    public void GenDepth()
    {
        BakeDepthParam param;
        param.Size = Size;
        param.UnitSize = UnitSize;
        param.DepthShader = DepthRenderShader;
        param.Bottom = Bottom;
        param.Top = Top;
        SaveDepthTexture(BakeDepthUtil.BakeDepth(OceanObj, param));
    }
    void SaveDepthTexture(RenderTexture rt)
    {
        Texture2D newTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        newTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        newTexture.Apply();
        byte[] bytes = newTexture.EncodeToPNG();
        string filePath = "Assets/Textures/";
        if (bytes != null && bytes.Length > 0)
        {
            if(!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            filePath += "depth.png";
            File.WriteAllBytes(filePath, bytes);
        }
    }
}
