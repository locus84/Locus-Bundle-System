using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BundleSystem
{
    public static partial class BundleManager
    {

        public static T[] LoadAll<T>(string bundleName) where T : Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap)
            {
                EnsureAssetDatabase();
                var assets = s_EditorDatabaseMap.GetAssetPaths(bundleName);
                if (assets.Count == 0) return new T[0];

                var typeExpected = typeof(T);
                var foundList = new List<T>(assets.Count);

                for (int i = 0; i < assets.Count; i++)
                {
                    var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assets[i]);
                    if (loaded == null) continue;
                    foundList.Add(loaded);
                }
                return foundList.ToArray();
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return new T[0];
            var loadedAssets = foundBundle.Bundle.LoadAllAssets<T>();
            TrackObjectsInternal(loadedAssets, foundBundle);
            return loadedAssets;
        }


        public static T Load<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorDatabaseMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return null; //asset not exist
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return null;
            var loadedAsset = foundBundle.Bundle.LoadAsset<T>(assetName);
            if(loadedAsset != null) TrackObjectInternal(loadedAsset, foundBundle);
            return loadedAsset;
        }

        
        public static T[] LoadWithSubAssets<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorDatabaseMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return new T[0];
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                return assets.Select(a => a as T).Where(a => a != null).ToArray();
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return new T[0];
            var loadedAssets = foundBundle.Bundle.LoadAssetWithSubAssets<T>(assetName);
            TrackObjectsInternal(loadedAssets, foundBundle);
            return loadedAssets;
        }


        public static BundleRequest<T> LoadAsync<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorDatabaseMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return new BundleRequest<T>((T)null); //asset not exist
                return new BundleRequest<T>(UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return new BundleRequest<T>((T)null); //asset not exist
            var request = foundBundle.Bundle.LoadAssetAsync<T>(assetName);
            //need to keep bundle while loading, so we retain before load, release after load
            RetainBundleInternal(foundBundle, 1);
            request.completed += op => AsyncAssetLoaded(request, foundBundle);
            return new BundleRequest<T>(request);
        }

        private static void AsyncAssetLoaded(AssetBundleRequest request, LoadedBundle loadedBundle)
        {
            if(request.asset != null)
            {
                TrackObjectInternal(request.asset, loadedBundle);
            }
            
            //because we did increase ref count in loadasync function, it need to be released
            ReleaseBundleInternal(loadedBundle, 1);
        }

        public static void LoadScene(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) throw new System.Exception("This function does not support non-playing mode!");
            if (UseAssetDatabaseMap)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorDatabaseMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return; // scene does not exist
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(mode));
                return;
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            SceneManager.LoadScene(Path.GetFileName(sceneName), mode);
        }
        
        public static AsyncOperation LoadSceneAsync(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) throw new System.Exception("This function does not support non-playing mode!");
            if (UseAssetDatabaseMap)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorDatabaseMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return null; // scene does not exist
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(mode));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");

            //like default scene load functionality, we return null if something went wrong
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) 
            {
                Debug.LogError("Bundle you requested could not be found");
                return null;
            }

            //need to keep bundle while loading, so we retain before load, release after load
            var aop = SceneManager.LoadSceneAsync(Path.GetFileName(sceneName), mode);
            if(aop != null)
            {
                RetainBundleInternal(foundBundle, 1);
                aop.completed += op => ReleaseBundleInternal(foundBundle, 1);
            }

            return aop;
        }

        public static bool IsAssetExist(string bundleName, string assetName)
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return s_EditorDatabaseMap.IsAssetExist(bundleName, assetName);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return false;
            return foundBundle.Bundle.Contains(assetName);
        }
        
        public static GameObject Instantiate(GameObject original)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");

#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Transform parent)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");
            if(parent == null) throw new System.Exception("The parent transform is null");

#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, parent);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, parent);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Transform parent, bool instantiateInWorldSpace)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");
            if(parent == null) throw new System.Exception("The parent transform is null");

#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, parent, instantiateInWorldSpace);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, parent, instantiateInWorldSpace);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");

#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, position, rotation);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, position, rotation);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");
            if(parent == null) throw new System.Exception("The parent transform is null");

#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, position, rotation, parent);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, position, rotation, parent);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }
    }
}