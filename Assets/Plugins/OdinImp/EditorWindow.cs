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
        tree = new OdinMenuTree();
        tree.Selection.SupportsMultiSelect = false;
        tree.Add("EditorWindow", m_bake_depth_to_vertex_color_component);
        tree.Selection.SelectionChanged += OnSelectionChanged;
        TrySelectMenuItemWithObject(m_bake_depth_to_vertex_color_component);
        return tree;
    }
    void OnEnterWindow()
    {
        if(tree.Selection.SelectedValue.GetType() == m_bake_depth_to_vertex_color_component?.GetType())
        {
            //进入界面
            m_bake_depth_to_vertex_color_component.Enter();
        }
        else
        {
            m_bake_depth_to_vertex_color_component.Leave();
        }
    }
    void Leave()
    {
        m_bake_depth_to_vertex_color_component.Leave();
    }
    void OnSelectionChanged(SelectionChangedType type)
    {
        if(type == SelectionChangedType.ItemAdded)
        {
            OnEnterWindow();
        }
    }
    protected override void OnDestroy()
    {
        Leave();
    }
}