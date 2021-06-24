
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace BundleSystem{

    public struct BundleSyncRequest<T> : System.IDisposable where T : Object
    {
        public readonly static BundleSyncRequest<T> Empty = new BundleSyncRequest<T>(null, TrackHandle<T>.Invalid);

        public readonly TrackHandle<T> Handle;
        public readonly T Asset;

        public BundleSyncRequest(T asset, TrackHandle<T> handle)
        {
            Asset = asset;
            Handle = handle;
        }


        public void Dispose()
        {
            Handle.Release();
        }
    }

    public struct BundleSyncRequests<T> : System.IDisposable where T : Object
    {
        public readonly static BundleSyncRequests<T> Empty = new BundleSyncRequests<T>(new T[0], new TrackHandle<T>[0]);
        public readonly TrackHandle<T>[] Handles;
        public readonly T[] Assets;

        public BundleSyncRequests(T[] assets, TrackHandle<T>[] handles)
        {
            Assets = assets;
            Handles = handles;
        }

        public void Dispose()
        {
            for(int i = 0; i < Handles.Length; i++)
            {
                Handles[i].Release();
            }
        }
    }

    /// <summary>
    /// this class is for simulating assetbundle request in editor.
    /// using this class we can provide unified structure.
    /// </summary>
    public class BundleAsyncRequest<T> : CustomYieldInstruction, IAwaiter<BundleAsyncRequest<T>>, System.IDisposable where T : Object
    {
        public readonly static BundleAsyncRequest<T> Empty = new BundleAsyncRequest<T>((T)null, TrackHandle<T>.Invalid);
        public readonly TrackHandle<T> Handle;
        AssetBundleRequest m_Request;
        T m_LoadedAsset;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleAsyncRequest(AssetBundleRequest request, TrackHandle<T> handle)
        {
            m_Request = request;
            Handle = handle;
        }

        /// <summary>
        /// create already ended bundle request for editor use
        /// </summary>
        /// <param name="loadedAsset"></param>
        public BundleAsyncRequest(T loadedAsset, TrackHandle<T> handle)
        {
            m_LoadedAsset = loadedAsset;
            Handle = handle;
        }

        //provide similar apis
        public override bool keepWaiting => m_Request == null ? false : !m_Request.isDone;
        public T Asset => m_Request == null ? m_LoadedAsset : m_Request.asset as T;
        public float Progress => m_Request == null ? 1f : m_Request.progress;
        public bool IsCompleted => m_Request == null ? true : m_Request.isDone;

        public void Dispose() => Handle.Release();

        BundleAsyncRequest<T> IAwaiter<BundleAsyncRequest<T>>.GetResult() => this;
        public IAwaiter<BundleAsyncRequest<T>> GetAwaiter() => this;

        public void UnsafeOnCompleted(System.Action continuation)
        {
            OnCompleted(continuation);
        }

        public void OnCompleted(System.Action continuation)
        {
            if(Thread.CurrentThread.ManagedThreadId != BundleManager.UnityMainThreadId) 
            {
                throw new System.Exception("Should be awaited in UnityMainThread"); 
            }

            if(IsCompleted) continuation.Invoke();
            else m_Request.completed += op => continuation.Invoke();
        }

    }

    public class BundleAsyncSceneRequest : CustomYieldInstruction, IAwaiter<BundleAsyncSceneRequest>
    {
        AsyncOperation m_AsyncOperation;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleAsyncSceneRequest(AsyncOperation operation)
        {
            m_AsyncOperation = operation;
        }

        bool IAwaiter<BundleAsyncSceneRequest>.IsCompleted => m_AsyncOperation.isDone;

        public override bool keepWaiting => !m_AsyncOperation.isDone;
        public float Progress => m_AsyncOperation.progress;

        BundleAsyncSceneRequest IAwaiter<BundleAsyncSceneRequest>.GetResult() => this;
        public IAwaiter<BundleAsyncSceneRequest> GetAwaiter() => this;

        public void OnCompleted(System.Action continuation)
        {
            if(Thread.CurrentThread.ManagedThreadId != BundleManager.UnityMainThreadId) 
            {
                throw new System.Exception("Should be awaited in UnityMainThread"); 
            }

            if(m_AsyncOperation.isDone) continuation.Invoke();
            else m_AsyncOperation.completed += op => continuation.Invoke();
        }

        public void UnsafeOnCompleted(System.Action continuation) => OnCompleted(continuation);
    }


    /// <summary>
    /// assetbundle update
    /// </summary>
    public class BundleAsyncOperation<T> : BundleAsyncOperationBase, IAwaiter<BundleAsyncOperation<T>>
    {
        public T Result { get; internal set; }

        //awaiter implementations
        bool IAwaiter<BundleAsyncOperation<T>>.IsCompleted => IsDone;
        BundleAsyncOperation<T> IAwaiter<BundleAsyncOperation<T>>.GetResult() => this;
        public IAwaiter<BundleAsyncOperation<T>> GetAwaiter() => this;
        void INotifyCompletion.OnCompleted(System.Action continuation) => AwaiterOnComplete(continuation);
        void ICriticalNotifyCompletion.UnsafeOnCompleted(System.Action continuation) => AwaiterOnComplete(continuation);
    }

    public class BundleAsyncOperation : BundleAsyncOperationBase, IAwaiter<BundleAsyncOperation>
    {
        //awaiter implementations
        bool IAwaiter<BundleAsyncOperation>.IsCompleted => IsDone;
        BundleAsyncOperation IAwaiter<BundleAsyncOperation>.GetResult() => this;
        public IAwaiter<BundleAsyncOperation> GetAwaiter() => this;
        void INotifyCompletion.OnCompleted(System.Action continuation) => AwaiterOnComplete(continuation);
        void ICriticalNotifyCompletion.UnsafeOnCompleted(System.Action continuation) => AwaiterOnComplete(continuation);
    }

    public class BundleAsyncOperationBase : CustomYieldInstruction
    {
        public bool IsDone => ErrorCode != BundleErrorCode.NotFinished;
        public bool Succeeded => ErrorCode == BundleErrorCode.Success;
        public BundleErrorCode ErrorCode { get; private set; } = BundleErrorCode.NotFinished;
        public int TotalCount { get; private set; } = 0;
        public int CurrentCount { get; private set; } = -1;
        public float Progress { get; private set; } = 0f;
        public bool CurrentlyLoadingFromCache { get; private set; } = false;
        protected event System.Action m_OnComplete;
        public override bool keepWaiting => !IsDone;

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
            m_OnComplete?.Invoke();
        }

        protected void AwaiterOnComplete(System.Action continuation)
        {
            if(Thread.CurrentThread.ManagedThreadId != BundleManager.UnityMainThreadId) 
            {
                throw new System.Exception("Should be awaited in UnityMainThread"); 
            }

            if(IsDone) continuation.Invoke();
            else m_OnComplete += continuation;
        }
    }

    public enum BundleErrorCode
    {
        NotFinished = -1,
        Success = 0,
        NotInitialized = 1,
        NetworkError = 2,
        ManifestParseError = 3,
    }
    
    public interface IAwaiter<out TResult> : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }

        TResult GetResult();
    }
}