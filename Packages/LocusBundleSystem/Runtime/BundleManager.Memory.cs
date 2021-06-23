using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleSystem
{
    public struct TrackHandle
    {
        public readonly int Id;
        public TrackHandle(int id) => Id = id;
        public bool IsValid() => Id != 0;
        public bool IsAlive() => BundleManager.IsTrackHandleAlive(Id);
        public bool IsValidAndAlive() => IsValid() && IsAlive();
    }

    public static partial class BundleManager
    {
        static int s_LastTrackId = 0;
        static IndexedDictionary<int, TrackInfo> s_TrackInfoDict = new IndexedDictionary<int, TrackInfo>(10);
        static Dictionary<int, int> s_TrackInstanceTransformDict = new Dictionary<int, int>();

        //bundle ref count
        private static Dictionary<string, int> s_BundleRefCounts = new Dictionary<string, int>(10);

        public static void ChangeOwner(TrackHandle handle, Component newOwner)
        {
            if(!handle.IsValidAndAlive()) throw new System.Exception("Handle is not valid or already not tracked");
            var exitingTrackInfo = s_TrackInfoDict[handle.Id];
            exitingTrackInfo.Owner = newOwner;
            s_TrackInfoDict[handle.Id] = exitingTrackInfo;
        }

        internal static bool IsTrackHandleAlive(int id)
        {
            if(id == 0) return false;
            return s_TrackInfoDict.ContainsKey(id);
        }

        public static TrackHandle TrackExplicit(Object objectToTrack, TrackHandle referenceHandle, Component newOwner = null)
        {
            if(!referenceHandle.IsValidAndAlive()) throw new System.Exception("Handle is not valid or already not tracked");

            var exitingTrackInfo = s_TrackInfoDict[referenceHandle.Id];
            var newTrackId = ++s_LastTrackId;
            if(newOwner == null) newOwner = exitingTrackInfo.Owner;

            s_TrackInfoDict.Add(newTrackId, new TrackInfo()
            {
                BundleName = exitingTrackInfo.BundleName,
                Tracked = objectToTrack,
                Owner = newOwner
            });

            return new TrackHandle(newTrackId);
        }

        static bool TryGetTrackIdInternal(Transform trans, out int trackId)
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
            public Object Tracked;
            public string BundleName;
            public bool InstanceTrack;
        }

        private static TrackHandle TrackObjectInternal(Component owner, Object obj, LoadedBundle loadedBundle, bool instanceTracking)
        {
            var trackId = ++s_LastTrackId;
            s_TrackInfoDict.Add(trackId, new TrackInfo(){
                BundleName = loadedBundle.Name,
                Owner = owner,
                Tracked = obj,
                InstanceTrack = instanceTracking
            });

            if(instanceTracking) s_TrackInstanceTransformDict.Add(owner.GetInstanceID(), trackId);

            RetainBundleInternal(loadedBundle, 1);
            return new TrackHandle(trackId);
        }

        private static TrackHandle[] TrackObjectsInternal<T>(Component owner, T[] objs, LoadedBundle loadedBundle) where T : Object
        {
            var result = new TrackHandle[objs.Length];
            for(int i = 0; i < objs.Length; i++)
            {
                var obj = objs[i];
                var trackId = ++s_LastTrackId;
                s_TrackInfoDict.Add(trackId, new TrackInfo()
                {
                    BundleName = loadedBundle.Name,
                    Owner = owner,
                    Tracked = objs[i],
                    InstanceTrack = false
                });
            }

            if(objs.Length > 0) RetainBundleInternal(loadedBundle, objs.Length); //do once

            return result;
        }

        private static void UntrackObjectInternal(TrackHandle handle)
        {
            if(!handle.IsValid()) return;
            if(!s_TrackInfoDict.TryGetValue(handle.Id, out var trackInfo)) return;

            //remove anyway
            s_TrackInfoDict.Remove(handle.Id);

            if(trackInfo.InstanceTrack) s_TrackInstanceTransformDict.Remove(trackInfo.Owner.GetInstanceID());
            //find related bundle
            if(!s_AssetBundles.TryGetValue(trackInfo.BundleName, out var loadedBundle)) return;

            //release
            ReleaseBundleInternal(loadedBundle, 1);
        }

        static List<GameObject> s_SceneRootObjectCache = new List<GameObject>();
        private static void TrackOnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            //if scene is from assetbundle, path will be assetpath inside bundle
            if (s_SceneNames.TryGetValue(scene.path, out var loadedBundle))
            {
                RetainBundleInternal(loadedBundle, 1);
                scene.GetRootGameObjects(s_SceneRootObjectCache);
                for (int i = 0; i < s_SceneRootObjectCache.Count; i++)
                {
                    var owner = s_SceneRootObjectCache[i].transform;
                    TrackObjectInternal(owner, null, loadedBundle, true);
                }
                s_SceneRootObjectCache.Clear();
            }
        }

        private static void TrackOnSceneUnLoaded(UnityEngine.SceneManagement.Scene scene)
        {
            //if scene is from assetbundle, path will be assetpath inside bundle
            if (s_SceneNames.TryGetValue(scene.path, out var loadedBundle))
            {
                ReleaseBundleInternal(loadedBundle, 1);
            }
        }

        private static void RetainBundleInternal(LoadedBundle bundle, int count)
        {
            for (int i = 0; i < bundle.Dependencies.Count; i++)
            {
                var refBundleName = bundle.Dependencies[i];
                if (!s_BundleRefCounts.ContainsKey(refBundleName)) s_BundleRefCounts[refBundleName] = count;
                else s_BundleRefCounts[refBundleName] += count;
            }
        }

        private static void ReleaseBundleInternal(LoadedBundle bundle, int count)
        {
            for (int i = 0; i < bundle.Dependencies.Count; i++)
            {
                var refBundleName = bundle.Dependencies[i];
                if (s_BundleRefCounts.ContainsKey(refBundleName))
                {
                    s_BundleRefCounts[refBundleName] = count;
                    if (s_BundleRefCounts[refBundleName] <= 0) 
                    {
                        s_BundleRefCounts.Remove(refBundleName);
                        ReloadBundle(refBundleName);
                    }
                }
            }
        }

        //we should check entire collection at least in 5 seconds, calculate trackCount for that purpose
        private static void Update()
        {
            //first, owner
            {
                int trackCount = Mathf.CeilToInt(Time.unscaledDeltaTime * 0.2f * s_TrackInfoDict.Count);
                for(int i = 0; i < trackCount; i++)
                {
                    if (s_TrackInfoDict.TryGetNext(out var kv) && kv.Value.Owner == null)
                    {
                        s_TrackInfoDict.Remove(kv.Key);
                        //release bundle
                    }
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
                s_Helper.StartCoroutine(ReloadBundle(bundleName, loadedBundle));
            }
        }

        static IEnumerator ReloadBundle(string bundleName, LoadedBundle loadedBundle)
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

