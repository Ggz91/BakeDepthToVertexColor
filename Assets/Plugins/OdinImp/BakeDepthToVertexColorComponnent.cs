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
    [BoxGroup("单位Patch")]
    [MinValue(1)]
    public Vector2Int Size = new Vector2Int(10, 10);

    [BoxGroup("单位Patch")]
    [MinValue(0.1f)]
    public float UnitSize = 1f;
    [BoxGroup("单位Patch")]
    [MinValue(1)]
    public int MaxQuadWidth = 8;

    [BoxGroup("Rendertexture参数")]
    [MinValue(1)]
    public Vector2Int RTSize = new Vector2Int(1024, 1024);

    [BoxGroup("深度图高度范围")]
    public float Bottom = -1.0f;

    [BoxGroup("深度图高度范围")]
    public float Top = 1.0f;
    [BoxGroup("生成深度图的最高高度")]
    public float SceneMaxHeight = 10f;
    [BoxGroup("海面边缘高度范围")]
    public Vector2 EdgeRange = new Vector2(-0.1f, 0.2f);
    public GameObject OceanObj = null;

    //water层
    [MinValue(1)]
    public int WaterLayerIndex = 4;

    //public Texture2D DepthTexture = null;
    //public Shader DepthRenderShader = null;
    BakeDepthUtil m_bake_depth_util = new BakeDepthUtil();
    GenMeshUtil m_gen_mesh_util = new GenMeshUtil();
    OptimizeMeshUtil m_optimize_mesh_util = new OptimizeMeshUtil();
    BakeDepthParam m_param;
    List<CombineInstance> m_combine_instances = new List<CombineInstance>();

    void AddSubMesh(PatchInfo mesh_info)
    {
        //把更新好的mesh全都合并成一个新的mesh，并赋值给原plane
        CombineInstance combine_instance = new CombineInstance();
        //combine_instance.mesh = mesh_info.Mesh;
        //combine_instance.transform = mesh_info.Transform.localToWorldMatrix;
        
        m_combine_instances.Add(combine_instance);
    }

    void CombineMesh()
    {
        Mesh combined_mesh = new Mesh();
        combined_mesh.name = "CombinedMesh";
        Debug.Log(" combine sub mesh count : " + m_combine_instances.Count.ToString());
        combined_mesh.CombineMeshes(m_combine_instances.ToArray());
        OceanObj.GetComponent<MeshFilter>().mesh = combined_mesh;
        //销毁掉sub mesh 的gameobject,都是挂在plane下面
        while(0 < OceanObj.transform.childCount)
        {
            GameObject.DestroyImmediate(OceanObj.transform.GetChild(0).gameObject);
        }
    }

    void OptimizeMesh()
    {
        m_optimize_mesh_util.OptimizeMesh(OceanObj, m_param);
    }

    Vector2Int CalPatchesCount(GameObject obj, in BakeDepthParam param)
    {
        Vector2 total_size = new Vector2(obj.transform.lossyScale.x, obj.transform.lossyScale.z) * CommonData.UnitSize;
        Vector2 patch_size = (Vector2)(param.Size) * param.UnitSize;
        return new Vector2Int(Mathf.CeilToInt(total_size.x / patch_size.x), Mathf.CeilToInt(total_size.y / patch_size.y));
    }
    void SetColorerMesh(Mesh mesh, Color[] cols)
    {
        mesh.SetColors(cols);
        mesh.name = "ColoredMesh";

        OceanObj.GetComponent<MeshFilter>().sharedMesh = mesh;
    }
    [Button("生成网格")]
    public void GenDepth()
    {
        //使用unity自带的网格合并，不会合并相同定点，不再划分sub mesh
        FillParam();
        //SaveDepthTexture(m_bake_depth_util.Execute(OceanObj.transform.position));
        //分割mesh信息
        Mesh mesh = m_gen_mesh_util.GenMesh(new Vector2(m_param.UnitSize, m_param.UnitSize), OceanObj);
        List<PatchInfo> sub_patch_info = m_gen_mesh_util.DividePatch(OceanObj, m_param, mesh);
        
        m_combine_instances.Clear();
        Color[] cols = new Color[mesh.vertexCount];
        for(int i=0; i<sub_patch_info.Count; ++i)
        {
            
            //根据mesh信息烘焙
            RenderTexture rt = m_bake_depth_util.Execute(sub_patch_info[i].MeshPos + new Vector3(0, SceneMaxHeight, 0));
            SaveDepthTexture(rt);
            //更新定点色
            m_gen_mesh_util.MapRTToVertexColor(rt, sub_patch_info[i], in m_param, mesh, cols);
            //添加sub mesh
            //AddSubMesh(sub_mesh_info[i]);
        }
        //合并为一个大的mesh
        //CombineMesh();
        SetColorerMesh(mesh, cols);
        //网格优化，减少面片数和定点数
        OptimizeMesh();
        
    }

    void SaveDepthTexture(RenderTexture rt)
    {
        RenderTexture before = RenderTexture.active;
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
        RenderTexture.active = before;
    }
    void FillParam()
    {
        m_param.Size = Size;
        m_param.UnitSize = UnitSize;
        //m_param.DepthShader = DepthRenderShader;
        m_param.Bottom = Bottom;
        m_param.Top = Top;
        m_param.RTSize = RTSize;
        m_param.EdgeRange = EdgeRange;
        m_param.WaterLayerIndex = WaterLayerIndex;
        m_param.MaxQuadWidth = MaxQuadWidth;
        m_bake_depth_util.InitParam(m_param);
    }
    public void Enter()
    {
        Debug.ClearDeveloperConsole();
        m_bake_depth_util.Enter();
    }
    public void Leave()
    {
        m_bake_depth_util.CleanUp();
    }
}
