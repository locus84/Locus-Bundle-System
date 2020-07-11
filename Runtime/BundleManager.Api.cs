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
        private static Dictionary<string, Dictionary<string, List<string>>> s_AssetListForEditor = new Dictionary<string, Dictionary<string, List<string>>>();

        static List<string> s_EmptyStringList = new List<string>();

        static void SetupAssetdatabaseUsage()
        {
            s_EditorBuildSettings = AssetbundleBuildSettings.EditorInstance;
            if (s_EditorBuildSettings == null || !s_EditorBuildSettings.IsValid()) throw new System.Exception("AssetbundleBuildSetting is not valid");

            if (s_EditorBuildSettings.CleanCacheInEditor)
            {
                Caching.ClearCache();
            }

            UseAssetDatabase = !s_EditorBuildSettings.EmulateInEditor;

            //when using assetbundle tag, we don't need to pre-collect assets
            if(UseAssetDatabase && !s_EditorBuildSettings.UseAssetBundleLabel)
            {
                var assetPath = new List<string>();
                var loadPath = new List<string>();
                foreach (var setting in s_EditorBuildSettings.BundleSettings)
                {
                    assetPath.Clear();
                    loadPath.Clear();
                    var folderPath = UnityEditor.AssetDatabase.GUIDToAssetPath(setting.Folder.guid);
                    var dir = new DirectoryInfo(Path.Combine(Application.dataPath, folderPath.Remove(0, 7)));
                    Utility.GetFilesInDirectory(string.Empty, assetPath, loadPath, dir, setting.IncludeSubfolder);
                    var assetList = new Dictionary<string, List<string>>();
                    for(int i = 0; i < assetPath.Count; i++)
                    {
                        if(assetList.TryGetValue(loadPath[i], out var list))
                        {
                            list.Add(assetPath[i]);
                            continue;
                        }
                        assetList.Add(loadPath[i], new List<string>() { assetPath[i] });
                    }
                    s_AssetListForEditor.Add(setting.BundleName, assetList);
                }
            }
        }

        static List<string> GetAssetPathsFromAssetBundleAndAssetName(string bundleName, string assetName)
        {
            if (s_EditorBuildSettings.UseAssetBundleLabel) return new List<string>(UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(bundleName, assetName));
            if (!s_AssetListForEditor.TryGetValue(bundleName, out var innerDic)) return s_EmptyStringList;
            if (!innerDic.TryGetValue(assetName, out var pathList)) return s_EmptyStringList;
            return pathList;
        }

        static List<string> GetAssetPathsFromAssetBundle(string bundleName)
        {
            if (s_EditorBuildSettings.UseAssetBundleLabel) return new List<string>(UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundle(bundleName));
            if (!s_AssetListForEditor.TryGetValue(bundleName, out var innerDic)) return s_EmptyStringList;
            return innerDic.Values.SelectMany(list => list).ToList();
        }
#endif

        public static T[] LoadAll<T>(string bundleName) where T : Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                var assets = GetAssetPathsFromAssetBundle(bundleName);
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
                var assets = GetAssetPathsFromAssetBundleAndAssetName(bundleName, assetName);
                if (assets.Count == 0) return null;

                var typeExpected = typeof(T);
                var foundIndex = 0;

                for (int i = 0; i < assets.Count; i++)
                {
                    var foundType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assets[i]);
                    if (foundType == typeExpected || foundType.IsSubclassOf(typeExpected))
                    {
                        foundIndex = i;
                        break;
                    }
                }
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assets[foundIndex]);
            }
#endif
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return null;
            var loadedAsset = foundBundle.Bundle.LoadAsset<T>(assetName);
            TrackObjectInternal(loadedAsset, foundBundle);
            return loadedAsset;
        }

        public static BundleRequest<T> LoadAsync<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) return new BundleRequest<T>(Load<T>(bundleName, assetName));
#endif
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


        public static void LoadScene(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                var assets = GetAssetPathsFromAssetBundleAndAssetName(bundleName, sceneName);
                if (assets.Count == 0 || UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assets[0]) == typeof(Scene))
                {
                    Debug.LogError("Request scene name does not exist in streamed scenes : " + sceneName);
                    return;
                }

                //this loads scene from playmode
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(assets[0], new LoadSceneParameters(mode));
                return;
            }
#endif
            SceneManager.LoadScene(sceneName, mode);
        }

        public static AsyncOperation LoadSceneAsync(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                var assets = GetAssetPathsFromAssetBundleAndAssetName(bundleName, sceneName);
                if (assets.Count == 0 || UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assets[0]) == typeof(Scene))
                {
                    Debug.LogError("Request scene name does not exist in streamed scenes : " + sceneName);
                    return null;
                }

                //this loads scene from playmode
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(assets[0], new LoadSceneParameters(mode));
            }
#endif
            return SceneManager.LoadSceneAsync(sceneName, mode);
        }

        public static bool IsAssetExist(string bundleName, string assetName)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                var assets = GetAssetPathsFromAssetBundleAndAssetName(bundleName, assetName);
                return assets.Count > 0;
            }

#endif
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return false;
            return foundBundle.AssetNames.Contains(assetName);
        }

        public static GameObject Instantiate(GameObject original)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) return GameObject.Instantiate(original);
#endif
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
            if (UseAssetDatabase) return GameObject.Instantiate(original, parent);
#endif
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
            if (UseAssetDatabase) return GameObject.Instantiate(original, parent, instantiateInWorldSpace);
#endif
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
            if (UseAssetDatabase) return GameObject.Instantiate(original, position, rotation);
#endif
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
            if (UseAssetDatabase) return GameObject.Instantiate(original, position, rotation, parent);
#endif
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