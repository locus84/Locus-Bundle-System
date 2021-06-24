using System.Collections;
using UnityEngine;
using BundleSystem;

public class LocusBundleSample : MonoBehaviour
{
    IEnumerator Start()
    {
        //show log message
        BundleManager.LogMessages = true;

        //show some ongui elements for debugging
        BundleManager.ShowDebugGUI = true;

        //initialize bundle system & load local bundles
        yield return BundleManager.Initialize();

        //get download size from latest bundle manifest
        var manifestReq = BundleManager.GetManifest();
        yield return manifestReq;
        if (!manifestReq.Succeeded)
        {
            //handle error
            Debug.LogError(manifestReq.ErrorCode);
        }

        Debug.Log($"Need to download { BundleManager.GetDownloadSize(manifestReq.Result) * 0.000001f } mb");

        //start downloading
        var downloadReq = BundleManager.DownloadAssetBundles(manifestReq.Result);
        while(!downloadReq.IsDone)
        {
            if(downloadReq.CurrentCount >= 0)
            {
                Debug.Log($"Current File {downloadReq.CurrentCount}/{downloadReq.TotalCount}, " +
                    $"Progress : {downloadReq.Progress * 100}%, " +
                    $"FromCache {downloadReq.CurrentlyLoadingFromCache}");
            }
            yield return null;
        }

        if(!downloadReq.Succeeded)
        {
            //handle error
            Debug.LogError(downloadReq.ErrorCode);
        }
        //start to game
    }


    IEnumerator ApiSamples()
    {
        //Sync loading
        {
            var loaded = this.Load<Texture2D>("Texture", "TextureName");
            //do something
            loaded.Handle.Release();
        }

        //Async loading
        {
            var loadReq = this.LoadAsync<Texture2D>("Texture", "TextureName");
            yield return loadReq;
            //do something
            loadReq.Dispose();
        }
        
        //Asnyc loading with 
        {
            //use using clause for easier release
            using (var loadReq = this.LoadAsync<Texture2D>("Texture", "TextureName"))
            {
                yield return loadReq;
                //do something
            }
        }

        //Instantiate Sync
        {
            var loaded = this.Load<GameObject>("Prefab", "PrefabName");
            //do something
            var instance = BundleManager.Instantiate(loaded.Handle);
            loaded.Handle.Release();
        }

        //Instantiate Async with using clause(which is recommended, or just dispose request)
        {
            using (var loadReq = this.LoadAsync<GameObject>("Prefab", "PrefabName"))
            {
                yield return loadReq;
                var instance = BundleManager.Instantiate(loadReq.Handle);
            }
        }

        //load scene
        {
            //Sync
            BundleManager.LoadScene("Scene", "SomeScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            //Async
            yield return BundleManager.LoadSceneAsync("Scene", "SomeScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }


    IEnumerator AdvancedApiSamples()
    {
        //load asset and assign release when gameobject is destroyed
        {
            //use using clause for easier release
            Texture2D memberTexture;

            using (var loadReq = this.LoadAsync<Texture2D>("Texture", "TextureName"))
            {
                yield return loadReq;
                memberTexture = loadReq.Asset;
            }
            //here the texture will not be unloaded until gameobject destruction.
            //so you don't have to care about it's release

            //if we need to swap the texture
            using (var loadReq = this.LoadAsync<Texture2D>("Texture", "TextureName2"))
            {
                yield return loadReq;
                //cancel owner and untrack
                // BundleManager.UntrackObjectWithOwner(gameObject, memberTexture);
                // memberTexture = loadReq.Asset;
                // //re-track with owner with new asset
                // BundleManager.TrackObjectWithOwner(gameObject, memberTexture);
            }
        }
    }
}
