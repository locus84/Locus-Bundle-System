# Locus Bundle System For Unity

[![openupm](https://img.shields.io/npm/v/com.locus.bundlesystem?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.locus.bundlesystem/)

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

## Introduction Video

[![Check this out](http://img.youtube.com/vi/49WKJRscDrA/0.jpg)](http://www.youtube.com/watch?v=49WKJRscDrA "Locus Bundle System Intro")

## How to Setup 

**Assets -> Create -> Create Bundle Build Settings**

Create AssetBundleSettings ScriptableObject using Context Menu.\
This object can be anywhere under Assets folder

**Setup Bundle Informations**

![BundleSettingInspector](https://user-images.githubusercontent.com/6591432/73616925-a527b580-465c-11ea-8c82-b004e3822d98.png)

1. *Bundle List*
   - BundleName : Assetbundle's name which you should provide when loading object from AssetBundles.
   - Included In Player : if true, this bundle will be shipped with player(also can be updated).
   - Folder : Drag or select folder, assets under that folder will be packed into this bundle.
   - Include Subfolder : if true, will search assets from subfolders recurviely, your asset name when loading will be [SubFolderPath]/[AssetName]
   - Compress Bundle : if true, it will use LMZA compression. otherwise LZ4 is used. Shipped local bundles will be always LZ4
   
2. *Output Folder and URL*
   - Specify your Local/Remote bundle build output path here, also provide Remote URL for remote patch.
   
3. *Editor Functionalities*
   - Emulate In Editor : Use and Update actual assetbundles like you do in built player.
   - Emulate Without Remote URL : if true, remote bundle will be loaded from remote output path, useful when your CDN is not ready yet.
   - Clean Cache In Editor : if true, clean up cache when initializing.
   - Force Rebuild : Disables BuildCache (When Scriptable Build Pipline ignores your modification, turn it on. It barely happens though)
   
4. *Useful Utilities.*
   - Cache Server : Cache server setting for faster bundle build(you need seperate Cache server along with asset cache server)
   - Ftp : if you have ftp information, upload your remote bundle with single click.

**Build And Editor Simulation**

**Multiple Settings**

![FindActiveSetting](https://user-images.githubusercontent.com/6591432/73616927-a5c04c00-465c-11ea-9689-3b8e5cdd4970.png)\
![ActiveSetting](https://user-images.githubusercontent.com/6591432/73616924-a527b580-465c-11ea-8cff-a4bfa60faf0a.png)\
Multiple AssetbundleSettings are supported.\
You can set one of them as your active AssetbundleBuildSetting(Saved in EditorPref).\
You can find active AssetbundleBuildSetting in menu.

## API Examples
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

<br />

## Installation

### Install via OpenUPM

The package is available on the [openupm registry](https://openupm.com). It's recommended to install it via [openupm-cli](https://github.com/openupm/openupm-cli).

```
openupm add com.locus.bundlesystem
```

### Install via Git URL

Use Unity Package Manager to use it as is.\
To update to latest version, Open up your Packages/manifest.json and delete following part
```json
"lock": {
    "com.locus.bundlesystem": {
      "revision": "HEAD",
      "hash": "7e0cf885f61145eaa20a7901ef9a1cdc60d09438"
    }
  }
```
If you want to modify, clone this repo into your project's *Packages* folder.


## License

[MIT](https://raw.githubusercontent.com/locus84/Threading/c6f053aac6840c133dc7f2a302de8799ea6daf36/LICENSE)
