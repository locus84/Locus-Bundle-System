using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BundleSystem
{
    [CreateAssetMenu(fileName = "AssetBundleBuildSetting.asset", menuName = "Create AssetBundle Build Setting", order = 999)]
    public class FolderBasedAssetBundleBuildSetting : AssetBundleBuildSetting
    {
        /// <summary>
        /// check setting is valid
        /// </summary>
        public override bool IsValid()
        {
            return !FolderSettings.GroupBy(setting => setting.BundleName).Any(group => group.Count() > 1 ||
                string.IsNullOrEmpty(group.Key));
        }

        public List<FolderSetting> FolderSettings = new List<FolderSetting>();

        public override List<BundleSetting> GetBundleSettings()
        {
            var bundleSettingList = new List<BundleSetting>();

            foreach (var folderSetting in FolderSettings)
            {
                //find folder
                var folderPath = AssetDatabase.GUIDToAssetPath(folderSetting.Folder.guid);
                if (!AssetDatabase.IsValidFolder(folderPath)) throw new Exception($"Could not found Path {folderPath} for {folderSetting.BundleName}");

                //collect assets
                var assetPathes = new List<string>();
                var loadPathes = new List<string>();
                Utility.GetFilesInDirectory(assetPathes, loadPathes, folderPath, folderSetting.IncludeSubfolder);
                if (assetPathes.Count == 0) Debug.LogWarning($"Could not found Any Assets {folderPath} for {folderSetting.BundleName}");

                //make assetbundlebuild
                var newBundle = new BundleSetting();
                newBundle.BundleName = folderSetting.BundleName;
                newBundle.AssetNames = assetPathes.ToArray();
                newBundle.AddressableNames = loadPathes.ToArray();
                newBundle.AutoSharedBundle = folderSetting.AutoSharedBundle;
                newBundle.IncludedInPlayer = folderSetting.IncludedInPlayer;
                newBundle.CompressBundle = folderSetting.CompressBundle;
                bundleSettingList.Add(newBundle);
            }

            return bundleSettingList;
        }

        //ftp settings
        public bool UseFtp = false;
        public string FtpHost;
        public string FtpUserName;
        public string FtpUserPass;
        
        [System.Serializable]
        public class FolderSetting
        {
            [Tooltip("AssetBundle Name")]
            public string BundleName;
            [Tooltip("Should this bundle included in player?")]
            public bool IncludedInPlayer = false;
            public FolderReference Folder;
            [Tooltip("Should include subfolder?")]
            public bool IncludeSubfolder = false;
            [Tooltip("Works only for remote bundle, true for LMZA, false for LZ4")]
            public bool CompressBundle = true;
            [Tooltip("Automatically generate shared bundles")]
            public bool AutoSharedBundle = true;
        }
    }
}