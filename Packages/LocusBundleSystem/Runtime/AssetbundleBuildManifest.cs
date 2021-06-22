using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BundleSystem
{
    [System.Serializable]
    public class AssetbundleBuildManifest
    {
        public static bool TryParse(string json, out AssetbundleBuildManifest manifest)
        {
            if(string.IsNullOrEmpty(json))
            {
                manifest = default;
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<AssetbundleBuildManifest>(json);
                return true;
            }
            catch 
            {
                manifest = default;
                return false;
            }
        }

        [System.Serializable]
        public struct BundleInfo
        {
            public string BundleName;
            public bool IsLocal;
            public string HashString;

            public List<string> Dependencies;
            public long Size;

            public CachedAssetBundle ToCachedBundle() => new CachedAssetBundle(BundleName, Hash128.Parse(HashString));
        }

        public List<BundleInfo> BundleInfos = new List<BundleInfo>();
        public string BuildTarget;

        /// <summary>
        /// This does not included in hash calculation, used to find newer version between cached manifest and local manifest
        /// </summary>
        public long BuildTime;
        public string RemoteURL;
        public string GlobalHashString;

        public bool TryGetBundleInfo(string name, out BundleInfo info)
        {
            var index = BundleInfos.FindIndex(bundleInfo => bundleInfo.BundleName == name);
            info = index >= 0 ? BundleInfos[index] : default;
            return index >= 0;
        }

        public bool TryGetBundleHash(string name, out Hash128 hash)
        {
            if(TryGetBundleInfo(name, out var info))
            {
                hash = Hash128.Parse(info.HashString);
                return true;
            }
            else
            {
                hash = default;
                return false;
            }
        }

        /// <summary>
        /// Collect subset of bundleinfoes that interested, including dependencies
        /// </summary>
        public List<BundleInfo> CollectSubsetBundleInfoes(IEnumerable<string> subsetNames)
        {
            var bundleInfoDic = BundleInfos.ToDictionary(info => info.BundleName);
            var resultDic = new Dictionary<string, BundleInfo>();
            foreach(var name in subsetNames)
            {
                if(!bundleInfoDic.TryGetValue(name, out var bundle))
                {
                    if (BundleManager.LogMessages) Debug.LogWarning($"Name you provided ({name}) could not be found");
                    continue;
                }

                if (!resultDic.ContainsKey(bundle.BundleName))
                {
                    resultDic.Add(bundle.BundleName, bundle);
                }
                
                for(int i = 0; i < bundle.Dependencies.Count; i++)
                {
                    var depName = bundle.Dependencies[i];
                    if (!resultDic.ContainsKey(depName)) resultDic.Add(depName, bundleInfoDic[depName]);
                }
            }

            return resultDic.Values.ToList();
        }
    }
}
