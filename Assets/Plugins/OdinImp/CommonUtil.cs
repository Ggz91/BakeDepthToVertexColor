using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct CommonData
{
    //默认单模型的mesh size，lossy scale为1的情况
    public const int UnitSize = 10;
}

public struct BakeDepthParam
{
    public Vector2Int Size;
    public float UnitSize;
    //public Shader DepthShader;
    public float Bottom;
    public float Top;
    public Vector2 EdgeRange;
    public Vector2Int RTSize;
    public int WaterLayerIndex;
}