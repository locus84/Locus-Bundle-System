using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleSystem
{
    [System.Serializable]
    public struct AssetReference
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
