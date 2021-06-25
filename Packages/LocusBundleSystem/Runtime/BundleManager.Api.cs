using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BundleSystem
{
    public static partial class BundleManager
    {
        public static BundleSyncRequests<T> LoadAll<T>(this Component owner, string bundleName) where T : Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap)
            {
                EnsureAssetDatabase();
                var assets = s_EditorDatabaseMap.GetAssetPaths(bundleName);
                if (assets.Count == 0) return BundleSyncRequests<T>.Empty;

                var typeExpected = typeof(T);
                var foundList = new List<T>(assets.Count);

                for (int i = 0; i < assets.Count; i++)
                {
                    var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assets[i]);
                    if (loaded == null) continue;
                    foundList.Add(loaded);
                }
                
                var loadedAssets = foundList.ToArray();
                var handles = TrackObjectsEditor(owner, loadedAssets, bundleName);
                return new BundleSyncRequests<T>(loadedAssets, handles);
            }
            else
#endif
            {
                if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
                if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return BundleSyncRequests<T>.Empty;
                var loadedAssets = foundBundle.Bundle.LoadAllAssets<T>();
                var handles = TrackObjects(owner, loadedAssets, foundBundle);
                return new BundleSyncRequests<T>(loadedAssets, handles);
            }
        }


        public static BundleSyncRequest<T> Load<T>(this Component owner, string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorDatabaseMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return BundleSyncRequest<T>.Empty; //asset not exist
                var loadedAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if(loadedAsset == null) return BundleSyncRequest<T>.Empty;
                return new BundleSyncRequest<T>(loadedAsset, TrackObjectEditor(owner, loadedAsset, bundleName, false));
            }
            else
#endif
            {
                if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
                if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return BundleSyncRequest<T>.Empty;;
                var loadedAsset = foundBundle.Bundle.LoadAsset<T>(assetName);
                if(loadedAsset == null) return BundleSyncRequest<T>.Empty;
                return new BundleSyncRequest<T>(loadedAsset, TrackObject(owner, loadedAsset, foundBundle, false));
            }
        }

        
        public static BundleSyncRequests<T> LoadWithSubAssets<T>(this Component owner, string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorDatabaseMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return BundleSyncRequests<T>.Empty;
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var loadedAssets = assets.Select(a => a as T).Where(a => a != null).ToArray();
                var handles = TrackObjectsEditor(owner, loadedAssets, bundleName);
                return new BundleSyncRequests<T>(loadedAssets, handles);
            }
            else
#endif
            {
                if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
                if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return BundleSyncRequests<T>.Empty;
                var loadedAssets = foundBundle.Bundle.LoadAssetWithSubAssets<T>(assetName);
                var handles = TrackObjects(owner, loadedAssets, foundBundle);
                return new BundleSyncRequests<T>(loadedAssets, handles);
            }
        }


        public static BundleAsyncRequest<T> LoadAsync<T>(this Component owner, string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorDatabaseMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return BundleAsyncRequest<T>.Empty; //asset not exist
                var loadedAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if(loadedAsset == null) return BundleAsyncRequest<T>.Empty; //asset not exist
                var handle = TrackObjectEditor(owner, loadedAsset, bundleName, false);
                BundleManager.TrackAutoReleaseInternal(handle.Id);
                return new BundleAsyncRequest<T>(loadedAsset, handle);
            }
            else
