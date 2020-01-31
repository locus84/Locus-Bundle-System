using System.Collections;
using System.Collections.Generic;
using System.IO;
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
                UnityEditor.EditorPrefs.GetString("LocusActiveBundleSetting", UnityEditor.AssetDatabase.AssetPathToGUID(assetPath));
                s_EditorInstance = value;
            }
        }

        /// <summary>
        /// Search files in directory
        /// </summary>
        public static void GetFilesInDirectory(string dirPrefix, List<string> resultAssetPath, List<string> resultLoadPath, DirectoryInfo dir, bool includeSubdir)
        {
            var files = dir.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                var currentFile = files[i];
                var unityPath = currentFile.FullName.Remove(0, Application.dataPath.Length - 6);
                var mainType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(unityPath);
                if (mainType == null) continue;
                if (mainType == typeof(UnityEditor.MonoScript)) continue;
                if (mainType.IsSubclassOf(typeof(Object)))
                {
                    resultAssetPath.Add(unityPath);
                    resultLoadPath.Add(Path.Combine(dirPrefix, Path.GetFileNameWithoutExtension(unityPath)).Replace('\\', '/'));
                }
            }

            if (includeSubdir)
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    GetFilesInDirectory(Path.Combine(dirPrefix, dir.Name), resultAssetPath, resultLoadPath, subDir, includeSubdir);
                }
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
#endif
        public const string ManifestFileName = "Manifest.json";
        public static string LocalBundleRuntimePath => Application.streamingAssetsPath + "/localbundles/";
        public string LocalOutputPath => Application.dataPath.Remove(Application.dataPath.Length - 6) + m_LocalOutputFolder;
        public string RemoteOutputPath => Application.dataPath.Remove(Application.dataPath.Length - 6) + m_RemoteOutputFolder;

        public List<BundleSetting> BundleSettings = new List<BundleSetting>();

        /// <summary>
        /// output folder inside project
        /// </summary>
        [SerializeField]
        string m_RemoteOutputFolder = "RemoteBundles";
        /// <summary>
        /// output folder inside project
        /// </summary>
        [SerializeField]
        string m_LocalOutputFolder = "LocalBundles";
        public string RemoteURL = "http://localhost/";
        public bool EmulateInEditor = false;
        public bool CleanCacheInEditor = false;

        //build cache server settings
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

     