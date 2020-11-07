using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleSystem
{
    [System.Serializable]
    public struct AssetReference
         : ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_AssetGuid;

        [SerializeField]
        private string m_BundleName;

        [SerializeField]
        private string m_PathInBundle;

        /// <summary>
        /// Load Asset
        /// </summary>
        public T Load<T>() where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if(BundleManager.UseAssetDatabase)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(m_AssetGuid));
            }
#endif
            return BundleManager.Load<T>(m_BundleName, m_PathInBundle);
        }

        /// <summary>
        /// Load AssetAsync
        /// </summary>
        public BundleRequest<T> LoadAsync<T>() where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (BundleManager.UseAssetDatabase)
            {
                return new BundleRequest<T>(Load<T>());
            }
#endif
            return BundleManager.LoadAsync<T>(m_BundleName, m_PathInBundle);
        }

        public void OnAfterDeserialize()
        {
            //DoNothing
        }


        public void OnBeforeSerialize()
        {
            Debug.Log("haha");
            ////Check only when it's not playing
            //if (!AssetbundleBuildSettings.EditorInstance.TryGetBundleNameAndAssetPath(m_AssetGuid, ref m_BundleName, ref m_PathInBundle) &&
            //    //AssetbundleBuildSettings.IsBuilding &&
            //    !string.IsNullOrEmpty(m_AssetGuid))
            //{
            //    Debug.LogError("Asset Reference is not actually included in current bundle settings");
            //}
        }

        public static bool ValidateReference(string guid)
        {
            return true;
            //var name = string.Empty;
            //var path = string.Empty;
            //return string.IsNullOrEmpty(guid) || AssetbundleBuildSettings.EditorInstance.TryGetBundleNameAndAssetPath(guid, ref name, ref path);
        }
    }
}
