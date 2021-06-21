using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BundleSystem
{
    public abstract class AssetbundleBuildSetting : ScriptableObject
    {
        static bool isDirty;
        
        class DirtyChecker : UnityEditor.AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] _, string[] __, string[] ___, string[] ____)
            {
                UnityEngine.Debug.Log("AssetBundleBuildSetting is Dirty");
                AssetbundleBuildSetting.isDirty = true;
            }
        }

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Asset Management/Select Active Assetbundle Build Setting")]
        static void SelectActiveSettings()
        {
            Selection.activeObject = AssetbundleBuildSetting.GetActiveSetting();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EditorRuntimeInitialize()
        {
            var editorInstance = AssetbundleBuildSetting.GetActiveSetting();
            if(editorInstance != null) 
            {
                BundleManager.SetEditorDatabase(editorInstance.CreateEditorDatabase());
            }
        }

        [ContextMenu("Set As Active Setting")]
        void SetDefaultSetting()
        {
            AssetbundleBuildSetting.SetActiveSetting(this);
        }
        
        [ContextMenu("Build With This Setting")]
        void BuildThisSetting()
        {
            AssetbundleBuilder.BuildAssetBundles(this);
        }
        
        [ContextMenu("Get Expected Shared Bundles")]
        void GetSharedBundleLog()
        {
            AssetbundleBuilder.WriteExpectedSharedBundles(this);
        }

        static AssetbundleBuildSetting s_ActiveSetting = null;

        public static AssetbundleBuildSetting GetActiveSetting(bool findIfNotExist = true)
        {
            if (s_ActiveSetting != null) return s_ActiveSetting;

            var defaultGUID = UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", string.Empty);
            var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(defaultGUID);

            if (!string.IsNullOrEmpty(assetPath))
            {
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetbundleBuildSetting>(assetPath);
                if(found != null)
                {
                    s_ActiveSetting = found;
                    return s_ActiveSetting;
                }
            }
            
            if(!findIfNotExist) return null;

            var typeName = typeof(AssetbundleBuildSetting).Name;
            var assetPathes = UnityEditor.AssetDatabase.FindAssets($"t:{typeName}");

            if (assetPathes.Length == 0) return null;

            var guid = UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.AssetDatabase.GUIDToAssetPath(assetPathes[0]));
            UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", guid);
            s_ActiveSetting = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetbundleBuildSetting>(UnityEditor.AssetDatabase.GUIDToAssetPath(assetPathes[0]));
            return s_ActiveSetting;
        }

        public static void SetActiveSetting(AssetbundleBuildSetting setting)
        {
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(setting);
            UnityEditor.EditorPrefs.SetString("LocusActiveBundleSetting", UnityEditor.AssetDatabase.AssetPathToGUID(assetPath));
            s_ActiveSetting = setting;
            isDirty = true;
        }

        public abstract List<BundleSetting> GetBundleSettings();
        public string OutputPath => Application.dataPath.Remove(Application.dataPath.Length - 6) + OutputFolder;

        /// <summary>
        /// output folder inside project
        /// </summary>
        [SerializeField]
        [Tooltip("Assetbundle build output folder")]
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
        /// check setting is valid
        /// </summary>
        public virtual bool IsValid()
        {
            return true;
        }

        public EditorDatabase CreateEditorDatabase()
        {
            var setting = new EditorDatabase();
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

     