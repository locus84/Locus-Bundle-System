using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleSystem
{
    [System.Serializable]
    public struct BundledAssetPath
    {
        [SerializeField]
        public string BundleName;

        [SerializeField]
        public string AssetName;

        /// <summary>
        /// Load Asset
        /// </summary>
        public T Load<T>() where T : Object
        {
            return BundleManager.Load<T>(BundleName, AssetName);
        }

        /// <summary>
        /// Load AssetAsync
        /// </summary>
        public BundleRequest<T> LoadAsync<T>() where T : Object
        {
            return BundleManager.LoadAsync<T>(BundleName, AssetName);
        }
    }
}

#if UNITY_EDITOR
namespace BundleSystem
{
    using UnityEditor;
    [CustomPropertyDrawer(typeof(BundledAssetPath))]
    public class BundledAssetPathDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 1;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var bundleName = property.FindPropertyRelative("BundleName");
            var assetName = property.FindPropertyRelative("AssetName");

            Rect r = EditorGUI.PrefixLabel(position, label);
            Rect objectFieldRect = r;

            if (objectFieldRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    Object reference = DragAndDrop.objectReferences[0];
                    string path = AssetDatabase.GetAssetPath(reference);
                    if(AssetbundleBuildSettings.EditorInstance.TryGetBundleNameAndAssetPath(path, out var foundName, out var foundPath))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    }
                    else
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    }
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    Object reference = DragAndDrop.objectReferences[0];
                    string path = AssetDatabase.GetAssetPath(reference);
                    if(AssetbundleBuildSettings.EditorInstance.TryGetBundleNameAndAssetPath(path, out var foundName, out var foundPath))
                    {
                        bundleName.stringValue = foundName;
                        assetName.stringValue = foundPath;
                    }
                    Event.current.Use();
                }
            }

            var half = objectFieldRect.width * 0.5f;
            objectFieldRect.width = half - 2;
            bundleName.stringValue = EditorGUI.TextField(objectFieldRect, bundleName.stringValue);
            objectFieldRect.x += half;
            assetName.stringValue = EditorGUI.TextField(objectFieldRect, assetName.stringValue);
        }
    }
}
#endif