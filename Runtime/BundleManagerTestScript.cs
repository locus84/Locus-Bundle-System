using System.Collections;
using UnityEngine;
using BundleSystem;

public class BundleManagerTestScript : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start()
    {
        yield return BundleManager.Instance.Initialize();
        var sizeReq = BundleManager.Instance.GetDownloadSize();
        yield return sizeReq;
        Debug.Log(sizeReq.Result);
        yield return BundleManager.Instance.DownloadAssetBundles();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
