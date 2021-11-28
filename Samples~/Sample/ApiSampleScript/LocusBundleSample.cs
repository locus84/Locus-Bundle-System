using System.Collections;
using UnityEngine;
using BundleSystem;
using System.Threading.Tasks;

public class LocusBundleSample : MonoBehaviour
{
    bool m_DownloadCancelRequested = false;

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

            //if user requests cancel
            if(m_DownloadCancelRequested) downloadReq.Cancel();

            yield return null;
        }

        if(!downloadReq.Succeeded)
        {
            //handle error
            Debug.LogError(downloadReq.ErrorCode);
        }
        //start to game
    }

    public void CancelDownload()
    {
        m_DownloadCancelRequested = true;
    }

    IEnumerator ApiSamples()
    {
        //Sync loading
        {
            var loaded = BundleManager.Load<Texture2D>("Texture", "TextureName");
            //do something
            BundleManager.ReleaseObject(loaded);
        }

        //Async loading
        {
            var loadReq = BundleManager.LoadAsync<Texture2D>("Texture", "TextureName");
            yield return loadReq;
            //do something
            loadReq.Dispose();
        }
        
        //Asnyc loading with 
        {
            //use using clause for easier release
            using (var loadReq = BundleManager.LoadAsync<Texture2D>("Texture", "TextureName"))
            {
                yield return loadReq;
                //do something
            }
        }

        //Instantiate Sync
        {
            var loaded = BundleManager.Load<GameObject>("Prefab", "PrefabName");
            //do something
            var instance = BundleManager.Instantiate(loaded);
            BundleManager.ReleaseObject(loaded);
        }

        //Instantiate Async with using clause(which is recommended, or just dispose request)
        {
            using (var loadReq = BundleManager.LoadAsync<GameObject>("Prefab", "PrefabName"))
            {
                yield return loadReq;
                var instance = BundleManager.Instantiate(loadReq.Asset);
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

    async Task AsyncAwaitSamples()
    {
        //initialize with task aupport
        {
            //show log message
            BundleManager.LogMessages = true;

            //show some ongui elements for debugging
            BundleManager.ShowDebugGUI = true;

            //initialize bundle system & load local bundles
            await BundleManager.Initialize();

            //get download size from latest bundle manifest
            var manifestReq = await BundleManager.GetManifest();
            if (!manifestReq.Succeeded)
            {
                //handle error
                Debug.LogError(manifestReq.ErrorCode);
            }

            //load asset with async/await
            using (var loadReq = await BundleManager.LoadAsync<GameObject>("Prefab", "PrefabName"))
            {
                var instance = BundleManager.Instantiate(loadReq.Asset);
            }
        }
    }

    IEnumerator AdvancedApiSamples()
    {
        //load asset and assign release when gameobject is destroyed
        {
            //use using clause for easier release
            Texture2D memberTexture;

            using (var loadReq = BundleManager.LoadAsync<Texture2D>("Texture", "TextureName"))
            {
                yield return loadReq;
                memberTexture = loadReq.Asset;
                BundleManager.TrackObjectWithOwner(gameObject, memberTexture);
            }

            //here the texture will not be unloaded until gameobject destruction.
            //so you don't have to care about it's release

            //if we need to swap the texture
            using (var loadReq = BundleManager.LoadAsync<Texture2D>("Texture", "TextureName2"))
            {
                yield return loadReq;
                //cancel owner and untrack
                BundleManager.UntrackObjectWithOwner(gameObject, memberTexture);
                memberTexture = loadReq.Asset;
                //re-track with owner with new asset
                BundleManager.TrackObjectWithOwner(gameObject, memberTexture);
            }
        }
    }
}
