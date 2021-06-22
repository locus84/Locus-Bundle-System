using BundleSystem;
using UnityEditor;
using UnityEngine;

public static class AssetbundleBuildSettingExtensions
{
    [MenuItem ("CONTEXT/AssetbundleBuildSetting/Set As Active Setting")]
    static void SetDefaultSetting()
    {
        var setting = Selection.activeObject as AssetbundleBuildSetting;
        if(setting != null) AssetbundleBuildSetting.SetActiveSetting(setting);
    }
    
    [MenuItem ("CONTEXT/AssetbundleBuildSetting/Build With This Setting")]
    static void BuildThisSetting()
    {
        var setting = Selection.activeObject as AssetbundleBuildSetting;
        if(setting != null)  AssetbundleBuilder.BuildAssetBundles(setting);
    }

    [MenuItem ("CONTEXT/AssetbundleBuildSetting/Get Expected Shared Bundles")]
    static void GetSharedBundleLog()
    {
        var setting = Selection.activeObject as AssetbundleBuildSetting;
        if(setting != null) AssetbundleBuilder.WriteExpectedSharedBundles(setting);
    }

    // Add menu named "My Window" to the Window menu
    [MenuItem("Window/Asset Management/Select Active Assetbundle Build Setting")]
    static void SelectActiveSettings()
    {
        if(AssetbundleBuildSetting.TryGetActiveSetting(out var setting))
        {
            Selection.activeObject = setting;    
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "No Setting Found", "Okay");
        }
    }

}
