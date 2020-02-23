using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    主要做的优化：
    1、 在水平面以下的面片去掉
    2、 非海边的面片合并成大的面片

    主要实现的算法：
    进行四叉树合并；
    原始的节点均为size最小的叶子节点；
    海边的面片设置成dirty，不可进行合并；
    非边缘的面片节点可以按照四叉树进行合并成大的面片；

    合并的具体做法：
    0、设置一个记录当前定点取下一个定点的step值的容器，如果step大于1，表示当前的定点出于一个已经合并的quad中，直接加step到下一个定点，否则跳到1；
    1、判断当前的quad中的四个点定点色是否都是在海边判断范围内，是的话，找下一个点，不是的话，转到2；
    2、查找相邻的quad，判断quad是否能合并，如果能则继续这一条，不能的话转到3；
    3、更新记录step的容器，然后调到0。
    
    为了后续减掉顶点，在合并过程中，还需要对所有减掉的定点index进行记录，等所有格子合并完成之后，对所有的顶点进行压缩挪位。
    
*/
public class OptimizeMeshUtil
{
    #region var

    enum QuadState
    {
        EQS_Normal,     //普通quad 可以合并
        EQS_Dirty,      //dirty quad 不能合并
        EQS_Clip,       //需要裁减掉
    }

    [System.Flags]
    enum QuadVertexUpdateType
    {
        EQVUT_NO_OP,        //不更新clip和step
        EQVUT_CLIP,         //更新clip
        EQVUT_STEP,         //更新step
    }

    int[] m_step_arr;
    bool[] m_clip_arr;

    int m_width = 0;
    int m_quad_tree_width = 0;
    int m_height = 0;

    Vector3[] m_vertiecs;
    Color[] m_colors;
    int[] m_indices;
    Vector2[] m_uvs;
    const int m_max_step = 1 << 3;
    BakeDepthParam m_param;

    #endregion

    #region  method
    void Init(GameObject obj, BakeDepthParam param)
    {
        m_param = param;
        Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
        const float unit_size = 10;
        m_step_arr = new int[mesh.vertexCount];
        m_clip_arr = new bool[mesh.vertexCount];
        m_width = (int)(unit_size * obj.transform.lossyScale.x / m_param.UnitSize) + 1;
        m_height = (int)(unit_size * obj.transform.lossyScale.z / m_param.UnitSize) + 1;
        m_quad_tree_width = Mathf.NextPowerOfTwo(m_width);

        m_vertiecs = mesh.vertices;
        m_indices = mesh.GetIndices(0);
        m_uvs = mesh.uv;
        m_colors = mesh.colors;
        Debug.Log("[OptimizeMesh-Enter] vertex count : " + m_vertiecs.Length.ToString()
        + " uv count : " + m_uvs.Length.ToString()
        + " indices count : " + m_indices.Length.ToString()
        + " color count : " + m_colors.Length.ToString());
    }

    int GetNextStep(int i)
    {
        int step = m_step_arr[i];
        return (0 == step) ? 1 : step;
    }

    QuadState CheckQuadState(int index, int step)
    {
        if(step > 1)
        {
            int divide_index = index;
            for(int i = 1; i <=step * step ; i *= 2)
            {
                QuadState quad_state = CheckQuadState(divide_index, step/2);
                if(QuadState.EQS_Normal != quad_state)
                {
                    return quad_state;
                }
                divide_index += step/2;
            }
        }

        //index 设定了quad左下角的起点，step确定quad的大小
        int quad_vetex_count = 4;
        float min = (m_param.EdgeRange.x - m_param.Bottom) / (m_param.Top - m_param.Bottom);
        float max = (m_param.EdgeRange.x - m_param.Bottom) / (m_param.Top - m_param.Bottom);
        //起点的索引
        int index_x = index % m_width;
        int index_y = index / m_width;
        //quad 4个点的index 
        int[] indices = 
        {
            index,
            index + step,
            (index_y + step) * m_width,
            (index_y + step) * m_width + step,
        };
        for(int i=0; i < quad_vetex_count; ++i)
        {
            Debug.Log("[OptimizeMesh-CalMaxStep] index : " + indices[i].ToString() + " cor : " + new Vector2Int(index_x, index_y).ToString());
            if((0 == i) && (m_colors[index].r>max))
            {
                return QuadState.EQS_Clip;
            }
            if(m_colors[indices[i]].r >= min && m_colors[indices[i]].r <= max)
            {
                return QuadState.EQS_Dirty;
            }
        }
        return QuadState.EQS_Normal;
    }
    Vector2Int MapQuadTreeCor(int index)
    {
        Vector2Int real_cor = new Vector2Int(index % m_width, index / m_width);
        return new Vector2Int(real_cor.x, real_cor.y * m_quad_tree_width);
    }

