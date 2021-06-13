using BundleSystem;
using System.Collections;
using UnityEngine;

public class SampleGameManager : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);

        //show log message
        BundleManager.LogMessages = true;

        //show some ongui elements for debugging
        BundleManager.ShowDebugGUI = true;

        //initialize
        yield return BundleManager.Initialize();

        //go to local TitleScene
        BundleManager.LoadScene("LocalScene", "LocalTitleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}
