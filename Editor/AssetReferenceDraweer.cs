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
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var guid = property.FindPropertyRelative("m_AssetGuid");
            var obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid.stringValue));

            Rect r = EditorGUI.PrefixLabel(position, label);
            Rect objectFieldRect = r;

            if(!AssetReference.ValidateReference(guid.stringValue))
            {
                objectFieldRect.width -= 17f;
                Rect iconRect = r;
                iconRect.x = objectFieldRect.xMax + 1f;
                iconRect.width = 17f;
                var contents = EditorGUIUtility.IconContent("d_CollabConflict Icon");
                EditorGUI.LabelField(iconRect, contents);
            }

            var newObj = EditorGUI.ObjectField(objectFieldRect, obj, typeof(Object), false);

            if (newObj != obj)
            {
                if(newObj == null)
                {
                    guid.stringValue = string.Empty;
                }
                else
                {
                    var path = AssetDatabase.GetAssetPath(newObj);
                    if (Utility.IsAssetCanBundled(path)) guid.stringValue = AssetDatabase.AssetPathToGUID(path);
                }
            }
        }
    }
}
