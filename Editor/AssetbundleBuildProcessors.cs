using UnityEngine;
using UnityEditor;
using System.IO;
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


            var localBundleSourcePath = Utility.CombinePath(settings.LocalOutputPath, EditorUserBuildSettings.activeBuildTarget.ToString());
            if(!Directory.Exists(localBundleSourcePath))
            {
                if(Application.isBatchMode)
                {
                    Debug.LogError("Missing built local bundle directory, Locus bundle system won't work properly.");
                    return; //we can't build now as it's in batchmode
                }
                else
                {
                    var buildNow = EditorUtility.DisplayDialog("LocusBundleSystem", "Warning - Missing built local bundle directory, would you like to build now?", "Yes", "Not now");
                    if(!buildNow) return; //user declined
                    AssetbundleBuilder.BuildAssetBundles(BuildType.Local);
                }
            }

            FileUtil.CopyFileOrDirectory(Utility.CombinePath(settings.LocalOutputPath, EditorUserBuildSettings.activeBuildTarget.ToString()), AssetbundleBuildSettings.LocalBundleRuntimePath);
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
