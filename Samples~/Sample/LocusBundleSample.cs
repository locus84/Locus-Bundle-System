using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BundleSystem;

public class LocusBundleSample : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start()
    {
        //initialize bundle system & load local bundles
        yield return BundleManager.Initialize();

        //get download size from latest bundle manifest
        var sizeReq = BundleManager.GetDownloadSize();
        yield return sizeReq;
        if (!sizeReq.Succeeded)
        {
            //handle error
            Debug.LogError(sizeReq.ErrorCode);
        }

        Debug.Log($"Need to download {sizeReq.Result * 0.000001f } mb");

        //start downloading
        var downloadReq = BundleManager.DownloadAssetBundles();
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

    void LoadObject()
    {
        var loaded = BundleManager.Load<Texture2D>("Texture", "TestTexture");
        //do something
        BundleManager.ReleaseObject(loaded);
    }

    IEnumerator LoadObjectAsync()
    {
        {
            var loadReq = BundleManager.LoadAsync<Texture2D>("Texture", "TestTexture");
            yield return loadReq;
            //do something
            loadReq.Dispose();
        }
        
        {
            //use using clause for easier release
            using (var loadReq = BundleManager.LoadAsync<Texture2D>("Texture", "TestTexture"))
            {
                yield return loadReq;
                //do something
            }
        }
    }

    void InstantiateGameObject()
    {
    }
}
