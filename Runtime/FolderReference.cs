using System.IO;
using UnityEngine;
using UnityEditor.PackageManager;

namespace BundleSystem
{
    [System.Serializable]
    public class FolderReference
    {
        public string guid;
    }
}
#if UNITY_EDITOR
namespace BundleSystem
{
    using System.Threading;
    using UnityEditor;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(FolderReference))]
    public class FolderReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var guid = property.FindPropertyRelative("guid");
            var obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid.stringValue));

            GUIContent guiContent = EditorGUIUtility.ObjectContent(obj, typeof(DefaultAsset));

            Rect r = EditorGUI.PrefixLabel(position, label);

            Rect textFieldRect = r;
            textFieldRect.width -= 19f;

            GUIStyle textFieldStyle = new GUIStyle("TextField")
            {
                imagePosition = obj ? ImagePosition.ImageLeft : ImagePosition.TextOnly
            };

            if (GUI.Button(textFieldRect, guiContent, textFieldStyle) && obj)
                EditorGUIUtility.PingObject(obj);

            if (textFieldRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    Object reference = DragAndDrop.objectReferences[0];
                    string path = AssetDatabase.GetAssetPath(reference);
                    DragAndDrop.visualMode = Directory.Exists(path) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    Object reference = DragAndDrop.objectReferences[0];
                    string path = AssetDatabase.GetAssetPath(reference);
                    if (Directory.Exists(path))
                    {
                        obj = reference;
                        guid.stringValue = AssetDatabase.AssetPathToGUID(path);
                    }
                    Event.current.Use();
                }
            }

            Rect objectFieldRect = r;
            objectFieldRect.x = textFieldRect.xMax + 1f;
            objectFieldRect.width = 19f;

            if (GUI.Button(objectFieldRect, "", GUI.skin.GetStyle("IN ObjectField")))
            {
                string path = EditorUtility.OpenFolderPanel("Select a folder", string.Empty, "");
                var req = Client.List();
                while (!req.IsCompleted) Thread.Sleep(100);
                foreach(var haha in req.Result)
                {
                    Debug.Log(haha.resolvedPath);
                }
                Debug.Log(path);
                var asset = AssetDatabase.GetMainAssetTypeAtPath("Packages/com.unity.collab-proxy/Editor/Collab");
                Debug.Log(asset);
                Debug.Log(Application.dataPath);
                Debug.Log(Path.GetFullPath("Packages/"));
                Debug.Log(Directory.GetCurrentDirectory());
                if (path.Contains(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                    obj = AssetDatabase.LoadAssetAtPath(path, typeof(DefaultAsset));
                    guid.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                }
                else if(path.Contains(Application.dataPath.Remove(Application.dataPath.Length - 6) + "/Library/PackagageCache"))
                {
                }
                else if(!string.IsNullOrEmpty(path))
                {
                    Debug.LogError("The path must be in the Assets folder");
                }
            }
        }
    }
}
#endif