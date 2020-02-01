# Locus Bundle System For Unity

Assetbundle system from unity5 will be obsolute in future.
Unity Addressables system provides very flexible implementation that fits on any project.
But for my experience, there's huge learning curve to get into it.
And also, there's no synchronized api which is familier to **Resource.Load** Users.

So here is my own bundle system that also utilizes Scriptable Build Pipline and it provides synchronized API.

This is build up to support very common senarios I've experienced.
But you can extend this on purpose.(just fork and make modifications)

Notice! It caches assetbundles so eats some memory(but quite low)

<br />

\
**Synchronized API Support!**

Main pros of Unity Addressables system is memory management.
It unloads bundle according to bundle's reference count.
So you don't need to call Resources.UnloadUnusedAssets() function which hangs your gameplay.

Mine support same functionality as well as synchronized api.
This is done by caching WWWRequest.

When a assetbundle's reference count is zero.
It fires another assetbundle request and cache up until assetbundle can be unloaded and swapped.

\
**Folder based Bundle & Local Bundles**

Like using Resources folder, you can specify folder that you want to make bundle(there's no bundle name in each asset).
It's very comfortable for users that loves organizing contents using Folders like me.

And using local bundles, you can ship part of your bundles in player build.
It also can be changed later on by patching.

\
**Examples**
```cs
async Task SomeFunction()
{
    //this will await full async execution
    await SomeAsyncFunction("Direct call"));

    //this will await just enqueueing and 1st iteration of SomeAsyncFunction.
    //(right before another await inside function)
    await myFiber.Enqueue(() => SomeAsyncFunction("Action Style"));
}

async Task SomeAsyncFunction(string log)
{
    //You can check where is your context anytime
    Console.WriteLine(log + " : "  + myFiber.IsCurrentThread);
    // - "Direct call : false"
    // - "Action Styple : true"
    
    //if you call this function directly, call one of following to get into TaskFiber execution
    await myFiber;
    await Task.Yield().IntoFiber(myFiber);
    await myFiber.IntoFiber();

    //Now you're in myFiber's execution chain.
    Console.WriteLine(log + " : "  + myFiber.IsCurrentThread);
    // - "Direct call : true"
    // - "Action Styple : true"

    await Task.Delay(1000);
    //when calling above await keyword, the execution context will be stored
    Console.WriteLine(myFiber.IsCurrentThread);
    // - "Direct call : true"
    // - "Action Styple : true"
}
```


There is also MessageFiber<T\> class for better performance. Take a look.
\
<br />

## Installation

Download source files and include them into your project.\
Or use nuget package console.

```
PM > Install-Package Locus.Threading
```
Works too.


## License

[MIT](https://raw.githubusercontent.com/locus84/Threading/c6f053aac6840c133dc7f2a302de8799ea6daf36/LICENSE)
