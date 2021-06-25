using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleSystem
{
    public struct TrackHandle<T> where T : Object
    {
        public int Id { get; private set; }
        public TrackHandle(int id) => Id = id;
        public bool IsValid() => Id != 0;
        public bool IsAlive() => BundleManager.IsTrackHandleAliveInternal(Id);
        public bool IsValidAndAlive() => IsValid() && IsAlive();
        public static TrackHandle<T> Invalid => new TrackHandle<T>(0);
        public void Release()
        {
            BundleManager.ReleaseHandleInternal(Id);
            Id = 0;
        }
    }

    public static partial class BundleManager
    {
        static int s_LastTrackId = 0;
        static Object s_SceneObjectDummy = new Object();
        static IndexedDictionary<int, TrackInfo> s_TrackInfoDict = new IndexedDictionary<int, TrackInfo>(10);
        static IndexedDictionary<int, float> s_LoadingAssetsDict = new IndexedDictionary<int, float>(10);
        static Dictionary<int, int> s_TrackInstanceTransformDict = new Dictionary<int, int>(10);

        //bundle ref count
        private static Dictionary<string, int> s_BundleRefCounts = new Dictionary<string, int>(10);
        private static Dictionary<string, int> s_BundleReferenceRefCounts = new Dictionary<string, int>(10);
        
        public static void GetTrackingSnapshot(Dictionary<int, TrackInfo> targetDict)
        {
            targetDict.Clear();
            if(!Application.isPlaying) return;
            s_TrackInfoDict.FillNormalDictionary(targetDict);
        } 

        public static void ChangeOwner<T>(this TrackHandle<T> handle, Component newOwner) where T : Object
        {
            if(!handle.IsValidAndAlive()) throw new System.Exception("Handle is not valid or already not tracked");
            var exitingTrackInfo = s_TrackInfoDict[handle.Id];
            exitingTrackInfo.Owner = newOwner;
            s_TrackInfoDict[handle.Id] = exitingTrackInfo;
        }

        internal static void TrackAutoReleaseInternal(int trackId) => s_LoadingAssetsDict.Add(trackId, Time.realtimeSinceStartup);
        internal static void UntrackAutoReleaseInternal(int trackId) => s_LoadingAssetsDict.Remove(trackId);
        internal static bool IsTrackHandleAliveInternal(int id) => id != 0 && s_TrackInfoDict.ContainsKey(id);

        public static TrackHandle<T> TrackExplicit<TRef, T>(this TrackHandle<TRef> referenceHandle, T objectToTrack,  Component newOwner = null)
        where TRef : Object where T : Object
        {
            if(!referenceHandle.IsValidAndAlive()) throw new System.Exception("Handle is not valid or already not tracked");

            var exitingTrackInfo = s_TrackInfoDict[referenceHandle.Id];
            var newTrackId = ++s_LastTrackId;
            if(newOwner == null) newOwner = exitingTrackInfo.Owner;

            s_TrackInfoDict.Add(newTrackId, new TrackInfo()
            {
                BundleName = exitingTrackInfo.BundleName,
                Asset = objectToTrack,
                Owner = newOwner
            });

            return new TrackHandle<T>(newTrackId);
        }

        public static void Override<T>(this TrackHandle<T> newHandle, ref TrackHandle<T> oldHandle) where T : Object
        {
            oldHandle.Release();
            oldHandle = newHandle;
        }

        public static TrackHandle<GameObject> GetInstanceTrackHandle(this GameObject gameObject) 
        {
            if(!TryGetTrackId(gameObject.transform, out var trackId))
            {
                return TrackHandle<GameObject>.Invalid;
            }
            return new TrackHandle<GameObject>(trackId);
        }

        static bool TryGetTrackId(Transform trans, out int trackId)
        {
            do
            {
                if(s_TrackInstanceTransformDict.TryGetValue(trans.GetInstanceID(), out trackId)) return true;
                trans = trans.parent;
            }
            while(trans != null);

            trackId = default;
            return false;
        }

        public struct TrackInfo
        {
            public Component Owner;
            public Object Asset;
            public string BundleName;
            public bool InstanceTrack;
        }

        private static TrackHandle<T> TrackObject<T>(Component owner, T asset, LoadedBundle loadedBundle, bool instanceTracking) where T : Object
        {
            var trackId = ++s_LastTrackId;
            s_TrackInfoDict.Add(trackId, new TrackInfo(){
                BundleName = loadedBundle.Name,
                Owner = owner,
                Asset = asset,
                InstanceTrack = instanceTracking
            });

            if(instanceTracking) s_TrackInstanceTransformDict.Add(owner.GetInstanceID(), trackId);

            RetainBundle(loadedBundle);
            return new TrackHandle<T>(trackId);
        }

        private static TrackHandle<T>[] TrackObjects<T>(Component owner, T[] assets, LoadedBundle loadedBundle) where T : Object
        {
            var result = new TrackHandle<T>[assets.Length];
            for(int i = 0; i < assets.Length; i++)
            {
                var obj = assets[i];
                var trackId = ++s_LastTrackId;
                s_TrackInfoDict.Add(trackId, new TrackInfo()
                {
                    BundleName = loadedBundle.Name,
                    Owner = owner,
                    Asset = assets[i],
                    InstanceTrack = false
                });
            }

            if(assets.Length > 0) RetainBundle(loadedBundle, assets.Length); //do once

            return result;
        }

        private static void TrackInstance(TrackInfo info, GameObject instance)
        {
            var trackId = ++s_LastTrackId;
            info.Owner = instance.transform;
            info.InstanceTrack = true;
            s_TrackInfoDict.Add(trackId, info);

            //retain
#if UNITY_EDITOR
            if(UseAssetDatabaseMap)
            {
                RetainBundleEditor(info.BundleName);
            }
            else
#endif
            //find related bundle
            if(s_AssetBundles.TryGetValue(info.BundleName, out var loadedBundle))
            {
                RetainBundle(loadedBundle);
            }

            s_TrackInstanceTransformDict.Add(instance.transform.GetInstanceID(), trackId);
        }

        internal static void ReleaseHandleInternal(int trackId)
        {
            if(trackId == 0) return;
            if(!s_TrackInfoDict.TryGetValue(trackId, out var trackInfo)) return;

            //remove anyway
            s_TrackInfoDict.Remove(trackId);

            if(trackInfo.InstanceTrack) s_TrackInstanceTransformDict.Remove(trackInfo.Owner.GetInstanceID());

            //release
#if UNITY_EDITOR
            if(UseAssetDatabaseMap)
            {
                ReleaseBundleEditor(trackInfo.BundleName);
            }
            else
#endif
            //find related bundle
            if(!s_AssetBundles.TryGetValue(trackInfo.BundleName, out var loadedBundle))
            {
                ReleaseBundle(loadedBundle);
            }
        }


        private static void RetainBundle(LoadedBundle bundle, int count = 1)
        {
            for (int i = 0; i < bundle.Dependencies.Count; i++)
            {
                var refBundleName = bundle.Dependencies[i];
                if (!s_BundleRefCounts.ContainsKey(refBundleName)) s_BundleRefCounts[refBundleName] = count;
                else s_BundleRefCounts[refBundleName] += count;
            }
        }

        private static void ReleaseBundle(LoadedBundle bundle, int count = 1)
        {
            for (int i = 0; i < bundle.Dependencies.Count; i++)
            {
                var refBundleName = bundle.Dependencies[i];
                if (s_BundleRefCounts.ContainsKey(refBundleName))
                {
                    s_BundleRefCounts[refBundleName] -= count;
                    if (s_BundleRefCounts[refBundleName] <= 0) 
                    {
                        s_BundleRefCounts.Remove(refBundleName);
                        ReloadBundle(refBundleName);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private static TrackHandle<T> TrackObjectEditor<T>(Component owner, T asset, string bundleName, bool instanceTracking) where T : Object
        {
            var trackId = ++s_LastTrackId;
            s_TrackInfoDict.Add(trackId, new TrackInfo(){
                BundleName = bundleName,
                Owner = owner,
                Asset = asset,
                InstanceTrack = instanceTracking
            });

            if(instanceTracking) s_TrackInstanceTransformDict.Add(owner.GetInstanceID(), trackId);

            RetainBundleEditor(bundleName);

            return new TrackHandle<T>(trackId);
        }

        private static TrackHandle<T>[] TrackObjectsEditor<T>(Component owner, T[] objs, string bundleName) where T : Object
        {
            var result = new TrackHandle<T>[objs.Length];
            for(int i = 0; i < objs.Length; i++)
            {
                var obj = objs[i];
                var trackId = ++s_LastTrackId;
                s_TrackInfoDict.Add(trackId, new TrackInfo()
                {
                    BundleName = bundleName,
                    Owner = owner,
                    Asset = objs[i],
                    InstanceTrack = false
                });
            }

            if(objs.Length > 0) ReleaseBundleEditor(bundleName, objs.Length);
            return result;
        }

        private static void RetainBundleEditor(string bundleName, int count = 1)
        {
            if (!s_BundleRefCounts.ContainsKey(bundleName)) s_BundleRefCounts[bundleName] = count;
            else s_BundleRefCounts[bundleName] += count;
        }

        private static void ReleaseBundleEditor(string bundleName, int count = 1)
        {
            var nextCount = s_BundleRefCounts[bundleName] - count;
            if(nextCount == 0) s_BundleRefCounts.Remove(bundleName);
            else s_BundleRefCounts[bundleName] = nextCount;
        }
#endif

        static List<GameObject> s_SceneRootObjectCache = new List<GameObject>();
        private static void TrackOnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap && s_EditorDatabaseMap.TryGetBundleNameFromSceneAssetPath(scene.path, out var bundleName))
            {
                RetainBundleEditor(bundleName);
                scene.GetRootGameObjects(s_SceneRootObjectCache);
                for (int i = 0; i < s_SceneRootObjectCache.Count; i++)
                {
                    var owner = s_SceneRootObjectCache[i].transform;
                    TrackObjectEditor(owner, s_SceneObjectDummy, bundleName, true);
                }
                s_SceneRootObjectCache.Clear();
            }
#endif
            //if scene is from assetbundle, path will be assetpath inside bundle
            if (s_SceneNames.TryGetValue(scene.path, out var loadedBundle))
            {
                RetainBundle(loadedBundle);
                scene.GetRootGameObjects(s_SceneRootObjectCache);
                for (int i = 0; i < s_SceneRootObjectCache.Count; i++)
                {
                    var owner = s_SceneRootObjectCache[i].transform;
                    TrackObject(owner, s_SceneObjectDummy, loadedBundle, true);
                }
                s_SceneRootObjectCache.Clear();
            }
        }

        private static void TrackOnSceneUnLoaded(UnityEngine.SceneManagement.Scene scene)
        {
#if UNITY_EDITOR
            if (UseAssetDatabaseMap && s_EditorDatabaseMap.TryGetBundleNameFromSceneAssetPath(scene.path, out var bundleName))
            {
                ReleaseBundleEditor(bundleName);
            }
#endif
            //if scene is from assetbundle, path will be assetpath inside bundle
            if (s_SceneNames.TryGetValue(scene.path, out var loadedBundle))
            {
                ReleaseBundle(loadedBundle);
            }
        }

        private static void Update()
        {
            {
                //we should check entire collection at least in 5 seconds, calculate trackCount for that purpose
                int trackCount = Mathf.CeilToInt(Time.unscaledDeltaTime * 0.2f * s_TrackInfoDict.Count);

                for(int i = 0; i < trackCount; i++)
                {
                    if (!s_TrackInfoDict.TryGetNext(out var kv)) continue;
                    if (kv.Value.Owner != null) continue;

                    s_TrackInfoDict.Remove(kv.Key);

                    //release bundle
#if UNITY_EDITOR
                    if(UseAssetDatabaseMap)
                    {
                        ReleaseBundleEditor(kv.Value.BundleName);
                    }
                    else
#endif
                    if (s_AssetBundles.TryGetValue(kv.Value.BundleName, out var loadedBundle)) 
                    {
                        ReleaseBundle(loadedBundle);
                    }
                }
            }

            {
                //we should check entire collection at least in 5 seconds, calculate trackCount for that purpose
                int trackCount = Mathf.CeilToInt(Time.unscaledDeltaTime * 0.2f * s_LoadingAssetsDict.Count);
                var time = Time.realtimeSinceStartup;

                for(int i = 0; i < trackCount; i++)
                {
                    if (!s_LoadingAssetsDict.TryGetNext(out var kv)) continue;
                    
                    //not loaded yet or not yet over 1sec
                    if (time - kv.Value < 1f) continue;

                    s_LoadingAssetsDict.Remove(kv.Key);
                    //release it
                    ReleaseHandleInternal(kv.Key);
                }
            }
        }

        private static void ReloadBundle(string bundleName)
        {
            if (!s_AssetBundles.TryGetValue(bundleName, out var loadedBundle))
            {
                if (LogMessages) Debug.Log("Bundle To Reload does not exist");
                return;
            }

            if(loadedBundle.IsReloading)
            {
                if (LogMessages) Debug.Log("Bundle is already reloading");
                return;
            }

            if(loadedBundle.RequestForReload != null)
            {
                loadedBundle.Bundle.Unload(true);
                loadedBundle.Bundle = DownloadHandlerAssetBundle.GetContent(loadedBundle.RequestForReload);
                //stored request needs to be disposed
                loadedBundle.RequestForReload.Dispose();
                loadedBundle.RequestForReload = null;
            }
            else
            {
                s_Helper.StartCoroutine(CoReloadBundle(bundleName, loadedBundle));
            }
        }

        static IEnumerator CoReloadBundle(string bundleName, LoadedBundle loadedBundle)
        {
            if (LogMessages) Debug.Log($"Start Reloading Bundle {bundleName}");
            var bundleReq = loadedBundle.IsLocalBundle? UnityWebRequestAssetBundle.GetAssetBundle(loadedBundle.LoadPath) : 
                UnityWebRequestAssetBundle.GetAssetBundle(loadedBundle.LoadPath, new CachedAssetBundle(bundleName, loadedBundle.Hash));

            loadedBundle.IsReloading = true;
            yield return bundleReq.SendWebRequest();
            loadedBundle.IsReloading = false;

            if (bundleReq.isNetworkError || bundleReq.isHttpError)
            {
                Debug.LogError($"Bundle reload error { bundleReq.error }");
                yield break;
            }

            if(!s_AssetBundles.TryGetValue(bundleName, out var currentLoadedBundle) || currentLoadedBundle.Hash != loadedBundle.Hash)
            {
                if (LogMessages) Debug.Log("Bundle To Reload does not exist(changed during loaing)");
                bundleReq.Dispose();
                yield break;
            }

            //if we can swap now
            if(!s_BundleRefCounts.TryGetValue(bundleName, out var refCount) || refCount == 0)
            {
                if (LogMessages) Debug.Log($"Reloaded Bundle {bundleName}");
                loadedBundle.Bundle.Unload(true);
                loadedBundle.Bundle = DownloadHandlerAssetBundle.GetContent(bundleReq);
                bundleReq.Dispose();
            }
            else
            {
                if (LogMessages) Debug.Log($"Reloaded Bundle Cached for later use {bundleName}");
                //store request for laster use
                loadedBundle.RequestForReload = bundleReq;
            }
        }
    }
}

