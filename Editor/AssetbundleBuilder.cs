using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEngine;
using System;

namespace BundleSystem
{
    /// <summary>
    /// class that contains actual build functionalities
    /// </summary>
    public static class AssetbundleBuilder
    {
        const string LogFileName = "BundleBuildLog.txt";

        class CustomBuildParameters : BundleBuildParameters
        {
            AssetbundleBuildSettings m_Settings;
            bool m_IsLocal;

            public CustomBuildParameters(AssetbundleBuildSettings settings, BuildTarget target, BuildTargetGroup group, string outputFolder, bool isLocal) : base(target, group, outputFolder)
            {
                m_Settings = settings;
                m_IsLocal = isLocal;
            }

            // Override the GetCompressionForIdentifier method with new logic
            public override BuildCompression GetCompressionForIdentifier(string identifier)
            {
                //local bundles are always lz4 for faster initializing
                if (m_IsLocal) return BuildCompression.LZ4;

                //find user set compression method
                var found = m_Settings.BundleSettings.FirstOrDefault(setting => setting.BundleName == identifier);
                return found == null || !found.CompressBundle ? BuildCompression.LZ4 : BuildCompression.LZMA;
            }
        }

        public static void BuildAssetBundles(AssetbundleBuildSettings settings, bool local = false)
        {
            var bundleList = new List<AssetBundleBuild>();
            foreach (var setting in settings.BundleSettings)
            {
                var folderPath = AssetDatabase.GUIDToAssetPath(setting.Folder.guid);
                var dir = new DirectoryInfo(Path.Combine(Application.dataPath, folderPath.Remove(0, 7)));
                if (!dir.Exists) throw new System.Exception($"Could not found Path {folderPath} for {setting.BundleName}");
                var assetPathes = new List<string>();
                var loadPathes = new List<string>();
                AssetbundleBuildSettings.GetFilesInDirectory(string.Empty, assetPathes, loadPathes, dir, setting.IncludeSubfolder);
                if (assetPathes.Count == 0) Debug.LogWarning($"Could not found Any Assets {folderPath} for {setting.BundleName}");
                var newBundle = new AssetBundleBuild();
                newBundle.assetBundleName = setting.BundleName;
                newBundle.assetNames = assetPathes.ToArray();
                newBundle.addressableNames = loadPathes.ToArray();
                bundleList.Add(newBundle);
            }

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var groupTarget = BuildPipeline.GetBuildTargetGroup(buildTarget);

            var outputPath = Path.Combine(local? settings.LocalOutputPath : settings.RemoteOutputPath, buildTarget.ToString());
            var buildParams = new CustomBuildParameters(settings, buildTarget, groupTarget, outputPath, local);

            if (settings.UseCacheServer)
            {
                buildParams.CacheServerHost = settings.CacheServerHost;
                buildParams.CacheServerPort = settings.CacheServerPort;
            }

            //do build with post packing callback
            if (local) ContentPipeline.BuildCallbacks.PostPackingCallback += PostPackingForSelectiveBuild;
            var returnCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(bundleList.ToArray()), out var results);
            if (local) ContentPipeline.BuildCallbacks.PostPackingCallback -= PostPackingForSelectiveBuild;

            if (returnCode == ReturnCode.Success)
            {
                WriteManifestFile(outputPath, results, buildTarget, settings.RemoteURL);
                WriteLogFile(outputPath, results);
                //only remote bundle build generates link.xml
                if (local)
                {
                    EditorUtility.DisplayDialog("Build Succeeded!", "Local bundle build succeeded!", "Confirm");
                }
                else
                {
                    var linkPath = TypeLinkerGenerator.Generate(settings, results);
                    EditorUtility.DisplayDialog("Build Succeeded!", $"Remote bundle build succeeded, \n {linkPath} updated!", "Confirm");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Build Failed!", $"Bundle build failed, \n Code : {returnCode}", "Confirm");
                Debug.LogError(returnCode);
            }
        }

