
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace BundleSystem{
    /// <summary>
    /// this class is for simulating assetbundle request in editor.
    /// using this class we can provide unified structure.
    /// </summary>
    public class BundleRequest<T> : CustomYieldInstruction, IAwaiter<T>, System.IDisposable where T : Object
    {
        AssetBundleRequest mRequest;
        T mLoadedAsset;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleRequest(AssetBundleRequest request)
        {
            mRequest = request;
        }

        /// <summary>
        /// create already ended bundle request for editor use
        /// </summary>
        /// <param name="loadedAsset"></param>
        public BundleRequest(T loadedAsset)
        {
            mLoadedAsset = loadedAsset;
        }

        //provide similar apis
        public override bool keepWaiting => mRequest == null ? false : !mRequest.isDone;
        public T Asset => mRequest == null ? mLoadedAsset : mRequest.asset as T;
        public float Progress => mRequest == null ? 1f : mRequest.progress;
        public bool IsCompleted => mRequest == null ? true : mRequest.isDone;

        public void Dispose()
        {
            if(mRequest != null)
            {
                if(mRequest.isDone)
                {
                    if (mRequest.asset != null) BundleManager.ReleaseObject(mRequest.asset);
                }
                else
                {
                    mRequest.completed += op =>
                    {
                        if(mRequest.asset != null) BundleManager.ReleaseObject(mRequest.asset);
                    };
                }
            }
        }

        public T GetResult() => Asset;
        public IAwaiter<T> GetAwaiter() => this;

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
            else mRequest.completed += op => continuation.Invoke();
        }
    }


    /// <summary>
    /// assetbundle update
    /// </summary>
    public class BundleAsyncOperation<T> : BundleAsyncOperationBase, IAwaiter<T>
    {
        public T Result { get; internal set; }

        //awaiter implementations
        bool IAwaiter<T>.IsCompleted => IsDone;
        T IAwaiter<T>.GetResult() => Result;
        public IAwaiter<T> GetAwaiter() => this;
        void INotifyCompletion.OnCompleted(System.Action continuation) => AwaiterOnComplete(continuation);
        void ICriticalNotifyCompletion.UnsafeOnCompleted(System.Action continuation) => AwaiterOnComplete(continuation);
    }

    public class BundleAsyncOperation : BundleAsyncOperationBase, IAwaiter
    {
        //awaiter implementations
        bool IAwaiter.IsCompleted => IsDone;
        void IAwaiter.GetResult() {}
        public IAwaiter GetAwaiter() => this;
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

    public interface IAwaiter : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }

        void GetResult();
    }
    
    public interface IAwaiter<out TResult> : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }

        TResult GetResult();
    }
}