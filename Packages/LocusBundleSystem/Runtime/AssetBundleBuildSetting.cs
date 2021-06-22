using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BundleSystem
{
    public abstract class AssetBundleBuildSetting : ScriptableObject
    {
#if UNITY_EDITOR
        static AssetBundleBuildSetting s_ActiveSetting = null;
        static bool isDirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EditorRuntimeInitialize()
        {
            RebuildEditorAssetDatabaseMap();
        }

        class DirtyChecker : UnityEditor.AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                //does not matter, we just need to rebuild editordatabase later on
                AssetBundleBuildSetting.isDirty = true;
            }
        }

        public static void RebuildEditorAssetDatabaseMap()
        {
            if(AssetBundleBuildSetting.TryGetActiveSetting(out var setting)) 
            {
                BundleManager.SetEditorDatabase(setting.CreateEditorDatabase());
                isDirty = false;
            }
        }

        public EditorDatabaseMap CreateEditorDatabase()
        {
            var setting = new EditorDatabaseMap();
            setting.UseAssetDatabase = !EmulateInEditor || !Application.isPlaying;
            setting.CleanCache = CleanCacheInEditor;
            setting.UseOuputAsRemote = EmulateWithoutRemoteURL;
            setting.OutputPath = Utility.CombinePath(OutputPath, UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString());

            var bundleSettings = GetBundleSettings();
            for (int i = 0; i < bundleSettings.Count; i++)
            {
                var currentSetting = bundleSettings[i]; 
                setting.Append(currentSetting.BundleName, currentSetting.AssetNames, currentSetting.AddressableNames);
            }
            return setting;
        }

        public static bool TryGetActiveSetting(out AssetBundleBuildSetting setting, bool findIfNotExist = true)
        {
            if (s_ActiveSetting != null) 
            {
                setting = s_ActiveSetting;
                return true;
            }

            var defaultGUID = UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", string.Empty);
            var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(defaultGUID);

            if (!string.IsNullOrEmpty(assetPath))
            {
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetBundleBuildSetting>(assetPath);
                if(found != null)
                {
                    s_ActiveSetting = found;
                    setting = s_ActiveSetting;
                    return true;
                }
            }
            
            if(!findIfNotExist)
            {
                setting = default;
                return false;
            }

            var typeName = typeof(AssetBundleBuildSetting).Name;
            var assetPathes = UnityEditor.AssetDatabase.FindAssets($"t:{typeName}");

            if (assetPathes.Length == 0) 
            {
                setting = default;
                return false;
            }

            var guid = UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.AssetDatabase.GUIDToAssetPath(assetPathes[0]));
            UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", guid);
            s_ActiveSetting = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetBundleBuildSetting>(UnityEditor.AssetDatabase.GUIDToAssetPath(assetPathes[0]));

            setting = s_ActiveSetting;
            return true;
        }

        public static void SetActiveSetting(AssetBundleBuildSetting setting, bool rebuildDatabaseMap = false)
        {
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(setting);
            UnityEditor.EditorPrefs.SetString("LocusActiveBundleSetting", UnityEditor.AssetDatabase.AssetPathToGUID(assetPath));
            s_ActiveSetting = setting;

            //rebuild map right away
            if(rebuildDatabaseMap) BundleManager.SetEditorDatabase(setting.CreateEditorDatabase());
            //if not, make it dirty
            isDirty = !rebuildDatabaseMap;
        }

        public string OutputPath => Application.dataPath.Remove(Application.dataPath.Length - 6) + OutputFolder;
#endif

        /// <summary>
        /// output folder inside project
        /// </summary>
        [SerializeField]
        [Tooltip("AssetBundle build output folder")]
        public string OutputFolder = "AssetBundles";

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
        public bool UseCacheServer = false;
        public string CacheServerHost;
        public int CacheServerPort;

        /// <summary>
        /// provide actual assets to bundle
        /// </summary>
        public abstract List<BundleSetting> GetBundleSettings();
        
        /// <summary>
        /// check setting is valid
        /// </summary>
        public virtual bool IsValid() => true;
    }

    public struct BundleSetting
    {
        public string BundleName;
        public bool IncludedInPlayer;
        public string[] AssetNames;
        public string[] AddressableNames;
        public bool CompressBundle;
        public bool AutoSharedBundle;
    }
}

     