    int CalQuadMaxStep(int index)
    {
        //根据在四叉树中的位置，来获取应该step的大小
        Vector2Int quad_cor = MapQuadTreeCor(index);
        Debug.Log("[OptimizeMesh-CalMaxStep] vertex cor : " + quad_cor.ToString());
        int quad_size = 1;
        while(quad_size <= m_max_step)
        {
            quad_size *= 2;
            if(quad_cor.x % quad_size > 0 || quad_cor.y % quad_size > 0)
            {
                return quad_size / 2;
            }
            //不能超过当前的网格范围
            if((quad_cor.x + quad_size) >= m_width || (quad_cor.y + quad_size) >= m_height)
            {
                return quad_size / 2;
            }
        }
        return quad_size;
    }

    QuadVertexUpdateType CalQuadVertexType(int index, int step)
    {
        //只更新z轴上的顶点step，合并quad的时候起点和size都是根据左下角的值进行计算，所以，quad顶边的顶点不进行更新
        //左边两个角的顶点，更新step，不更新clip
        //右边两个角的顶点，不更新step， 不更新clip
        //quad内部的定点，不更新step，更新clip
        //quad右边和上边的非角顶点,不更新step，clip
        //quad左边和底边的非角顶点,不更新step，根据共边的quad更新clip (右边跟上边的这类顶点默认clip掉)
        int index_x = index % step;
        int index_y = index / step;
        if((step - 1 ) == index_y)
        {
            //顶边
            if(0 == index_x)
            {
                return QuadVertexUpdateType.EQVUT_STEP;
            }
            else if((step - 1) == index_x)
            {
                return QuadVertexUpdateType.EQVUT_NO_OP;
            }
            else
            {
                return QuadVertexUpdateType.EQVUT_CLIP;
            }
        }
        else if(0 == index_y)
        {
            //底边
            if(0 == index_x)
            {
                return QuadVertexUpdateType.EQVUT_STEP;
            }
            else if((step - 1) == index_x)
            {
                return QuadVertexUpdateType.EQVUT_NO_OP;
            }
        }
        else if(0 == index_x)
        {
            return QuadVertexUpdateType.EQVUT_STEP;
        }
        else if((step - 1) == index_x)
        {
            return QuadVertexUpdateType.EQVUT_CLIP;
        }
        return QuadVertexUpdateType.EQVUT_NO_OP;
    }

    void UpdateQuadVertices(int index, int step)
    {
        //更新边界的step，和内部的clip
        for(int i = 0; i < step * step; ++i)
        {
            int y = i / step;
            int x = i % step;
            int vetex_index = y * m_width + x + index;
            Debug.Log("[OpitimizeMesh-UpateQuadVertices] index : " + i.ToString() 
            + " cor : " + new Vector2Int(x, y).ToString()
            + " vertex index : " + vetex_index.ToString()
            + " width : " + m_width.ToString()
            + " step : " + step.ToString());
            QuadVertexUpdateType type = CalQuadVertexType(i, step);
            if(type.HasFlag(QuadVertexUpdateType.EQVUT_CLIP))
            {
                m_clip_arr[vetex_index] = true;
            }
            if(type.HasFlag(QuadVertexUpdateType.EQVUT_STEP))
            {
                m_step_arr[vetex_index] = step;
            }
        }
    }

