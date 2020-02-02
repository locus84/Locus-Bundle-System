using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleSystem
{
    public static partial class BundleManager
    {
        private static HashSet<System.Type> s_NonDirectReleasableTypes = new HashSet<System.Type>() { typeof(GameObject), typeof(Component) };

        //weak refernce pool to reduce allocation
        private static Stack<System.WeakReference> s_WeakRefPool = new Stack<System.WeakReference>(50);
        //bundle ref count
        private static Dictionary<string, int> s_BundleRefCounts = new Dictionary<string, int>(10);
        //bundle ref count
        private static Dictionary<string, int> s_BundleDirectUseCount = new Dictionary<string, int>(10);

        //obect weak reference tracking
        private static IndexedDictionary<int, TrackingObject> s_TrackingObjects = new IndexedDictionary<int, TrackingObject>(50);
        //game object destroy tracking, as well as owner tracking(with original object)
        private static IndexedDictionary<TupleObjectKey, TrackingOwner> s_TrackingOwners = new IndexedDictionary<TupleObjectKey, TrackingOwner>(50);
        //game object tracking(only with loaded bundle)
        private static IndexedList<TrackingGameObject> s_TrackingGameObjects = new IndexedList<TrackingGameObject>(50);

        private struct TrackingObject //bundle with refcount, when zero, try to unload directly
        {
            public System.WeakReference WeakRef;
            public int RefCount;
            public LoadedBundle Bundle;
            public TrackingObject(Object obj, LoadedBundle loadedBundle)
            {
                if(s_WeakRefPool.Count > 0)
                {
                    WeakRef = s_WeakRefPool.Pop();
                    WeakRef.Target = obj;
                }
                else
                {
                    WeakRef = new System.WeakReference(obj);
                }
                Bundle = loadedBundle;
                RefCount = 1;
            }
        }

        private struct TrackingOwner //has original bundle so release it when destroyed
        {
            public GameObject Owner;
            public Object Child; //should own Object reference otherwise it'll be forgotton.
            public TrackingOwner(GameObject owner, Object child)
            {
                Owner = owner;
                Child = child;
            }
        }

        private struct TrackingGameObject //no original asset so it just depends directly on bundle
        {
            public GameObject GameObject;
            public LoadedBundle Bundle;
            public TrackingGameObject(GameObject obj, LoadedBundle loadedBundle)
            {
                GameObject = obj;
                Bundle = loadedBundle;
            }
        }

        private static void TrackObjectInternal(Object obj, LoadedBundle loadedBundle)
        {
            var id = obj.GetInstanceID();
            if (s_TrackingObjects.TryGetValue(id, out var trackingObject))
            {
                trackingObject.RefCount++;
            }
            else
            {
                trackingObject = new TrackingObject(obj, loadedBundle);
                RetainBundleInternal(trackingObject.Bundle, 1);
            }
            s_TrackingObjects[id] = trackingObject; //update
        }

        private static void TrackObjectsInternal<T>(T[] objs, LoadedBundle loadedBundle) where T : Object
        {
            int retainCount = 0;
            for(int i = 0; i < objs.Length; i++)
            {
                var id = objs[0].GetInstanceID();
                if (s_TrackingObjects.TryGetValue(id, out var trackingObject))
                {
                    trackingObject.RefCount++;
                }
                else
                {
                    trackingObject = new TrackingObject(objs[0], loadedBundle);
                    retainCount++;
                    
                }
                s_TrackingObjects[id] = trackingObject; //update
            }
            if(retainCount > 0) RetainBundleInternal(loadedBundle, retainCount); //do once
        }

        private static void UntrackObjectInternal(Object obj)
        {
            var id = obj.GetInstanceID();
            if (s_TrackingObjects.TryGetValue(id, out var trackingObject))
            {
                trackingObject.RefCount--;
                if (trackingObject.RefCount <= 0)
                {
                    s_TrackingObjects.Remove(id);
                    ReleaseBundleInternal(trackingObject.Bundle, 1);
                    s_WeakRefPool.Push(trackingObject.WeakRef);
                    if(!s_NonDirectReleasableTypes.Contains(obj.GetType()))
                    {
                        if (LogMessages) Debug.Log($"Unloading {obj}");
                        Resources.UnloadAsset(obj);
                    }
                }
                else
                {
                    s_TrackingObjects[id] = trackingObject; //update
                }
            }
            else
            {
                Debug.LogWarning("Object is not tracked - maybe already disposed");
            }
        }

        private static void TrackIndepenantInternal(LoadedBundle loadedBundle, GameObject go)
        {
            if (go.scene.name == null) throw new System.Exception("GameObject is not instantiated one");
            s_TrackingGameObjects.Add(new TrackingGameObject(go, loadedBundle));
            RetainBundleInternal(loadedBundle, 1);
        }

        public static void ReleaseObject(Object obj)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) return;
#endif
            UntrackObjectInternal(obj);
        }

        public static T TrackObjectWithOwner<T>(GameObject owner, T loaded) where T : Object
        {
            if (owner.scene.name == null) throw new System.Exception("GameObject is not instantiated one");
            var id = loaded.GetInstanceID();
            if (!s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Original Object is not tracked");
            var tupleKey = new TupleObjectKey(owner, loaded);
            if (s_TrackingOwners.ContainsKey(tupleKey)) throw new System.Exception("Already Tracked by this combination");
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(owner, loaded));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return loaded;
        }

        public static bool UntrackObjectWithOwner(GameObject owner, Object loaded)
        {
            var tupleKey = new TupleObjectKey(owner, loaded);
            if (!s_TrackingOwners.TryGetValue(tupleKey, out var tracking)) return false; //is not tracking combination
            s_TrackingOwners.Remove(tupleKey);
            UntrackObjectInternal(tracking.Child);
            return true;
        }

        static List<GameObject> s_SceneRootObjectCache = new List<GameObject>();
        private static void TrackOnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (s_SceneNames.TryGetValue(scene.name, out var loadedBundle))
            {
                RetainBundleInternal(loadedBundle, 1);
                scene.GetRootGameObjects(s_SceneRootObjectCache);
                for (int i = 0; i < s_SceneRootObjectCache.Count; i++)
                    TrackIndepenantInternal(loadedBundle, s_SceneRootObjectCache[i]);
                s_SceneRootObjectCache.Clear();
            }
        }

        private static void TrackOnSceneUnLoaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (s_SceneNames.TryGetValue(scene.name, out var loadedBundle))
            {
                ReleaseBundleInternal(loadedBundle, 1);
            }
        }

        private static void RetainBundleInternal(LoadedBundle bundle, int count)
        {
#if UNITY_EDITOR
            if (s_BundleDirectUseCount.ContainsKey(bundle.Name)) s_BundleDirectUseCount[bundle.Name] += count;
            else s_BundleDirectUseCount.Add(bundle.Name, count);
#endif
            for (int i = 0; i < bundle.Dependencies.Count; i++)
            {
                var refBundleName = bundle.Dependencies[i];
                if (!s_BundleRefCounts.ContainsKey(refBundleName)) s_BundleRefCounts[refBundleName] = count;
                else s_BundleRefCounts[refBundleName] += count;
            }
        }

        private static void ReleaseBundleInternal(LoadedBundle bundle, int count)
        {
#if UNITY_EDITOR
            s_BundleDirectUseCount[bundle.Name] -= count;
#endif
            for (int i = 0; i < bundle.Dependencies.Count; i++)
            {
                var refBundleName = bundle.Dependencies[i];
                if (s_BundleRefCounts.ContainsKey(refBundleName))
                {
                    s_BundleRefCounts[refBundleName] -= count;
                    if (s_BundleRefCounts[refBundleName] <= 0) ReloadBundle(refBundleName);
                }
            }
        }

        //we should check entire collection at least in 5 seconds, calculate trackCount for that purpose
        private static void Update()
        {
            //first, owner
            {
                int trackCount = Mathf.CeilToInt(Time.unscaledDeltaTime * 0.2f * s_TrackingOwners.Count);
                for(int i = 0; i < trackCount; i++)
                {
                    if (s_TrackingOwners.TryGetNext(out var kv) && kv.Value.Owner == null)
                    {
                        s_TrackingOwners.Remove(kv.Key);
                        UntrackObjectInternal(kv.Value.Child);
                    }
                }
            }

            //second, objects(so owner can release ref)
            {
                int trackCount = Mathf.CeilToInt(Time.unscaledDeltaTime * 0.2f * s_TrackingObjects.Count);
                for (int i = 0; i < trackCount; i++)
                {
                    if (s_TrackingObjects.TryGetNext(out var kv) && !kv.Value.WeakRef.IsAlive)
                    {
                        s_TrackingObjects.Remove(kv.Key);
                        ReleaseBundleInternal(kv.Value.Bundle, 1);
                        s_WeakRefPool.Push(kv.Value.WeakRef);
                    }
                }
            }

            //next, sole object
            {
                int trackCount = Mathf.CeilToInt(Time.unscaledDeltaTime * 0.2f * s_TrackingGameObjects.Count);
                for (int i = 0; i < trackCount; i++)
                {
                    if (s_TrackingGameObjects.TryGetNext(out var trackInfo) && trackInfo.GameObject == null)
                    {
                        s_TrackingGameObjects.RemoveAt(s_TrackingGameObjects.CurrentIndex);
                        ReleaseBundleInternal(trackInfo.Bundle, 1);
                    }
                }
            }
        }

        private static void ReloadBundle(string bundleName)
        {
            if (!AutoReloadBundle) return;

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

