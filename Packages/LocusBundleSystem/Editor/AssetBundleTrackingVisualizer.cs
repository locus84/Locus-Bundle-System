using UnityEngine;
using UnityEditor;
using TrackInfo = BundleSystem.BundleManager.TrackInfo;
using System.Collections.Generic;
using BundleSystem;

public class AssetBundleTrackingVisualizer : EditorWindow
{
    [MenuItem("Window/Asset Management/AssetBundle Tracking Visualizer")]
    static void Init() => EditorWindow.GetWindow<AssetBundleTrackingVisualizer>(false, "AssetBundle TrackInfos").Show();
    static Dictionary<int, TrackInfo> s_TrackInfoCache = new Dictionary<int, TrackInfo>();
    Vector2 m_ScrollPosition;
    
    void OnGUI()
    {
        BundleSystem.BundleManager.GetTrackingSnapshot(s_TrackInfoCache);
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, false, true);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Key", GUILayout.Width(200));
        EditorGUILayout.LabelField($"Owner", GUILayout.Width(200));
        EditorGUILayout.LabelField($"Asset", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        foreach(var kv in s_TrackInfoCache)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{kv.Key} - {kv.Value.BundleName.ToString()}", GUILayout.Width(200));
            EditorGUILayout.ObjectField(kv.Value.Owner, typeof(UnityEngine.Object), true, GUILayout.Width(200));
            EditorGUILayout.ObjectField(kv.Value.Asset, typeof(UnityEngine.Object), false, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        Repaint();
    }
}