# Locus Bundle System For Unity

Assetbundle system from unity5 will be obsolute in future.\
Unity Addressables system provides very flexible implementation that fits on any project.\
But for my experience, there's huge learning curve to get into it.\
And also, there's no synchronized api which is familier to **Resource.Load** Users.

So here is my own bundle system that also utilizes Scriptable Build Pipline and it provides synchronized API.

This is build up to support very common senarios I've experienced.\
But you can extend this on purpose.(just fork and make modifications)

Not that it caches assetbundles so eats some memory(but quite low)

\
**Synchronized API Support!**

Main pros of Unity Addressables system is memory management.\
It unloads bundle according to bundle's reference count.\
So you don't need to call Resources.UnloadUnusedAssets() function which hangs your gameplay.

Mine support same functionality as well as synchronized api.\
This is done by caching WWWRequest.

When a assetbundle's reference count is zero.\
It fires another assetbundle request and cache up until assetbundle can be unloaded and swapped.

\
**Folder based Bundle & Local Bundles**

Like using Resources folder, you can specify folder that you want to make bundle(there's no bundle name in each asset).\
It's very comfortable for users that loves organizing contents using Folders like me.

And using local bundles, you can ship part of your bundles in player build.\
It also can be changed later on by patching.

\
**Initialization Example**
```cs
    IEnumerator Start()
    {
        //show log message
        BundleManager.LogMessages = true;

        //show some ongui elements for debugging
        BundleManager.ShowDebugGUI = true;

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

```

\
**API Examples**
```cs
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
```

There is also MessageFiber<T\> class for better performance. Take a look.
\
<br />

## Installation

Use Unity Package Manager to use it as is.
If you want to modify, clone it into your project's *Packages* folder.

## License

[MIT](https://raw.githubusercontent.com/locus84/Threading/c6f053aac6840c133dc7f2a302de8799ea6daf36/LICENSE)
