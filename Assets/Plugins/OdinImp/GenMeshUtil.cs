using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MeshInfo
{
    public Mesh Mesh;
    public Vector3 MeshPos;
    public Transform Transform;
}

public class GenMeshUtil
{

    Mesh GenTemplateMesh(Vector2 unit_size, BakeDepthParam param)
    {
        Mesh tmplate_mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();
        Vector2Int index = Vector2Int.zero;
        for(int i=-param.Size.x; i<=param.Size.x; ++i, ++index.x)
        {
            index.y = 0;
            for(int j=-param.Size.y; j<=param.Size.y; ++j, ++index.y)
            {
                Vector2 offset = new Vector2(i, j) * param.UnitSize;
                vertices.Add(new Vector3(offset.x, 0, offset.y));
                uv.Add(new Vector2(index.x * 0.5f/param.Size.x, index.y*0.5f/param.Size.y));
                normals.Add(new Vector3(0, 1, 0));
                Debug.Log("[GenTemplateMesh Vertex] index : " + offset.ToString() + " : " + vertices.ToArray()[vertices.Count-1].ToString());
                //设置三角形索引序列,顺时针生成
                //每个定点对应左边的一个倒三角跟右边的一个正三角
                if(param.Size.y == j)
                {
                    continue;
                }
                if(-param.Size.x != i)
                {
                    int index_0 = index.y * (2 * param.Size.x + 1)+ index.x;
                    int index_2 = (index.y + 1) * (2 * param.Size.x + 1) + index.x - 1;
                    int index_1 = (index.y + 1) * (2 * param.Size.x + 1) + index.x;
                    indices.Add(index_0);
                    indices.Add(index_1);
                    indices.Add(index_2);
                    Debug.Log("[GenTemplateMesh] index :" + index.ToString() + "  " + index_0.ToString() + " " + index_1.ToString() + " " + index_2.ToString());
                }
                if(param.Size.x != i)
                {
                    int index_0 = index.y * (2 * param.Size.x + 1) + index.x;
                    int index_2 = (index.y + 1) * (2 * param.Size.x + 1) + index.x;
                    int index_1 = index_0 + 1;
                    indices.Add(index_0);
                    indices.Add(index_1);
                    indices.Add(index_2);
                    Debug.Log("[GenTemplateMesh] index :" + index.ToString() + "  " + index_0.ToString() + " " + index_1.ToString() + " " + index_2.ToString());
                }
                
            }
            
        }

        tmplate_mesh.SetVertices(vertices);
        tmplate_mesh.SetUVs(0, uv);
        tmplate_mesh.SetNormals(normals);
        tmplate_mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        tmplate_mesh.RecalculateBounds();
        return tmplate_mesh;
    }

    public List<MeshInfo> DivideSubMesh(GameObject plane, BakeDepthParam param)
    {
        //根据plane的大小划分成多个不同的网格，这里要通过参数设置保证plane的大小是设置的Patch的整数倍
        // 1单位的Scale 为 5
        const float UnitSize = 5;
        Vector2 total_size = new Vector2(0f, 0f);
        total_size.x = plane.transform.lossyScale.x * UnitSize;
        total_size.y = plane.transform.lossyScale.z * UnitSize;
        
        Vector2 unit_size = new Vector2(param.Size.x * param.UnitSize, param.Size.y * param.UnitSize);
        Vector2Int total_num = new Vector2Int(Mathf.CeilToInt(total_size.x / unit_size.x), Mathf.CeilToInt(total_size.y / unit_size.y));
        List<MeshInfo> meshes = new List<MeshInfo>();
        Mesh template_mesh = GenTemplateMesh(unit_size, param);
        for(int i=0; i<total_num.x; ++i)
        {
            for(int j=0; j<total_num.y; ++j)
            {
                Vector2 offset = new Vector2(i, j);
                meshes.Add(GenSingleMeshInfo(offset, unit_size, template_mesh, plane));
            }
        }
        Debug.Log("[Divide Sub Mesh] lossyscale : " + plane.transform.lossyScale.ToString() 
        + ", total size : " + total_size.ToString()
        + ", unit size : " + unit_size.ToString()
        + ", total num : " + total_num.ToString());
        Debug.Log("[GenMeshUtil] Divide Mesh Done");
        return meshes;
    }

    MeshInfo GenSingleMeshInfo(Vector2 offset, Vector2 unit_size, Mesh template_mesh, GameObject parent)
    {
        GameObject mesh_obj = new GameObject();
        mesh_obj.AddComponent<MeshFilter>();
        mesh_obj.GetComponent<MeshFilter>().sharedMesh = new Mesh();

        MeshInfo mesh_info = new MeshInfo();
        Vector2 imp_offset_v2 = offset * unit_size;
        Vector3 imp_offset_v3 = new Vector3(imp_offset_v2.x, 0, imp_offset_v2.y);
        
        mesh_info.Mesh = mesh_obj.GetComponent<MeshFilter>().sharedMesh;
        mesh_info.Mesh.SetVertices(new List<Vector3>(template_mesh.vertices));
        mesh_info.Mesh.SetNormals(template_mesh.normals);
        mesh_info.Mesh.SetIndices(template_mesh.GetIndices(0), MeshTopology.Triangles, 0);
        mesh_info.Mesh.SetUVs(0, new List<Vector2>(template_mesh.uv));
        mesh_info.Mesh.RecalculateBounds();

        mesh_obj.transform.position += imp_offset_v3;
        
        mesh_info.MeshPos = imp_offset_v3;
        mesh_info.Transform = mesh_obj.transform;
        mesh_info.Transform.parent = parent.transform;
        return mesh_info;
    }

    public void MapRTToVertexColor(RenderTexture rt, MeshInfo mesh_info, in BakeDepthParam param)
    {
        //获取像素
        RenderTexture before = RenderTexture.active;
        Texture2D tex_2d = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex_2d.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex_2d.Apply();
        Color[] cols = new Color[mesh_info.Mesh.vertexCount];
        
        //更新到定点色
        for(int i=0; i<mesh_info.Mesh.vertexCount; ++i)
        {
            int x = Mathf.FloorToInt(mesh_info.Mesh.uv[i].x * rt.width);
            int y = Mathf.FloorToInt(mesh_info.Mesh.uv[i].y * rt.height);
            cols[i] = tex_2d.GetPixel(x, y);
            Debug.Log("[MapVertexColor] index : (" + x.ToString() + ", " + y.ToString() +"). Vertex Color : " + cols[i].ToString());
        }
        mesh_info.Mesh.SetColors(cols);
        RenderTexture.active = before;
        Debug.Log("[GenMeshUtil] Map Mesh Vetex Color Done");
    }
}