    void CombineQuad()
    {
        //最上面一排不合并
        for(int i=0; i<(m_vertiecs.Length - m_width);)
        {
            int step = GetNextStep(i);
            if(step > 1)
            {
                i += step; 
                continue;
            }

            QuadState quad_state = CheckQuadState(i, 1);        
            if(QuadState.EQS_Clip == quad_state)
            {
                //裁剪掉了
                m_clip_arr[i] = true;
                ++i;
                continue;
            }
            else if(QuadState.EQS_Dirty == quad_state)
            {
                //处于边缘位置，不合并
                ++i;
                continue;
            }

            //需要合并,因为采用四叉树，所以需要将当前的index映射成四叉树的坐标,然后根据在四叉树中的位置，判断能合并到的最大的quad
            int max_step = CalQuadMaxStep(i);
            Debug.Log("[OptimizeMesh-CalMaxStep] index : " + i.ToString() + " max step : " + max_step.ToString());
            int real_max_step = 1;
            for(; real_max_step * 2 <= max_step; real_max_step *= 2)
            {
                //迭代合并quad
                if(QuadState.EQS_Normal != CheckQuadState(i,real_max_step))
                {
                    //当前不能合并
                    break;
                }
            }

            //把当前quad内的顶点进行更新，包括剔除和更新step
            UpdateQuadVertices(i, real_max_step);
            
            ++i;
        }
    }

    void CompressMesh()
    {
        //根据clip的数组来计算数组挪位的一个数组
        int acc_clip = 0;
        List<int> acc_clip_arr = new List<int>();
        
        for(int i=0; i<m_vertiecs.Length; ++i)
        {
            acc_clip_arr.Add(acc_clip);
            if(m_clip_arr[i])
            {
                ++acc_clip;
            }
        }
       
        //把所有的index进行更新
        for(int i=0; i<m_vertiecs.Length; ++i)
        {
            int offset = acc_clip_arr[i];
            //移动vertices、uv和color
            m_vertiecs[i-offset] = m_vertiecs[i];
            m_uvs[i-offset] = m_uvs[i];
            m_colors[i-offset] = m_colors[i];

            //indice除了挪位置，还要更新具体的值
            //先更新具体的值，再挪位置
            int tris_vertex_count = 3;
            int indice_begin_index = 3 * i;
            for(int j=0; j < tris_vertex_count; ++j)
            {
                //更新值
                m_indices[indice_begin_index + j] -= 3 * acc_clip_arr[m_indices[indice_begin_index + j]];
                //移动位置
                m_indices[indice_begin_index + j - 3 * acc_clip_arr[offset]] = m_indices[indice_begin_index + j];
            }
        }

         //截断
        System.Array.Resize(ref m_vertiecs, m_vertiecs.Length - acc_clip);
        System.Array.Resize(ref m_uvs, m_vertiecs.Length);
        System.Array.Resize(ref m_indices, 3 * m_vertiecs.Length);
        System.Array.Resize(ref m_colors, m_vertiecs.Length);
    }

    void UpdateMesh(GameObject obj)
    {
        Vector3[] normals = obj.GetComponent<MeshFilter>().sharedMesh.normals;
        System.Array.Resize(ref normals, m_vertiecs.Length);
        obj.GetComponent<MeshFilter>().sharedMesh = new Mesh();
        Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
        mesh.SetVertices(m_vertiecs);
        mesh.SetUVs(0, m_uvs);
        mesh.SetIndices(m_indices, MeshTopology.Triangles, 0);
        mesh.SetColors(m_colors);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
    }

    public void OptimizeMesh(GameObject obj, BakeDepthParam param)
    {
        //初始化
        Init(obj, param);

        //合并格子
        CombineQuad();

        //压缩顶点
        CompressMesh();

        //把计算后的值更新给obj
        UpdateMesh(obj);
    }
    #endregion
}
