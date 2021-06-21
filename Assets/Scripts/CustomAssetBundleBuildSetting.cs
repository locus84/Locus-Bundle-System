using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BundleSystem;

public class CustomAssetBundleBuildSetting : AssetbundleBuildSetting 
{
    public override List<BundleSetting> GetBundleSettings()
    {
        throw new System.NotImplementedException();
    }

    public override bool IsValid()
    {
        return base.IsValid();
    }
}