        private static ReturnCode PostPackingForSelectiveBuild(IBuildParameters buildParams, IDependencyData dependencyData, IWriteData writeData)
        {
            var includedBundles = AssetbundleBuildSettings.EditorInstance.BundleSettings
                .Where(setting => setting.IncludedInPlayer)
                .Select(setting => setting.BundleName)
                .ToList();

            //quick exit 
            if (includedBundles == null || includedBundles.Count == 0) return ReturnCode.Success;

            for (int i = writeData.WriteOperations.Count - 1; i >= 0; --i)
            {
                string bundleName;

                //derive bundle name from operation
                if (writeData.WriteOperations[i] is AssetBundleWriteOperation bundleOp)
                {
                    bundleName = bundleOp.Info.bundleName;
                }
                else if (writeData.WriteOperations[i] is SceneBundleWriteOperation sceneBundleOp)
                {
                    bundleName = sceneBundleOp.Info.bundleName;
                }
                else if (writeData.WriteOperations[i] is SceneDataWriteOperation sceneDataOp)
                {
                    //this is the simplest way to derive bundle name from SceneDataWriteOperation
                    var bundleWriteData = writeData as IBundleWriteData;
                    bundleName = bundleWriteData.FileToBundle[sceneDataOp.Command.internalName];
                }
                else
                {
                    Debug.LogError("Unexpected write operation");
                    return ReturnCode.Error;
                }

                // if we do not want to build that bundle, remove the write operation from the list
                if (includedBundles.Contains(bundleName) == false)
                {
                    writeData.WriteOperations.RemoveAt(i);
                }
            }

            return ReturnCode.Success;
        }

        /// <summary>
        /// write manifest into target path.
        /// </summary>
        static void WriteManifestFile(string path, IBundleBuildResults bundleResults, BuildTarget target, string remoteURL)
        {
            var manifest = new AssetbundleBuildManifest();
            manifest.BuildTarget = target.ToString();
            var deps = bundleResults.BundleInfos.ToDictionary(kv => kv.Key, kv => kv.Value.Dependencies.ToList());
            var depsCollectCache = new HashSet<string>();

            foreach (var result in bundleResults.BundleInfos)
            {
                var bundleInfo = new AssetbundleBuildManifest.BundleInfo();
                bundleInfo.BundleName = result.Key;
                depsCollectCache.Clear();
                CollectBundleDependencies(depsCollectCache, deps, result.Key);
                bundleInfo.Dependencies = depsCollectCache.ToList();
                bundleInfo.Hash = result.Value.Hash;
                bundleInfo.Size = new FileInfo(result.Value.FileName).Length;
                manifest.BundleInfos.Add(bundleInfo);
            }

            //sort by size
            manifest.BundleInfos.Sort((a, b) => b.Size.CompareTo(a.Size));
            var manifestString = JsonUtility.ToJson(manifest);
            manifest.GlobalHash = Hash128.Compute(manifestString);
            manifest.BundleVersion = PlayerSettings.bundleVersion;
            manifest.RemoteURL = remoteURL;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, AssetbundleBuildSettings.ManifestFileName), JsonUtility.ToJson(manifest, true));
        }


        /// <summary>
        /// write logs into target path.
        /// </summary>
        static void WriteLogFile(string path, IBundleBuildResults bundleResults)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Build Time : {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            sb.AppendLine();
            
            for(int i = 0; i < bundleResults.BundleInfos.Count; i++)
            {
                var bundleInfo = bundleResults.BundleInfos.ElementAt(i);
                var writeResult = bundleResults.WriteResults.ElementAt(i);
                sb.AppendLine($"----File Path : {bundleInfo.Value.FileName}----");
                var assetDic = new Dictionary<string, ulong>();
                foreach(var file in writeResult.Value.serializedObjects)
                {
                    //skip nonassettype
                    if (file.serializedObject.fileType == UnityEditor.Build.Content.FileType.NonAssetType) continue;

                    //gather size
                    var assetPath = AssetDatabase.GUIDToAssetPath(file.serializedObject.guid.ToString());
                    if (!assetDic.ContainsKey(assetPath))
                    {
                        assetDic.Add(assetPath, file.header.size);
                    } 
                    else assetDic[assetPath] += file.header.size;
                }

                //sort by it's size
                var sortedAssets = assetDic.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key);

                foreach(var asset in sortedAssets)
                {
                    sb.AppendLine($"{(asset.Value * 0.000001f).ToString("0.00000").PadLeft(10)} mb - {asset.Key}");
                }

                sb.AppendLine();
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, LogFileName), sb.ToString());
        }

        static void CollectBundleDependencies(HashSet<string> result, Dictionary<string, List<string>> deps,  string name)
        {
            result.Add(name);
            foreach(var dependency in deps[name])
            {
                if (!result.Contains(dependency))
                    CollectBundleDependencies(result, deps, dependency);
            }
        }
    }
}
