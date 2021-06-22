using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BundleSystem;
using UnityEditor;

namespace Tests
{
    public class BundleSystemTest
    {
        AssetBundleBuildSetting m_PrevActiveSettingCache = null;

        [UnitySetUp]
        public IEnumerator InitializeTestSetup()
        {
            //no setting
            if(!AssetBundleBuildSetting.TryGetActiveSetting(out var setting)) yield break;

            //load target test setting
            var toTest = AssetDatabase.LoadAssetAtPath<AssetBundleBuildSetting>("Assets/TestRemoteResources/AssetBundleBuildSetting.asset");

            if(toTest != setting)
            {
                //cache prev setting to recover
                m_PrevActiveSettingCache = setting;
                //make it active setting
                AssetBundleBuildSetting.SetActiveSetting(toTest, true);
            }

            //log messages
            BundleManager.LogMessages = true;
            BundleManager.ShowDebugGUI = true;

            //actual initialize function
            yield return BundleManager.Initialize();
            var manifestReq = BundleManager.GetManifest();

            yield return manifestReq;
            yield return BundleManager.DownloadAssetBundles(manifestReq.Result);
        }

        [TearDown]
        public void RestoreActiveSetting()
        {
            if(m_PrevActiveSettingCache != null)
            {
                //restore setting
                AssetBundleBuildSetting.SetActiveSetting(m_PrevActiveSettingCache);
            }
        }

        [Test]
        public void SyncApiTest()
        {
            {
                var tex = BundleManager.Load<Texture>("Local", "TestTexture_Local");
                Assert.NotNull(tex);
                BundleManager.ReleaseObject(tex);
            }
        }

        [UnityTest]
        public IEnumerator AsyncApiTest()
        {
            yield return null;
            Debug.Log("UnityTest");
        }
    }
}
