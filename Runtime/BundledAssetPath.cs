using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleSystem
{
    [System.Serializable]
    public struct BundledAssetPath : IBundledAssetPath
    {
        [SerializeField]
        public string BundleName;
        public string GetBundleName() => BundleName;

        [SerializeField]
        public string AssetName;
        public string GetAssetName() => AssetName;

    }

    public interface IBundledAssetPath
    {
        string GetBundleName();
        string GetAssetName();
    }

    public static class BundledAssetPathExtension
    {
        /// <summary>
        /// Load Asset
        /// </summary>
        public static T Load<T>(this IBundledAssetPath path) where T : Object
        {
            return BundleManager.Load<T>(path.GetBundleName(), path.GetAssetName());
        }
        
        /// <summary>
        /// Load AssetAsync
        /// </summary>
        public static BundleRequest<T> LoadAsync<T>(this IBundledAssetPath path) where T : Object
        {
            return BundleManager.LoadAsync<T>(path.GetBundleName(), path.GetAssetName());
        }

        /// <summary>
        /// Is specified asset exist in current bundle settings?
        /// </summary>
        public static bool Exists(this IBundledAssetPath path)
        {
            return BundleManager.IsAssetExist(path.GetBundleName(), path.GetAssetName());
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