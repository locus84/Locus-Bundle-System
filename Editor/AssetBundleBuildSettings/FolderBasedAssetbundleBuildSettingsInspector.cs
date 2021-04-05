using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace BundleSystem
{
    [DisallowMultipleComponent]
    [CustomEditor(typeof(FolderBasedAssetbundleBuildSettings))]
    public class FolderBasedAssetbundleBuildSettingsInspector : Editor
    {
        SerializedProperty m_SettingsProperty;
        SerializedProperty m_OutputPath;
        SerializedProperty m_EmulateBundle;
        SerializedProperty m_EmulateUseRemoteFolder;
        SerializedProperty m_CleanCache;
        SerializedProperty m_RemoteURL;
        ReorderableList list;

        SerializedProperty m_ForceRebuld;
        SerializedProperty m_UseCacheServer;
        SerializedProperty m_CacheServerHost;
        SerializedProperty m_CacheServerPort;

        SerializedProperty m_UseFtp;
        SerializedProperty m_FtpHost;
        SerializedProperty m_FtpUser;
        SerializedProperty m_FtpPass;

        private void OnEnable()
        {
            m_SettingsProperty = serializedObject.FindProperty("FolderSettings");
            m_OutputPath = serializedObject.FindProperty("OutputFolder");
            m_EmulateBundle = serializedObject.FindProperty("EmulateInEditor");
            m_EmulateUseRemoteFolder = serializedObject.FindProperty("EmulateWithoutRemoteURL");
            m_CleanCache = serializedObject.FindProperty("CleanCacheInEditor");
            m_RemoteURL = serializedObject.FindProperty("RemoteURL");

            m_ForceRebuld = serializedObject.FindProperty("ForceRebuild");
            m_UseCacheServer = serializedObject.FindProperty("UseCacheServer");
            m_CacheServerHost = serializedObject.FindProperty("CacheServerHost");
            m_CacheServerPort = serializedObject.FindProperty("CacheServerPort");

            m_UseFtp = serializedObject.FindProperty("UseFtp");
            m_FtpHost = serializedObject.FindProperty("FtpHost");
            m_FtpUser = serializedObject.FindProperty("FtpUserName");
            m_FtpPass = serializedObject.FindProperty("FtpUserPass");

            var settings = target as FolderBasedAssetbundleBuildSettings;

            list = new ReorderableList(serializedObject, m_SettingsProperty, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Bundle List");
                },

                elementHeightCallback = index =>
                {
                    var element = m_SettingsProperty.GetArrayElementAtIndex(index);
                    return EditorGUI.GetPropertyHeight(element, element.isExpanded);
                },

                drawElementCallback = (rect, index, a, h) =>
                {
                    // get outer element
                    var element = m_SettingsProperty.GetArrayElementAtIndex(index);
                    rect.xMin += 10;
                    EditorGUI.PropertyField(rect, element, new GUIContent(settings.FolderSettings[index].BundleName), element.isExpanded);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = target as FolderBasedAssetbundleBuildSettings;

            list.DoLayoutList();
            bool allowBuild = true;
            if (!settings.IsValid())
            {
                GUILayout.Label("Duplicate or Empty BundleName detected");
                allowBuild = false;
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_OutputPath);
            if (GUILayout.Button("Open", GUILayout.ExpandWidth(false))) EditorUtility.RevealInFinder(Utility.CombinePath(settings.OutputPath, EditorUserBuildSettings.activeBuildTarget.ToString()));
            GUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(m_RemoteURL);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_EmulateBundle);
            EditorGUILayout.PropertyField(m_EmulateUseRemoteFolder);
            EditorGUILayout.PropertyField(m_CleanCache);
            EditorGUILayout.PropertyField(m_ForceRebuld);
            
            EditorGUILayout.PropertyField(m_UseCacheServer);
            if(m_UseCacheServer.boolValue)
            {
                EditorGUI.indentLevel ++;
                EditorGUILayout.PropertyField(m_CacheServerHost);
                EditorGUILayout.PropertyField(m_CacheServerPort);
                EditorGUI.indentLevel --;
            }

            EditorGUILayout.PropertyField(m_UseFtp);
            if(m_UseFtp.boolValue)
            {
                EditorGUI.indentLevel ++;
                EditorGUILayout.PropertyField(m_FtpHost);
                EditorGUILayout.PropertyField(m_FtpUser);
                m_FtpPass.stringValue = EditorGUILayout.PasswordField("Ftp Password", m_FtpPass.stringValue);
                EditorGUI.indentLevel --;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            if(AssetbundleBuildSettings.EditorInstance == settings)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(!settings.UseFtp);
                if (allowBuild && GUILayout.Button("Upload(FTP)"))
                {
                    AssetbundleUploader.UploadAllRemoteFiles(settings);
                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }

}
