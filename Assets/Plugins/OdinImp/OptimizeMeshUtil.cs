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
    高于海面一定距离的面片被剔除；
    其他的面片节点可以按照四叉树进行合并成大的面片；

    合并的具体做法：
    0、设置一个记录当前定点取下一个定点的step值的容器，如果step大于1，表示当前的定点出于一个已经合并的quad中，直接加step到下一个定点，否则跳到1；
    1、判断当前的quad中的四个点定点色是否都是在海边判断范围内，是的话，找下一个点，不是的话，转到2；
    2、查找相邻的quad，判断quad是否能合并，如果能则继续这一条，不能的话转到3；
    3、更新记录step的容器，然后调到0。
    
    等所有格子合并完成之后，对所有的顶点进行压缩挪位。
    
*/
public class OptimizeMeshUtil
{
    #region var
    enum VertexState
    {
        EQS_Normal,     
        EQS_Dirty,     
        EQS_Clip,      
    }
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
    //int m_quad_tree_width = 0;
    int m_height = 0;

    Vector3[] m_vertiecs;
    Color[] m_colors;
    List<int> m_indices;
    Vector2[] m_uvs;
    const int m_max_step = 1 << 3;
    List<KeyValuePair<int, int>> m_quad_gen_list; 
    BakeDepthParam m_param;
    float m_horizon_height;
    #endregion

    #region  method
    void Init(GameObject obj, BakeDepthParam param)
    {
        m_param = param;
        m_horizon_height = obj.transform.position.y;
        Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
        const float unit_size = 10;
        m_step_arr = new int[mesh.vertexCount];
        m_width = (int)(unit_size * obj.transform.lossyScale.x / m_param.UnitSize) + 1;
        m_height = (int)(unit_size * obj.transform.lossyScale.z / m_param.UnitSize) + 1;
        //m_quad_tree_width = Mathf.NextPowerOfTwo(m_width);

        m_vertiecs = mesh.vertices;
        m_indices = new List<int>();
        m_quad_gen_list = new List<KeyValuePair<int, int>>();
        m_uvs = mesh.uv;
        m_colors = mesh.colors;
        Debug.Log("[OptimizeMesh-Enter] vertex count : " + m_vertiecs.Length.ToString()
        + " uv count : " + m_uvs.Length.ToString()
        + " color count : " + m_colors.Length.ToString());
    }

    int GetNextStep(int i)
    {
        int step = m_step_arr[i];
        return step;
    }
   
