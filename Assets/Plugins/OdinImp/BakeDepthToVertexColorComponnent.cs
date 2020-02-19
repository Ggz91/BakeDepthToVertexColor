using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

public class BakeDepthToVertexColorComponent
{
    [InfoBox("把深度信息映射到网格的定点色上")]
    [MinValue(1)]
    public Vector2Int Size = new Vector2Int(100, 100);

    [MinValue(0.1f)]
    public Vector2 UnitSize = new Vector2(0.1f, 0.1f);
}
