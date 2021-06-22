using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BundleSystem;
using UnityEditor;

namespace Tests
{
    public class BundleSystemTest
    {
        AssetbundleBuildSetting m_PrevActiveSettingCache = null;

        [UnitySetUp]
        public IEnumerator InitializeTestSetup()
        {
            //no setting
            if(!AssetbundleBuildSetting.TryGetActiveSetting(out var setting)) yield break;

            //load target test setting
            var toTest = AssetDatabase.LoadAssetAtPath<AssetbundleBuildSetting>("Assets/TestRemoteResources/AssetbundleBuildSetting.asset");

            if(toTest != setting)
            {
                //cache prev setting to recover
                m_PrevActiveSettingCache = setting;
                //make it active setting
                AssetbundleBuildSetting.SetActiveSetting(toTest, true);
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
                AssetbundleBuildSetting.SetActiveSetting(m_PrevActiveSettingCache);
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
        public IEnumerator ASyncApiTest()
        {
            yield return null;
            Debug.Log("UnityTest");
        }
    }
}
