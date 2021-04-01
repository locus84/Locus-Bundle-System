using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BundleSystem
{
    public class AssetbundleBuildProcessors : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 999;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = AssetbundleBuildSettings.EditorInstance;
            //no instance found
            if (settings == null) return;
            if (Directory.Exists(AssetbundleBuildSettings.LocalBundleRuntimePath)) Directory.Delete(AssetbundleBuildSettings.LocalBundleRuntimePath, true);
            if (!Directory.Exists(Application.streamingAssetsPath)) Directory.CreateDirectory(Application.streamingAssetsPath);

            //there should be a local bundle
            var localBundleSourcePath = Utility.CombinePath(settings.OutputPath, EditorUserBuildSettings.activeBuildTarget.ToString());
            if(!Directory.Exists(localBundleSourcePath))
            {
                if(Application.isBatchMode)
                {
                    Debug.LogError("Missing built local bundle directory, Locus bundle system won't work properly.");
                    return; //we can't build now as it's in batchmode
                }
                else
                {
                    var buildNow = EditorUtility.DisplayDialog("LocusBundleSystem", "Warning - Missing built bundle directory, would you like to build now?", "Yes", "Not now");
                    if(!buildNow) return; //user declined
                    AssetbundleBuilder.BuildAssetBundles(settings);
                }
            }

            //load manifest and make local bundle list
            var manifest = JsonUtility.FromJson<AssetbundleBuildManifest>(File.ReadAllText(Utility.CombinePath(localBundleSourcePath, AssetbundleBuildSettings.ManifestFileName)));
            var localBundleNames = manifest.BundleInfos.Where(bi => bi.IsLocal).Select(bi => bi.BundleName).ToList();

            Directory.CreateDirectory(AssetbundleBuildSettings.LocalBundleRuntimePath);

            //copy only manifest and local bundles                        
            foreach(var file in new DirectoryInfo(localBundleSourcePath).GetFiles())
            {
                if(!localBundleNames.Contains(file.Name) && AssetbundleBuildSettings.ManifestFileName != file.Name) continue;
                FileUtil.CopyFileOrDirectory(file.FullName, Utility.CombinePath(AssetbundleBuildSettings.LocalBundleRuntimePath, file.Name));
            }

            AssetDatabase.Refresh();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if(FileUtil.DeleteFileOrDirectory(AssetbundleBuildSettings.LocalBundleRuntimePath))
            {
                AssetDatabase.Refresh();
            }
        }
    }
}
