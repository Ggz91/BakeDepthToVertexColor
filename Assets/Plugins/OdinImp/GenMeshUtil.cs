using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MeshInfo
{
    public Mesh Mesh;
    public Vector3 MeshPos;
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
        for(int i=0; i<param.Size.x; ++i)
        {
            for(int j=0; j<param.Size.y; ++j)
            {
                Vector2 offset = new Vector2(i, j) * param.UnitSize;
                vertices.Add(offset);
                uv.Add(new Vector2(i*1.0f/param.Size.x, j*1.0f/param.Size.y));
                normals.Add(new Vector3(0, 1, 0));

                //设置三角形索引序列,顺时针生成
                //每个定点对应左边的一个倒三角跟右边的一个正三角
                if(param.Size.x == (i-1) || param.Size.y == (j-1))
                {
                    continue;
                }
                if(0 != i)
                {
                    int index_0 = j * param.Size.x + i;
                    int index_1 = (j + 1) * param.Size.x + i - 1;
                    int index_2 = (j + 1) * param.Size.x + i;
                    indices.Add(index_0);
                    indices.Add(index_1);
                    indices.Add(index_2);
                }
                if((param.Size.y-1) != j)
                {
                    int index_0 = j * param.Size.x + i;
                    int index_1 = (j + 1) * param.Size.x + i;
                    int index_2 = index_0 + 1;
                    indices.Add(index_0);
                    indices.Add(index_1);
                    indices.Add(index_2);
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
                meshes.Add(GenSingleMeshInfo(offset, unit_size, template_mesh));
            }
        }
        return meshes;
    }

    MeshInfo GenSingleMeshInfo(Vector2 offset, Vector2 unit_size, Mesh template_mesh)
    {
        MeshInfo mesh_info = new MeshInfo();
        List<Vector3> vertices = new List<Vector3>(template_mesh.vertices);
        Vector2 imp_offset_v2 = offset * unit_size;
        Vector3 imp_offset_v3 = new Vector3(imp_offset_v2.x, 0, imp_offset_v2.y);
        //整体加偏移
        for(int i=0; i<vertices.Count; ++i)
        {
            vertices[i] += imp_offset_v3;
        }
        mesh_info.Mesh = new Mesh();
        mesh_info.Mesh.SetVertices(vertices);
        mesh_info.Mesh.SetNormals(template_mesh.normals);
        mesh_info.Mesh.SetIndices(template_mesh.GetIndices(0), MeshTopology.Triangles, 0);
        mesh_info.Mesh.SetUVs(0, new List<Vector2>(template_mesh.uv));
        mesh_info.Mesh.RecalculateBounds();
        mesh_info.MeshPos = imp_offset_v3;
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
        RenderTexture.active = before;

        //更新到定点色
        for(int i=0; i<mesh_info.Mesh.colors.Length; ++i)
        {
            int x = Mathf.FloorToInt(mesh_info.Mesh.uv[i].x * rt.width);
            int y = Mathf.FloorToInt(mesh_info.Mesh.uv[i].y * rt.height);
            mesh_info.Mesh.colors[i] = tex_2d.GetPixel(x, y);
        }
    }
}
