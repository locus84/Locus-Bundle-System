﻿using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using UnityEditor.Build.Pipeline.Utilities;
using System.IO;

namespace BundleSystem
{
    [DisallowMultipleComponent]
    [CustomEditor(typeof(AssetbundleBuildSettings))]
    public class AssetbundleBuildSettingsInspector : Editor
    {
        SerializedProperty m_SettingsProperty;
        SerializedProperty m_AutoCreateSharedBundles;
        SerializedProperty m_RemoteOutputPath;
        SerializedProperty m_LocalOutputPath;
        SerializedProperty m_EmulateBundle;
        SerializedProperty m_EmulateUseRemoteFolder;
        SerializedProperty m_CleanCache;
        SerializedProperty m_RemoteURL;
        ReorderableList list;

        SerializedProperty m_ForceRebuld;
        SerializedProperty m_AutoUploadS3;
        SerializedProperty m_UseCacheServer;
        SerializedProperty m_CacheServerHost;
        SerializedProperty m_CacheServerPort;

        SerializedProperty m_UseFtp;
        SerializedProperty m_FtpHost;
        SerializedProperty m_FtpUser;
        SerializedProperty m_FtpPass;

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Asset Management/Select Active Assetbundle Build Settings")]
        static void SelectActiveSettings()
        {
            Selection.activeObject = AssetbundleBuildSettings.EditorInstance;
        }

        private void OnEnable()
        {
            m_SettingsProperty = serializedObject.FindProperty("BundleSettings");
            m_AutoCreateSharedBundles = serializedObject.FindProperty("AutoCreateSharedBundles");
            m_RemoteOutputPath = serializedObject.FindProperty("m_RemoteOutputFolder");
            m_LocalOutputPath = serializedObject.FindProperty("m_LocalOutputFolder");
            m_EmulateBundle = serializedObject.FindProperty("EmulateInEditor");
            m_EmulateUseRemoteFolder = serializedObject.FindProperty("EmulateWithoutRemoteURL");
            m_CleanCache = serializedObject.FindProperty("CleanCacheInEditor");
            m_RemoteURL = serializedObject.FindProperty("RemoteURL");

            m_ForceRebuld = serializedObject.FindProperty("ForceRebuild");
            m_AutoUploadS3 = serializedObject.FindProperty("AutoUploadS3");
            m_UseCacheServer = serializedObject.FindProperty("UseCacheServer");
            m_CacheServerHost = serializedObject.FindProperty("CacheServerHost");
            m_CacheServerPort = serializedObject.FindProperty("CacheServerPort");

            m_UseFtp = serializedObject.FindProperty("UseFtp");
            m_FtpHost = serializedObject.FindProperty("FtpHost");
            m_FtpUser = serializedObject.FindProperty("FtpUserName");
            m_FtpPass = serializedObject.FindProperty("FtpUserPass");

            var settings = target as AssetbundleBuildSettings;

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
                    EditorGUI.PropertyField(rect, element, new GUIContent(settings.BundleSettings[index].BundleName), element.isExpanded);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = target as AssetbundleBuildSettings;

            list.DoLayoutList();
            bool allowBuild = true;
            if (!settings.IsValid())
            {
                GUILayout.Label("Duplicate or Empty BundleName detected");
                allowBuild = false;
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_AutoCreateSharedBundles);
            if (allowBuild && GUILayout.Button("Get Expected Sharedbundle List"))
            {
                AssetbundleBuilder.WriteExpectedSharedBundles(settings);
                GUIUtility.ExitGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_RemoteOutputPath);
            if (GUILayout.Button("Open", GUILayout.ExpandWidth(false))) EditorUtility.RevealInFinder(Utility.CombinePath(settings.RemoteOutputPath, EditorUserBuildSettings.activeBuildTarget.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_LocalOutputPath);
            if (GUILayout.Button("Open", GUILayout.ExpandWidth(false))) EditorUtility.RevealInFinder(Utility.CombinePath(settings.LocalOutputPath, EditorUserBuildSettings.activeBuildTarget.ToString()));
            GUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(m_RemoteURL);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_EmulateBundle);
            EditorGUILayout.PropertyField(m_EmulateUseRemoteFolder);
            EditorGUILayout.PropertyField(m_CleanCache);
            EditorGUILayout.PropertyField(m_ForceRebuld);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_AutoUploadS3);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(m_UseCacheServer);
            if(m_UseCacheServer.boolValue)
            {
                EditorGUILayout.PropertyField(m_CacheServerHost);
                EditorGUILayout.PropertyField(m_CacheServerPort);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_UseFtp);
            EditorGUILayout.Space();
            if(m_UseFtp.boolValue)
            {
                EditorGUILayout.PropertyField(m_FtpHost);
                EditorGUILayout.PropertyField(m_FtpUser);
                m_FtpPass.stringValue = EditorGUILayout.PasswordField("Ftp Password", m_FtpPass.stringValue);
            }

            GUILayout.Label($"Local Output folder : { settings.LocalOutputPath }");
            GUILayout.Label($"Remote Output folder : { settings.RemoteOutputPath }");

            serializedObject.ApplyModifiedProperties();

            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            if(AssetbundleBuildSettings.EditorInstance == settings)
            {
                EditorGUILayout.BeginHorizontal();
                if (allowBuild && GUILayout.Button("Build Remote"))
                {
                    AssetbundleBuilder.BuildAssetBundles(settings, BuildType.Remote);
                    GUIUtility.ExitGUI();
                }

                if (allowBuild && GUILayout.Button("Build Local"))
                {
                    AssetbundleBuilder.BuildAssetBundles(settings, BuildType.Local);
                    GUIUtility.ExitGUI();
                }

                EditorGUI.BeginDisabledGroup(!settings.UseFtp);
                if (allowBuild && GUILayout.Button("Upload(FTP)"))
                {
                    AssetbundleUploader.UploadAllRemoteFiles(settings);
                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginVertical();
                if (GUILayout.Button("Upload to S3"))
                {
                    LocusAssetbundleUploaderExtension.UploadToS3Bucket(settings);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                if (GUILayout.Button("Set as active setting"))
                {
                    AssetbundleBuildSettings.EditorInstance = settings;
                }
            }

            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }

}
