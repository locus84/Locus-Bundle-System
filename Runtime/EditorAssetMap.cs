using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
namespace BundleSystem
{
    public class EditorAssetMap
    {        
        private Dictionary<string, Dictionary<string, List<string>>> m_Map = new Dictionary<string, Dictionary<string, List<string>>>();
        static List<string> s_EmptyStringList = new List<string>();
        static string[] s_EmptyStringArray = new string[0];
        
        public EditorAssetMap(AssetbundleBuildSettings settings)
        {
            var assetPath = new List<string>();
            var loadPath = new List<string>();
            foreach (var setting in settings.BundleSettings)
            {
                assetPath.Clear();
                loadPath.Clear();
                var folderPath = UnityEditor.AssetDatabase.GUIDToAssetPath(setting.Folder.guid);
                Utility.GetFilesInDirectory(string.Empty, assetPath, loadPath, folderPath, setting.IncludeSubfolder);
                var assetList = new Dictionary<string, List<string>>();
                for(int i = 0; i < assetPath.Count; i++)
                {
                    if(assetList.TryGetValue(loadPath[i], out var list))
                    {
                        list.Add(assetPath[i]);
                        continue;
                    }
                    assetList.Add(loadPath[i], new List<string>() { assetPath[i] });
                }
                m_Map.Add(setting.BundleName, assetList);
            }
        }

        public List<string> GetAssetPaths(string bundleName, string assetName)
        {
            if (!m_Map.TryGetValue(bundleName, out var innerDic)) return s_EmptyStringList;
            if (!innerDic.TryGetValue(assetName, out var pathList)) return s_EmptyStringList;
            return pathList;
        }

        public string[] GetAssetPaths(string bundleName)
        {
            if (!m_Map.TryGetValue(bundleName, out var innerDic)) return s_EmptyStringArray;
            return innerDic.Values.SelectMany(list => list).ToArray();
        }
        
        public string[] GetAssetNames(string bundleName)
        {
            if (!m_Map.TryGetValue(bundleName, out var innerDic)) return s_EmptyStringArray;
            return innerDic.Keys.ToArray();
        }

        public bool IsAssetExist(string bundleName, string assetName)
        {
            var assets = GetAssetPaths(bundleName, assetName);
            return assets.Count > 0;
        }

        public string GetAssetPath<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            var assets = GetAssetPaths(bundleName, assetName);
            if (assets.Count == 0) return string.Empty;

            var typeExpected = typeof(T);
            var foundIndex = 0;

            for (int i = 0; i < assets.Count; i++)
            {
                var foundType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assets[i]);
                if (foundType == typeExpected || foundType.IsSubclassOf(typeExpected))
                {
                    foundIndex = i;
                    break;
                }
            }

            return assets[foundIndex];
        }

        public string GetScenePath(string bundleName, string sceneName)
        {
            var assets = GetAssetPaths(bundleName, sceneName);
            if (assets.Count == 0 || UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assets[0]) == typeof(UnityEngine.SceneManagement.Scene))
            {
                Debug.LogError("Request scene name does not exist in streamed scenes : " + sceneName);
                return string.Empty;
            }

            if (!Application.isPlaying)
            {
                Debug.LogError("Can't load scene while not playing : " + sceneName);
                return string.Empty;
            }
        
            return assets[0];
        }
    }
}
#endif