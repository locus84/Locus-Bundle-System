﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BundleSystem
{
    [CreateAssetMenu(fileName = "AssetbundleBuildSettings.asset", menuName = "Create Assetbundle Build Settings", order = 999)]
    public class AssetbundleBuildSettings : ScriptableObject
    {
#if UNITY_EDITOR
        static AssetbundleBuildSettings s_EditorInstance = null;

        public static AssetbundleBuildSettings EditorInstance
        {
            get
            {
                if (s_EditorInstance != null) return s_EditorInstance;

                var defaultGUID = UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", string.Empty);
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(defaultGUID);

                if (!string.IsNullOrEmpty(assetPath))
                {
                    var found = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetbundleBuildSettings>(assetPath);
                    if(found != null)
                    {
                        s_EditorInstance = found;
                        return s_EditorInstance;
                    }
                }

                var assetPathes = UnityEditor.AssetDatabase.FindAssets("t:AssetbundleBuildSettings");
                if (assetPathes.Length == 0) return null;

                var guid = UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.AssetDatabase.GUIDToAssetPath(assetPathes[0]));
                UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", guid);
                s_EditorInstance = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetbundleBuildSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(assetPathes[0]));
                return s_EditorInstance;
            }
            set
            {
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(value);
                UnityEditor.EditorPrefs.SetString("LocusActiveBundleSetting", UnityEditor.AssetDatabase.AssetPathToGUID(assetPath));
                s_EditorInstance = value;
            }
        }

        /// <summary>
        /// check setting is valid
        /// </summary>
        public bool IsValid()
        {
            return !BundleSettings.GroupBy(setting => setting.BundleName).Any(group => group.Count() > 1 ||
                string.IsNullOrEmpty(group.Key));
        }

        
        /// <summary>
        /// Check if an asset is included in one of bundles in this setting
        /// </summary>
        public bool TryGetBundleNameAndAssetPath(string editorAssetPath, out string bundleName, out string assetPath)
        {
            foreach(var setting in BundleSettings)
            {
                var bundleFolderPath = UnityEditor.AssetDatabase.GUIDToAssetPath(setting.Folder.guid);
                if (editorAssetPath.StartsWith(bundleFolderPath))
                {
                    //setting does not include subfolder and asset is in subfolder
                    if (!Utility.IsAssetCanBundled(editorAssetPath)) continue;
                    var partialPath = editorAssetPath.Remove(0, bundleFolderPath.Length + 1);

                    //partial path should not contain directory seperator if include subfoler option is false
                    if (!setting.IncludeSubfolder && partialPath.IndexOf('/') > -1) break;

                    assetPath = partialPath.Remove(partialPath.LastIndexOf('.'));
                    bundleName = setting.BundleName;
                    return true;
                }
            }

            bundleName = string.Empty;
            assetPath = string.Empty;
            return false;
        }
#endif
        public const string ManifestFileName = "Manifest.json";
        public static string LocalBundleRuntimePath => Application.streamingAssetsPath + "/localbundles/";
        public string LocalOutputPath => Application.dataPath.Remove(Application.dataPath.Length - 6) + m_LocalOutputFolder;
        public string RemoteOutputPath => Application.dataPath.Remove(Application.dataPath.Length - 6) + m_RemoteOutputFolder;

        public List<BundleSetting> BundleSettings = new List<BundleSetting>();

        [Tooltip("Auto create shared bundles to remove duplicated assets")]
        public bool AutoCreateSharedBundles = true;

        /// <summary>
        /// output folder inside project
        /// </summary>
        [SerializeField]
        [Tooltip("Remote bundle build output folder")]
        string m_RemoteOutputFolder = "RemoteBundles";
        /// <summary>
        /// output folder inside project
        /// </summary>
        [SerializeField]
        [Tooltip("Local bundle build output folder")]
        string m_LocalOutputFolder = "LocalBundles";

        [Tooltip("Remote URL for downloading remote bundles")]
        public string RemoteURL = "http://localhost/";

        [Tooltip("Use built asset bundles even in editor")]
        public bool EmulateInEditor = false;

        [Tooltip("Use Remote output folder when emulating remote bundles")]
        public bool EmulateWithoutRemoteURL = false;

        [Tooltip("Clean cache when initializing BundleManager for testing purpose")]
        public bool CleanCacheInEditor = false;

        //build cache server settings
        public bool ForceRebuild = false;
        public bool AutoUploadS3 = true;
        public bool UseCacheServer = false;
        public string CacheServerHost;
        public int CacheServerPort;

        //ftp settings
        public bool UseFtp = false;
        public string FtpHost;
        public string FtpUserName;
        public string FtpUserPass;
    }

    [System.Serializable]
    public class BundleSetting
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
    }
}

     