using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PatchInfo
{
    public Vector3 MeshPos;
    public List<Vector2> UVs;
    public List<int> Indices;

}

public class GenMeshUtil
{

    Mesh GenTemplateMesh(Vector2 unit_size, BakeDepthParam param, GameObject plane)
    {
        Mesh tmplate_mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();
        Vector2Int index = Vector2Int.zero;
        Vector3 scale = plane.transform.lossyScale;
        //锚点设置到中间
        param.Size /= 2;
        for(int i=-param.Size.x; i<=param.Size.x; ++i, ++index.x)
        {
            index.y = 0;
            for(int j=-param.Size.y; j<=param.Size.y; ++j, ++index.y)
            {
                Vector2 offset = new Vector2(i, j) * param.UnitSize;
                vertices.Add(new Vector3(offset.x / scale.x , 0, offset.y / scale.z));
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

    public List<PatchInfo> DividePatch(GameObject plane, BakeDepthParam param)
    {
        //根据plane的大小划分成多个不同的网格，这里要通过参数设置保证plane的大小是设置的Patch的整数倍
        // 1单位的Scale 为 10，
        Vector2 total_size = new Vector2(0f, 0f);
        total_size.x = plane.transform.lossyScale.x * CommonData.UnitSize;
        total_size.y = plane.transform.lossyScale.z * CommonData.UnitSize;
        
        Vector2 unit_size = new Vector2(param.Size.x * param.UnitSize, param.Size.y * param.UnitSize);
        Vector2Int total_num = new Vector2Int(Mathf.CeilToInt(total_size.x / unit_size.x), Mathf.CeilToInt(total_size.y / unit_size.y));
        List<PatchInfo> meshes = new List<PatchInfo>();
        //根据原有位置和目标的圆心位置，得到一个中心平移的向量
        Vector3 center_offset_dir = new Vector3(unit_size.x/2, 0, unit_size.y/2) - new Vector3(total_size.x/2, 0, total_size.y/2);
        center_offset_dir.x /= plane.transform.lossyScale.x;
        center_offset_dir.z /= plane.transform.lossyScale.z;
        Vector2Int patch_vertex_size = new Vector2Int(param.Size.x, param.Size.y);
        Vector2Int total_vertex_size = new Vector2Int(Mathf.CeilToInt(total_size.x / param.UnitSize) + 1, Mathf.CeilToInt(total_size.y / param.UnitSize) + 1);
        for(int i=0; i<total_num.x; ++i)
        {
            for(int j=0; j<total_num.y; ++j)
            {
                //需要中心对称
                Vector2Int offset_index = new Vector2Int(i, j);
                meshes.Add(GenPatchInfo(offset_index, unit_size, plane, center_offset_dir, patch_vertex_size, total_vertex_size));
            }
        }
        Debug.Log("[Divide Sub Mesh] lossyscale : " + plane.transform.lossyScale.ToString() 
        + ", total size : " + total_size.ToString()
        + ", unit size : " + unit_size.ToString()
        + ", total num : " + total_num.ToString());
        Debug.Log("[GenMeshUtil] Divide Mesh Done");
        return meshes;
    }

    PatchInfo GenPatchInfo(Vector2Int offset_index, Vector2 unit_size, GameObject parent, Vector3 center_offset_dir, Vector2Int patch_vertex_size, Vector2Int total_vertex_size)
    {
        Mesh mesh = parent.GetComponent<MeshFilter>().sharedMesh;
        PatchInfo mesh_info = new PatchInfo();
        Vector3 scale = parent.transform.lossyScale;
        Vector2 imp_offset_v2 = offset_index * unit_size;
        imp_offset_v2.x /= scale.x;
        imp_offset_v2.y /= scale.z;
        Vector3 imp_offset_v3 = parent.transform.position + new Vector3(imp_offset_v2.x, 0, imp_offset_v2.y) + center_offset_dir;
        mesh_info.MeshPos = imp_offset_v3;
        mesh_info.Indices = new List<int>();
        mesh_info.UVs = new List<Vector2>();
        Vector2Int origin_pos = new Vector2Int(offset_index.x * patch_vertex_size.x, offset_index.y * patch_vertex_size.y);
        //把这个patch包含的顶点信息取出来
        for(int i = 0; i <= patch_vertex_size.x; ++i)
        {
            for(int j = 0; j <= patch_vertex_size.y; ++j)
            {
                //mesh的顶点是列优先
                int index_x = origin_pos.x + i;
                int index_y = origin_pos.y + j;
                int index = index_x * total_vertex_size.y + index_y;
                mesh_info.UVs.Add(mesh.uv[index]);
                mesh_info.Indices.Add(index);
                Debug.Log("[GenMeshUtil] GenPatchInfo cor : " + new Vector2Int(index_x, index_y).ToString() + " index : " + index.ToString());
            }
        }
        return mesh_info;
    }

    public void MapRTToVertexColor(RenderTexture rt, PatchInfo mesh_info, in BakeDepthParam param, Mesh mesh,Color[] cols)
    {
        //获取像素
        RenderTexture before = RenderTexture.active;
        Texture2D tex_2d = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex_2d.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex_2d.Apply();
        
        //更新到定点色
        for(int i=0; i<mesh_info.Indices.Count; ++i)
        {
            int x = Mathf.FloorToInt(mesh_info.UVs[i].x * rt.width);
            int y = Mathf.FloorToInt(mesh_info.UVs[i].y * rt.height);
            int index = mesh_info.Indices[i];
            cols[index] = tex_2d.GetPixel(x, y);
            Debug.Log("[MapVertexColor] index : (" + x.ToString() + ", " + y.ToString() +"). Vertex Color : " + cols[index].ToString());
        }
        RenderTexture.active = before;
        Debug.Log("[GenMeshUtil] Map Mesh Vetex Color Done");
    }
}
