using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BundleSystem;
using UnityEditor;
using System.Threading.Tasks;

namespace Tests
{
    public class BundleSystemTest
    {
        AssetBundleBuildSetting m_PrevActiveSettingCache = null;
        Component m_Owner;

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

            m_Owner = new GameObject("Owner").transform;
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
            var texReq = m_Owner.Load<Texture>("Local", "TestTexture_Local");
            Assert.NotNull(texReq.Asset);
        }

        [UnityTest]
        public IEnumerator AsyncApiTest()
        {
            yield return null;
            Debug.Log("UnityTest");
        }

        [UnityTest]
        public IEnumerator TaskAsyncApiTest()
        {
            var task = TaskAsyncApiTestFunction();
            while(!task.IsCompleted) yield return null;
        }

        private async Task TaskAsyncApiTestFunction()
        {
            await Task.Delay(1000);
            Assert.NotNull(await m_Owner.LoadAsync<Texture>("Local", "TestTexture_Local"));
        }
    }
}
