
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace BundleSystem{
    /// <summary>
    /// this class is for simulating assetbundle request in editor.
    /// using this class we can provide unified structure.
    /// </summary>
    public class BundleRequest<T> : CustomYieldInstruction, IAwaiter<BundleRequest<T>>, System.IDisposable where T : Object
    {
        AssetBundleRequest m_Request;
        T m_LoadedAsset;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleRequest(AssetBundleRequest request)
        {
            m_Request = request;
        }

        /// <summary>
        /// create already ended bundle request for editor use
        /// </summary>
        /// <param name="loadedAsset"></param>
        public BundleRequest(T loadedAsset)
        {
            m_LoadedAsset = loadedAsset;
        }

        //provide similar apis
        public override bool keepWaiting => m_Request == null ? false : !m_Request.isDone;
        public T Asset => m_Request == null ? m_LoadedAsset : m_Request.asset as T;
        public float Progress => m_Request == null ? 1f : m_Request.progress;
        public bool IsCompleted => m_Request == null ? true : m_Request.isDone;

        public void Dispose()
        {
            if(m_Request != null)
            {
                if(m_Request.isDone)
                {
                    if (m_Request.asset != null) BundleManager.ReleaseObject(m_Request.asset);
                }
                else
                {
                    m_Request.completed += op =>
                    {
                        if(m_Request.asset != null) BundleManager.ReleaseObject(m_Request.asset);
                    };
                }
            }
        }

        BundleRequest<T> IAwaiter<BundleRequest<T>>.GetResult() => this;
        public IAwaiter<BundleRequest<T>> GetAwaiter() => this;

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

    public class BundleSceneRequest : CustomYieldInstruction, IAwaiter<BundleSceneRequest>
    {
        AsyncOperation m_AsyncOperation;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleSceneRequest(AsyncOperation operation)
        {
            m_AsyncOperation = operation;
        }

        bool IAwaiter<BundleSceneRequest>.IsCompleted => m_AsyncOperation.isDone;

        public override bool keepWaiting => !m_AsyncOperation.isDone;
        public float Progress => m_AsyncOperation.progress;

        BundleSceneRequest IAwaiter<BundleSceneRequest>.GetResult() => this;
        public IAwaiter<BundleSceneRequest> GetAwaiter() => this;

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