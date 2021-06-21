

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BundleSystem
{
#if UNITY_EDITOR
    using System.Linq;
    using UnityEditor;
    /// <summary>
    /// utilities can be used in runtime but only in editor 
    /// or just in editor scripts
    /// </summary>
    public static partial class Utility
    {
        public const string kTempBuildPath = "Temp/BundleContentBuildData";
        public static bool IsAssetCanBundled(string assetPath)
        {
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            return mainType != null && mainType != typeof(MonoScript) && mainType.IsSubclassOf(typeof(Object));
        }

        /// <summary>
        /// Search files in directory
        /// </summary>
        
        public static void GetFilesInDirectory(List<string> resultAssetPath, List<string> resultLoadPath, string folderPath, bool includeSubdir)
        {
            GetFilesInDirectoryInternal(string.Empty, resultAssetPath, resultLoadPath, folderPath, includeSubdir);
        }

        static void GetFilesInDirectoryInternal(string dirPrefix, List<string> resultAssetPath, List<string> resultLoadPath, string folderPath, bool includeSubdir)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(folderPath));
            var files = dir.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                var currentFile = files[i];
                var unityPath = Utility.CombinePath(folderPath, currentFile.Name);
                if (!IsAssetCanBundled(unityPath)) continue;

                resultAssetPath.Add(unityPath);
                resultLoadPath.Add(Utility.CombinePath(dirPrefix, Path.GetFileNameWithoutExtension(unityPath)));
            }

            if (includeSubdir)
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    var subdirName = $"{folderPath}/{subDir.Name}";
                    GetFilesInDirectoryInternal(Utility.CombinePath(dirPrefix, subDir.Name), resultAssetPath, resultLoadPath, subdirName, includeSubdir);
                }
            }
        }

        /// <summary>
        /// collect bundle deps to actually use in runtime
        /// </summary>
        public static List<string> CollectBundleDependencies<T>(Dictionary<string, T> deps, string name, bool includeSelf = false) where T : IEnumerable<string>
        {
            var depsHash = new HashSet<string>();
            CollectBundleDependenciesRecursive<T>(depsHash, deps, name, name);
            if (includeSelf) depsHash.Add(name);
            return depsHash.ToList();
        }

        static void CollectBundleDependenciesRecursive<T>(HashSet<string> result, Dictionary<string, T> deps, string name, string rootName) where T : IEnumerable<string>
        {
            foreach (var dependency in deps[name])
            {
                //skip root name to prevent cyclic deps calculation
                if (rootName == dependency) continue;
                if (result.Add(dependency))
                    CollectBundleDependenciesRecursive(result, deps, dependency, rootName);
            }
        }

        //prefabs placed into a scene are encoded into scene when building, and they does not participate bundle references.
        //this is somewhat weird but this happens on scriptable build pipeline
        public static string[] UnwarpSceneEncodedPrefabs(string scenePath, string[] sceneDeps)
        {
            var list = new List<string>(sceneDeps);
            var settings = new UnityEditor.Build.Content.BuildSettings();
            settings.target = EditorUserBuildSettings.activeBuildTarget;
            settings.group = BuildPipeline.GetBuildTargetGroup(settings.target);
            var usageTags = new UnityEditor.Build.Content.BuildUsageTagSet();
            var depsCache = new UnityEditor.Build.Content.BuildUsageCache();

            //extract deps form scriptable build pipeline
#if UNITY_2019_3_OR_NEWER
            var sceneInfo = UnityEditor.Build.Content.ContentBuildInterface.CalculatePlayerDependenciesForScene(scenePath, settings, usageTags, depsCache);
#else
            Directory.CreateDirectory(kTempBuildPath);
            var sceneInfo = UnityEditor.Build.Content.ContentBuildInterface.PrepareScene(scenePath, settings, usageTags, depsCache, kTempBuildPath);
#endif

            //this is needed as calculate function actumatically pops up progress bar
            EditorUtility.ClearProgressBar();

            //we do care only prefab
            var hashSet = new HashSet<string>();
            foreach(var objInfo in sceneInfo.referencedObjects)
            {
                if(objInfo.fileType != UnityEditor.Build.Content.FileType.MetaAssetType) continue;
                var path = AssetDatabase.GUIDToAssetPath(objInfo.guid.ToString());
                if(!path.EndsWith(".prefab")) continue;
                hashSet.Add(path);
            }
            
            //remove direct reference of the prefab and append the deps of the prefab we removed
            var appendList = new List<string>();
            for(int i = list.Count - 1; i >= 0; i--)
            {
                var child = list[i];
                if(AssetDatabase.GetMainAssetTypeAtPath(child) != typeof(UnityEngine.GameObject)) continue;
                if(hashSet.Contains(child)) continue;
                list.RemoveAt(i);
                var deps = AssetDatabase.GetDependencies(child, false);
                appendList.AddRange(deps);
            }

            //append we found into original list except prefab itself
            list.AddRange(appendList);

            //remove duplicates and return
            return list.Distinct().ToArray(); 
        }
    }
#endif

    //Runtime usable Utility
    public static partial class Utility
    {
        public static string CombinePath(params string[] args)
        {
            var combined = Path.Combine(args);
            if(Path.DirectorySeparatorChar == '\\') combined = combined.Replace('\\', '/');
            return combined;
        }
    }
}