#endif
            {
                if (!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
                if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return BundleAsyncRequest<T>.Empty; //asset not exist
                var request = foundBundle.Bundle.LoadAssetAsync<T>(assetName);
                //need to keep bundle while loading, so we retain before load, release after load
                var handle = TrackObject(owner, (T)null, foundBundle, false);
                var bundleRequest = new BundleAsyncRequest<T>(request, handle);
                request.completed += op => AsyncAssetLoaded(request, bundleRequest);
                return new BundleAsyncRequest<T>(request, handle);
            }
        }

        private static void AsyncAssetLoaded<T>(AssetBundleRequest request, BundleAsyncRequest<T> bundleRequest) where T : Object
        {
            var handle = bundleRequest.Handle;
            if(request.asset == null) 
            {
                handle.Release();
            } 
            else if(s_TrackInfoDict.TryGetValue(handle.Id, out var info))
            {
                info.Asset = request.asset;
                s_TrackInfoDict[handle.Id] = info;
                bundleRequest.OnTrackAutoRelease();
            }
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
            else
#endif
            {
                if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
                SceneManager.LoadScene(Path.GetFileName(sceneName), mode);
            }
        }
        
        public static BundleAsyncSceneRequest LoadSceneAsync(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) throw new System.Exception("This function does not support non-playing mode!");
            if (UseAssetDatabaseMap)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorDatabaseMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return null; // scene does not exist
                var aop = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(mode));
                if(aop == null) return null; // scene cannot be loaded
                RetainBundleEditor(bundleName);
                aop.completed += op => ReleaseBundleEditor(bundleName);
                return new BundleAsyncSceneRequest(aop);
            }
            else
#endif
            {
                if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");

                //like default scene load functionality, we return null if something went wrong
                if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) 
                {
                    Debug.LogError("Bundle you requested could not be found");
                    return null;
                }

                //need to keep bundle while loading, so we retain before load, release after load
                var aop = SceneManager.LoadSceneAsync(Path.GetFileName(sceneName), mode);
                if(aop == null) return null;

                RetainBundle(foundBundle);
                aop.completed += op => ReleaseBundle(foundBundle);

                return new BundleAsyncSceneRequest(aop);
            }
        }

        public static bool IsAssetExist(string bundleName, string assetName)
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap) 
            {
                EnsureAssetDatabase();
                return s_EditorDatabaseMap.IsAssetExist(bundleName, assetName);
            }
            else
#endif
            {
                if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
                if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return false;
                return foundBundle.Bundle.Contains(assetName);
            }
        }
        
        public static GameObject Instantiate(TrackHandle<GameObject> handle)
        {
            if(!s_TrackInfoDict.TryGetValue(handle.Id, out var info)) throw new System.Exception("Handle is notValid");
            var instance = GameObject.Instantiate(info.Asset as GameObject);
            
            TrackInstance(info, instance);
            return instance;
        }

        public static GameObject Instantiate(TrackHandle<GameObject> handle, Transform parent)
        {
            if(!s_TrackInfoDict.TryGetValue(handle.Id, out var info)) throw new System.Exception("Handle is notValid");
            var instance = GameObject.Instantiate(info.Asset as GameObject, parent);
            
            TrackInstance(info, instance);
            return instance;
        }

        public static GameObject Instantiate(TrackHandle<GameObject> handle, Transform parent, bool instantiateInWorldSpace)
        {
            if(!s_TrackInfoDict.TryGetValue(handle.Id, out var info)) throw new System.Exception("Handle is notValid");
            var instance = GameObject.Instantiate(info.Asset as GameObject, parent, instantiateInWorldSpace);
            
            TrackInstance(info, instance);
            return instance;
        }

        public static GameObject Instantiate(TrackHandle<GameObject> handle, Vector3 position, Quaternion rotation)
        {
            if(!s_TrackInfoDict.TryGetValue(handle.Id, out var info)) throw new System.Exception("Handle is notValid");
            var instance = GameObject.Instantiate(info.Asset as GameObject, position, rotation);
            
            TrackInstance(info, instance);
            return instance;
        }

        public static GameObject Instantiate(TrackHandle<GameObject> handle, Vector3 position, Quaternion rotation, Transform parent)
        {
            if(!s_TrackInfoDict.TryGetValue(handle.Id, out var info)) throw new System.Exception("Handle is notValid");
            var instance = GameObject.Instantiate(info.Asset as GameObject, position, rotation, parent);
            
            TrackInstance(info, instance);
            return instance;
        }
    }
}