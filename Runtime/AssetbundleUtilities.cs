

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BundleSystem
{
    using System.Linq;
#if UNITY_EDITOR
    using UnityEditor;
    /// <summary>
    /// utilities can be used in runtime but in editor
    /// or just in editor scripts
    /// </summary>
    public static class Utility
    {
        public static bool IsAssetCanBundled(string assetPath)
        {
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            return mainType != null && mainType != typeof(MonoScript) && mainType.IsSubclassOf(typeof(Object));
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
                var unityPath = currentFile.FullName.Remove(0, Application.dataPath.Length - 6).Replace('\\', '/');
                if (!IsAssetCanBundled(unityPath)) continue;

                resultAssetPath.Add(unityPath);
                resultLoadPath.Add(Path.Combine(dirPrefix, Path.GetFileNameWithoutExtension(unityPath)));
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
    }
#endif
}