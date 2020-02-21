using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public struct BakeDepthParam
{
    public Vector2Int Size;
    public float UnitSize;
    public Shader DepthShader;
    public float Bottom;
    public float Top;
}

public class BakeDepthUtil
{
    #region var
    static BakeDepthUtil m_instance = new BakeDepthUtil();
    Camera m_cam = null;
    RenderTexture m_rt;
    BakeDepthParam m_param;
    #endregion

    #region  method
    public RenderTexture Execute(Vector3 pos)
    {
        //设置相机
        InitCamera(pos);

        //用相机渲染深度
        return RenderDepth();
    }
    public void InitParam(in BakeDepthParam param)
    {
        //设置
        Init(param);
    }
    
    void ChangeToDepthRP(bool reset)
    {
        //RenderWithShader在URP中被弃用了，所以下面的做法不能达到效果
        //m_cam.RenderWithShader(m_param.DepthShader, "RenderType");
        //使用下面的方式进行替代
        ScriptableObject obj = AssetDatabase.LoadAssetAtPath(@"Assets/Settings/UniversalRP-HighQuality.asset", typeof(ScriptableObject)) as ScriptableObject;
        SerializedObject se_obj = new SerializedObject(obj);
        SerializedProperty pro = se_obj.FindProperty("m_DefaultRendererIndex");
        pro.intValue = reset ? 0 : 1;
        se_obj.ApplyModifiedProperties();
    }

    void Init(in BakeDepthParam param)
    {
        m_param = param;

        if(null == m_cam)
        {
            GameObject cam_obj = new GameObject("DepthRenderCam");
            cam_obj.AddComponent<Camera>();
            m_cam = cam_obj.GetComponent<Camera>();
        }
       
        m_rt = RenderTexture.GetTemporary(4096, 4096, 24);
        m_rt.filterMode = FilterMode.Point;

        ChangeToDepthRP(false);

        Shader.SetGlobalFloat("_DepthRangeBottom", m_param.Bottom);
        Shader.SetGlobalFloat("_DepthRangeTop", m_param.Top);
    }

    void InitCamera(Vector3 pos)
    {
        //根据plane设置相机相关参数
        m_cam.targetTexture = m_rt;
        m_cam.orthographic = true;
        m_cam.nearClipPlane = 0.1f;
        m_cam.farClipPlane = 1000f;
        m_cam.transform.forward = new Vector3(0, -1, 0);
        float width = m_param.Size.x * m_param.UnitSize;
        float height = m_param.Size.y * m_param.UnitSize;
        m_cam.aspect = height / width;
        m_cam.enabled = true;
        m_cam.orthographicSize = width;
        m_cam.clearFlags = CameraClearFlags.SolidColor;
        m_cam.backgroundColor = Color.black;
        m_cam.gameObject.transform.position = pos + new Vector3(0, 1, 0);
    }

    RenderTexture RenderDepth()
    {
        m_cam.Render();
        return m_rt;
    }

    public void CleanUp()
    {
        if(null != m_cam && null != m_cam.gameObject)
        {
            GameObject.DestroyImmediate(m_cam.gameObject);
        }
        m_cam = null;
        RenderTexture.ReleaseTemporary(m_rt);
        ChangeToDepthRP(true);
    }
    #endregion
}
