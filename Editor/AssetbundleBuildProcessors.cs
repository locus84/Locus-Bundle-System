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
            FileUtil.CopyFileOrDirectory(Path.Combine(settings.LocalOutputPath, EditorUserBuildSettings.activeBuildTarget.ToString()), AssetbundleBuildSettings.LocalBundleRuntimePath);
            AssetDatabase.Refresh();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            FileUtil.DeleteFileOrDirectory(AssetbundleBuildSettings.LocalBundleRuntimePath);
            AssetDatabase.Refresh();
        }
    }
}
