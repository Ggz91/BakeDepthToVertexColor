using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

public class OdinEditorWindow : OdinMenuEditorWindow
{
    [MenuItem("CusPlugin/OdinEditroWindow")]
    private static void ShowWindow()
    {
        GetWindow<OdinEditorWindow>().Show();
    }
    static BakeDepthToVertexColorComponent m_bake_depth_to_vertex_color_component = new BakeDepthToVertexColorComponent();
    static OdinMenuTree tree = new OdinMenuTree();
    protected override OdinMenuTree BuildMenuTree()
    {
        tree.Selection.SupportsMultiSelect = false;
        tree.Add("EditorWindow", m_bake_depth_to_vertex_color_component);
        return tree;
    }
}