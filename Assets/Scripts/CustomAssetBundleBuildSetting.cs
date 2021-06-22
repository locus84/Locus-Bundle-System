using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BundleSystem;

#if UNITY_EDITOR
[CreateAssetMenu(fileName = "AssetbundleBuildSetting.asset", menuName = "Create Custom Assetbundle Build Setting", order = 999)]
public class CustomAssetBundleBuildSetting : AssetbundleBuildSetting 
{
    public override List<BundleSetting> GetBundleSettings()
    {
        //add settings manually
        var bundleSettings = new List<BundleSetting>();

        AddFilesInFolder("Local", "Assets/TestRemoteResources/Local", true, true, bundleSettings);
        AddFilesInFolder("Object", "Assets/TestRemoteResources/Object", false, true, bundleSettings);
        AddFilesInFolder("Object_RootOnly", "Assets/TestRemoteResources/Object_RootOnly", false, false, bundleSettings);
        AddFilesInFolder("Scene", "Assets/TestRemoteResources/Scene", false, true, bundleSettings);
        
        return bundleSettings;
    }

    static void AddFilesInFolder(string bundleName, string folderPath, bool local, bool includeSubfolder, List<BundleSetting> targetList)
    {
        var assetPath = new List<string>();
        var loadPath = new List<string>();

        Utility.GetFilesInDirectory(assetPath, loadPath, folderPath, includeSubfolder);

        targetList.Add(new BundleSetting(){
            AssetNames = assetPath.ToArray(),
            AddressableNames = loadPath.ToArray(),
            BundleName = bundleName,
            AutoSharedBundle = true,
            CompressBundle = true,
            IncludedInPlayer = local
        });
    }

    public override bool IsValid() => true;
}

#endif