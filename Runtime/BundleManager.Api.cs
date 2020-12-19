using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BundleSystem
{
    public static partial class BundleManager
    {
#if UNITY_EDITOR
        private static AssetbundleBuildSettings s_EditorBuildSettings;
        private static EditorAssetMap s_EditorAssetMap;

        static void SetupAssetdatabaseUsage()
        {

            s_EditorBuildSettings = AssetbundleBuildSettings.EditorInstance;
            if (s_EditorBuildSettings == null || !s_EditorBuildSettings.IsValid()) throw new System.Exception("AssetbundleBuildSetting is not valid");

            if (s_EditorBuildSettings.CleanCacheInEditor)
            {
                Caching.ClearCache();
            }

            UseAssetDatabase = !s_EditorBuildSettings.EmulateInEditor;

            //create editor asset map
            if(UseAssetDatabase)
            {
                s_EditorAssetMap = new EditorAssetMap(s_EditorBuildSettings);
                //set initialied so it does not need explit call initialzed when using aassetdatabase
                Initialized = true;
            }
        }

        public static void SetupApiTestSettings(AssetbundleBuildSettings settings = null)
        {
            if(Application.isPlaying) throw new System.Exception("This funcion cannot be called while playing!");
            if(settings == null) settings = AssetbundleBuildSettings.EditorInstance;
            if(settings == null || !settings.IsValid()) throw new System.Exception("AssetbundleBuildSetting is not valid");
            UseAssetDatabase = true;
            //create editor asset map only for testing
            s_EditorAssetMap = new EditorAssetMap(settings);
        }

        private static void EnsureAssetDatabase()
        {
            if(!Application.isPlaying && s_EditorAssetMap == null) throw new System.Exception("EditorAssetMap is null, try call SetupApiTestSettings before calling actual api in non-play mode");
        }
#endif

        public static T[] LoadAll<T>(string bundleName) where T : Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                EnsureAssetDatabase();
                var assets = s_EditorAssetMap.GetAssetPaths(bundleName);
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
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorAssetMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return null; //asset not exist
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return null;
            var loadedAsset = foundBundle.Bundle.LoadAsset<T>(assetName);
            TrackObjectInternal(loadedAsset, foundBundle);
            return loadedAsset;
        }

        public static BundleRequest<T> LoadAsync<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorAssetMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return new BundleRequest<T>((T)null); //asset not exist
                return new BundleRequest<T>(UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return null;
            var request = foundBundle.Bundle.LoadAssetAsync<T>(assetName);
            request.completed += op => AsyncAssetLoaded(request, foundBundle);
            return new BundleRequest<T>(request);
        }

        private static void AsyncAssetLoaded(AssetBundleRequest request, LoadedBundle loadedBundle)
        {
            if(request.asset != null)
            {
                TrackObjectInternal(request.asset, loadedBundle);
            }
        }

        public static void LoadScene(BundledAssetPath path, LoadSceneMode mode)
        {
            LoadScene(path.BundleName, path.AssetName, mode);
        }

        public static void LoadScene(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorAssetMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return; // scene does not exist
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(mode));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            SceneManager.LoadScene(Path.GetFileName(sceneName), mode);
        }
        
        public static AsyncOperation LoadSceneAsync(BundledAssetPath path, LoadSceneMode mode)
        {
            return LoadSceneAsync(path.BundleName, path.AssetName, mode);
        }

        public static AsyncOperation LoadSceneAsync(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorAssetMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return null; // scene does not exist
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(mode));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            return SceneManager.LoadSceneAsync(sceneName, mode);
        }

        public static bool IsAssetExist(string bundleName, string assetName)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return s_EditorAssetMap.IsAssetExist(bundleName, assetName);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return false;
            return foundBundle.Bundle.Contains(assetName);
        }
        
        public static GameObject Instantiate(GameObject original)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
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
#if UNITY_EDITOR
            if (UseAssetDatabase) 
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
#if UNITY_EDITOR
            if (UseAssetDatabase) 
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
#if UNITY_EDITOR
            if (UseAssetDatabase) 
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
#if UNITY_EDITOR
            if (UseAssetDatabase) 
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

    /// <summary>
    /// this class is for simulating assetbundle request in editor.
    /// using this class we can provide unified structure.
    /// </summary>
    public class BundleRequest<T> : CustomYieldInstruction, System.IDisposable where T : Object
    {
        AssetBundleRequest mRequest;
        T mLoadedAsset;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleRequest(AssetBundleRequest request)
        {
            mRequest = request;
        }

        /// <summary>
        /// create already ended bundle request for editor use
        /// </summary>
        /// <param name="loadedAsset"></param>
        public BundleRequest(T loadedAsset)
        {
            mLoadedAsset = loadedAsset;
        }

        //provide similar apis
        public override bool keepWaiting => mRequest == null ? false : !mRequest.isDone;
        public bool IsDone => mRequest == null ? true : mRequest.isDone;
        public T Asset => mRequest == null ? mLoadedAsset : mRequest.asset as T;
        public float Progress => mRequest == null ? 1f : mRequest.progress;

        public void Dispose()
        {
            if(mRequest != null)
            {
                if(mRequest.isDone)
                {
                    if (mRequest.asset != null) BundleManager.ReleaseObject(mRequest.asset);
                }
                else
                {
                    mRequest.completed += op =>
                    {
                        if(mRequest.asset != null) BundleManager.ReleaseObject(mRequest.asset);
                    };
                }
            }
        }
    }


    /// <summary>
    /// assetbundle update
    /// </summary>
    public class BundleAsyncOperation<T> : BundleAsyncOperation
    {
        public T Result;
    }

    public class BundleAsyncOperation : CustomYieldInstruction
    {
        public bool IsDone => ErrorCode != BundleErrorCode.NotFinished;
        public bool Succeeded => ErrorCode == BundleErrorCode.Success;
        public BundleErrorCode ErrorCode { get; private set; } = BundleErrorCode.NotFinished;
        public int TotalCount { get; private set; } = 0;
        public int CurrentCount { get; private set; } = -1;
        public float Progress { get; private set; } = 0f;
        public bool CurrentlyLoadingFromCache { get; private set; } = false;

        internal void SetCachedBundle(bool cached)
        {
            CurrentlyLoadingFromCache = cached;
        }

        internal void SetIndexLength(int total)
        {
            TotalCount = total;
        }

        internal void SetCurrentIndex(int current)
        {
            CurrentCount = current;
        }

        internal void SetProgress(float progress)
        {
            Progress = progress;
        }

        internal void Done(BundleErrorCode code)
        {
            if (code == BundleErrorCode.Success)
            {
                CurrentCount = TotalCount;
                Progress = 1f;
            }
            ErrorCode = code;
        }

        public override bool keepWaiting => !IsDone;
    }

    public enum BundleErrorCode
    {
        NotFinished = -1,
        Success = 0,
        NotInitialized = 1,
        NetworkError = 2,
        ManifestParseError = 3,
    }
}