using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleSystem
{
    using System.IO;
    using UnityEditor;
    [CustomPropertyDrawer(typeof(AssetReference))]
    public class AssetReferenceDrawer : PropertyDrawer
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
            objectFieldRect.width -= 17f;
            var half = objectFieldRect.width * 0.5f;
            objectFieldRect.width = half - 2;
            bundleName.stringValue = EditorGUI.TextField(objectFieldRect, bundleName.stringValue);
            objectFieldRect.x += half;
            assetName.stringValue = EditorGUI.TextField(objectFieldRect, assetName.stringValue);

            if (!AssetbundleBuildSettings.EditorInstance.IsAssetIncluded(bundleName.stringValue, assetName.stringValue))
            {
                Rect iconRect = r;
                iconRect.x = objectFieldRect.xMax + 1f;
                iconRect.width = 17f;
                var contents = EditorGUIUtility.IconContent("d_CollabConflict Icon");
                EditorGUI.LabelField(iconRect, contents);
            }

        }
    }
}
