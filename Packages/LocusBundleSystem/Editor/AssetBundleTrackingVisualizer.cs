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
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
        foreach(var kv in s_TrackInfoCache)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{kv.Key} - {kv.Value.BundleName.ToString()}");
            EditorGUILayout.ObjectField(new GUIContent("Owner"), kv.Value.Owner, typeof(UnityEngine.Object), true);
            EditorGUILayout.ObjectField(new GUIContent("Asset"), kv.Value.Asset, typeof(UnityEngine.Object), false);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        Repaint();
    }
}