    QuadState CheckQuadState(int index, int step, bool affect_by_all_vertex = true)
    {
        if(step > 1)
        {
            int divide_index = index;
            int size = 2;
            for(int i = 0; i < size ; ++i)
            {
                for(int j = 0; j < size; ++j)
                {
                    divide_index = i * step / 2 * m_height + j * step / 2 + index;
                    QuadState quad_state = CheckQuadState(divide_index, step/2, false);
                    if(QuadState.EQS_Normal != quad_state)
                    {
                        return quad_state;
                    }
                }
            }
        }

        //index 设定了quad左下角的起点，step确定quad的大小
        int quad_vetex_count = 4;
        //起点的索引
        int index_x = index % m_width;
        int index_y = index / m_width;

        //quad 4个点的index 
        int[] indices = 
        {
            index,
            index + step,
            (index_y + step) * m_width + index_x,
            (index_y + step) * m_width + step + index_x,
        };
        float range = m_param.Top - m_param.Bottom;
        QuadState state = QuadState.EQS_Normal;
        int clip_count = 0;
        for(int i=0; i < quad_vetex_count; ++i)
        {
            Debug.Log("[OptimizeMesh-CheckQuadState] index : " + indices[i].ToString() 
            + " cor : " + new Vector2Int(index_x, index_y).ToString()
            + " step : " + step.ToString());
            float cur_height = m_colors[indices[i]].r * range + m_param.Bottom;
            float cur_delta = cur_height - m_horizon_height;
            Debug.Log("[OptimizeMesh-CheckQuadState Cal] index : " + indices[i].ToString()
            + " cur_height : " + cur_height.ToString()
            + " cur_delta : " + cur_delta.ToString());
            //判断clip要分情况（取step和判断顶点类型），判断dirty 4个顶点任意一个dirty即dirty
            if((cur_delta > m_param.EdgeRange.y))
            {
                if(affect_by_all_vertex)
                {
                    //判断剔除的时候要4个点都可剔除才剔除
                    ++clip_count;
                }
                else
                {
                    //判断合并的时候，有一个clip就不可合并
                    state = QuadState.EQS_Clip;
                    break;
                }
            }
            if(cur_delta >= m_param.EdgeRange.x && cur_delta <= m_param.EdgeRange.y)
            {
                state = QuadState.EQS_Dirty;
                break;
            }
        }
        if(affect_by_all_vertex && quad_vetex_count == clip_count)
        {
            state = QuadState.EQS_Clip;
        }
        Debug.Log("[OptimizeMesh-CheckQuadState Res] index : " + index.ToString() + " state : " + state.ToString());
        return state;
    }
    Vector2Int MapQuadTreeCor(int index)
    {
        Vector2Int real_cor = new Vector2Int(index % m_width, index / m_width);
        return real_cor;
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
        //底边两个角的顶点，更新step，不更新clip
        //上边两个角的顶点，不更新step，clip=false
        //quad内部的定点，不更新step，更新clip = true
        //quad上边和右边的非角顶点,更新step，clip为ture
        //quad左边和下边的非角顶点,不更新step，根据共边的quad更新clip
        int index_x = index % step;
        int index_y = index / step;
        if((step - 1) == index_y || 0 == index_y)
        {
            //右边/左边
            if((step - 1) == index_x)
            {
                //顶边右/左角顶点
                return QuadVertexUpdateType.EQVUT_NO_OP;
            }
            if(0 == index_x)
            {
                //底边右/左角顶点
                return QuadVertexUpdateType.EQVUT_STEP;
            }
            
            return (step - 1) == index_y ? QuadVertexUpdateType.EQVUT_CLIP : QuadVertexUpdateType.EQVUT_NO_OP;
        }
        else if( (step - 1) == index_x || 0 == index_x )
        {
            return (step - 1) == index_x ? QuadVertexUpdateType.EQVUT_CLIP : QuadVertexUpdateType.EQVUT_STEP;
        }
        //中间点
        return QuadVertexUpdateType.EQVUT_CLIP;
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
            if(type.HasFlag(QuadVertexUpdateType.EQVUT_STEP))
            {
                m_step_arr[vetex_index] = step;
            }
            /*Debug.Log("[OptimizeMesh] updata quad vertices index : " + index.ToString()
            + " step : " + step.ToString()
            + " quad index : " + i.ToString()
            + " type : " + type.ToString());*/
        }

    }
    
    void GenNewQuad(int index, int step)
    {
        //以quad的左下顶点开始，生成2个逆时针拼的quad
        int[] indices = 
        {
            index,
            index + step,
            index + step * m_width,
            index + step * m_width + step,
        };
        //不用clip
        for(int i=0; i<indices.Length; ++i)
        {
           m_clip_arr[indices[i]] = false;
        }
        /*Debug.Log(" [OptimizeMesh] GenNewQuad index : " + index.ToString() 
        + " quad vertex index : " + indices[0].ToString()
        + " " + indices[1].ToString()
        + " " + indices[2].ToString()
        + " " + indices[3].ToString());*/
        //第一个三角形
        m_indices.Add(indices[0]);
        m_indices.Add(indices[1]);
        m_indices.Add(indices[2]);
        //第二个三角形
        m_indices.Add(indices[1]);
        m_indices.Add(indices[3]);
        m_indices.Add(indices[2]);
        Debug.Log("[OptimizeMesh] GenNewQuad index : " + index.ToString()
        + " step : " + step.ToString());
    }
    int CalRealStep(int i)
    {
        //需要合并,因为采用四叉树，所以需要将当前的index映射成四叉树的坐标,然后根据在四叉树中的位置，判断能合并到的最大的quad
        int max_step = CalQuadMaxStep(i);
        //Debug.Log("[OptimizeMesh-CalMaxStep] index : " + i.ToString() + " max step : " + max_step.ToString());
        int real_max_step = 1;
        QuadState state = QuadState.EQS_Normal;
        while(real_max_step <= max_step)
        {
            //迭代合并quad
            QuadState sub_state = CheckQuadState(i, real_max_step);
            if(QuadState.EQS_Normal != sub_state)
            {
                //当前不能合并
                state = sub_state;
                break;
            }
            real_max_step *= 2;
        }
        /*Debug.Log("[OptimizeMeshUtil Quad State Cal Res] index : " + i.ToString()
        + " real_max_step : " + real_max_step.ToString()
        + " state : " + state.ToString());*/
        //clip都是按照最小的size进行
        if(1 == real_max_step)
        {
            if(QuadState.EQS_Clip == state)
            {
                real_max_step = -1;
            }
        }
        else 
        {
            real_max_step /= 2;
        }
        return real_max_step;
    }
    void GenQuadData()
    {
        //最上面一排不合并
        for(int i=0; i<(m_vertiecs.Length - m_width);)
        {
            //最后一列不判断
            if((m_width - 1) == (i % m_width))
            {
                ++i;
                continue;
            }
            int step = GetNextStep(i);
            if(1 <= step)
            {
                i += step;
                continue;
            }
            
            int real_max_step = CalRealStep(i);
            if(-1 == real_max_step)
            {
                //被剔除
                ++i;
                continue;
            }
            //把当前quad内的顶点进行更新，包括剔除和更新step
            UpdateQuadVertices(i, real_max_step);
            RecordQuadData(i, real_max_step);
            //quad的左下角点更新了step
            step = GetNextStep(i);
            i = i + (0 == step ? 1 : step);
        }
    }
    void RecordQuadData(int index, int step)
    {
        m_quad_gen_list.Add(new KeyValuePair<int, int>(index, step));
    }

    void GenQuads()
    {
        //用来记录没有用到后续压缩要clip的节点
        m_clip_arr = new bool[m_vertiecs.Length];
        foreach(var data in m_quad_gen_list)
        {
            GenNewQuad(data.Key, data.Value);
        }
    }
    void CombineQuad()
    {
        //更新相关数据
        GenQuadData();

        //生成quad
        GenQuads();
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
            else
            {
                int offset = acc_clip_arr[i];
                //移动vertices、uv和color
                m_vertiecs[i-offset] = m_vertiecs[i];
                m_uvs[i-offset] = m_uvs[i];
                m_colors[i-offset] = m_colors[i];
            }
        }
        
        //更新indics中的值
        for(int i = 0; i < m_indices.Count; ++i)
        {
            //Debug.Log("[OptimizeMesh] update indice : " + m_indices[i].ToString());
            if(m_clip_arr[m_indices[i]])
            {
                //已经被剔除了，不用更新
                continue;
            }
            m_indices[i] -= acc_clip_arr[m_indices[i]];
        }

        //下面的方式用来更新indice太麻烦，在生成step的时候就有了一个quad 的信息，这个时候用来更新indice，但是定点还是压缩前的顶点
        //把所有的index进行更新
        /*for(int i=0; i<m_vertiecs.Length; ++i)
        { 
            if(m_clip_arr[i])
            {
                continue;
            }
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
        }*/

        //截断
        System.Array.Resize(ref m_vertiecs, m_vertiecs.Length - acc_clip);
        System.Array.Resize(ref m_uvs, m_vertiecs.Length);
        //System.Array.Resize(ref m_indices, 3 * m_vertiecs.Length);
        System.Array.Resize(ref m_colors, m_vertiecs.Length);
    }

    void UpdateMesh(GameObject obj)
    {
        Vector3[] normals = obj.GetComponent<MeshFilter>().sharedMesh.normals;
        System.Array.Resize(ref normals, m_vertiecs.Length);
        obj.GetComponent<MeshFilter>().sharedMesh = new Mesh();
        Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
        mesh.name = "OptimizedColoredMesh";
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
