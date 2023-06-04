using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if BUNDLE_SPRITE_ATLAS
using UnityEditor.U2D;
using UnityEngine.U2D;
#endif

namespace BundleSystem
{
    /// <summary>
    /// this class finds out duplicated topmost assets
    /// and make them into one single shared bundle one by one(to reduce bundle rebuild)
    /// so that there would be no asset duplicated
    /// </summary>
    public static class AssetDependencyTree
    {

        public class ProcessResult
        {
            public List<AssetBundleBuild> Results;
            public Dictionary<string, List<string>> BundleDependencies = new Dictionary<string, List<string>>();
            public List<RootNode> SharedNodes;
        }

        public static ProcessResult ProcessDependencyTree(IEnumerable<AssetBundleBuild> bundles, ISharedBundleSetting sharedBundleSetting = null)
        {
            var context = new Context() { SharedSetting = sharedBundleSetting ?? new DefaultSharedBundleSetting() };

#if BUNDLE_SPRITE_ATLAS
            context.AtlasedSprites = AssetDatabase.FindAssets("t:spriteatlas")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => SpriteAtlasExtensions.IsIncludeInBuild(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path)))
                .SelectMany(path => AssetDatabase.GetDependencies(path, true).Where(dep => dep != path).Select(dep => (path, dep)))
                .GroupBy(tuple => tuple.dep)
                .ToDictionary(grp => grp.Key, grp => grp.Select(tuple => tuple.path).ToList());
#endif

            var rootNodesToProcess = new List<RootNode>();
            var originalBundles = new HashSet<string>();

            //collecting reference should be done after adding all root nodes
            //if not, there might be false positive shared bundle that already exist in bundle defines
            foreach (var bundle in bundles)
            {
                if (bundle.assetBundleName.StartsWith(sharedBundleSetting.SharedPrefix)) throw new System.Exception($"Bundle name should not start with \"Shared\" : {bundle.assetBundleName}");
                originalBundles.Add(bundle.assetBundleName);
                var isLocal = context.SharedSetting.IsLocalBundle(bundle.assetBundleName);
                foreach (var asset in bundle.assetNames)
                {
                    var rootNode = RootNode.CreateExisting(asset, bundle.assetBundleName, isLocal);
                    context.RootNodes.Add(asset, rootNode);
                    rootNodesToProcess.Add(rootNode);
                }
            }

            //actually analize and create shared bundles
            foreach (var node in rootNodesToProcess) node.CollectNodes(context);
            foreach (var sharedNode in context.ResultSharedNodes) 
            {
                var bundleName = context.SharedSetting.GetSharedBundleName(sharedNode.Path, sharedNode.IsLocal);
                sharedNode.SetBundleName($"{context.SharedSetting.SharedPrefix}{bundleName}");
            }

            var dependencies = new Dictionary<string, List<string>>();
            
            var resultBundles = new List<AssetBundleBuild>();
            resultBundles.AddRange(bundles);

            foreach (var grp in context.RootNodes.Select(kv => kv.Value).GroupBy(node => node.BundleName))
            {
                //we do not touch original bundles, as we don't do any modifiation there
                if (!originalBundles.Contains(grp.Key))
                {
                    var assets = grp.Select(node => node.Path).ToArray();
                    resultBundles.Add(new AssetBundleBuild()
                    {
                        assetBundleName = grp.Key,
                        assetNames = assets,
                        addressableNames = assets
                    });
                }

                var deps = grp.SelectMany(node => node.GetReferences().Select(refNode => refNode.BundleName)).Distinct().ToList();
                dependencies.Add(grp.Key, deps);
            }

            return new ProcessResult()
            {
                Results = resultBundles,
                BundleDependencies = dependencies,
                SharedNodes = context.ResultSharedNodes
            };
        }

        //actual node tree context
        public class Context
        {
            public Dictionary<string, RootNode> RootNodes = new Dictionary<string, RootNode>();
            public Dictionary<string, Node> IndirectNodes = new Dictionary<string, Node>();
            public List<RootNode> ResultSharedNodes = new List<RootNode>();
            public ISharedBundleSetting SharedSetting;
#if BUNDLE_SPRITE_ATLAS
            public Dictionary<string, List<string>> AtlasedSprites = new Dictionary<string, List<string>>();
#endif
        }

        public class RootNode : Node
        {
            public string BundleName { get; private set; }
            public bool IsLocal { get; private set; }
            public bool IsShared { get; private set; }
            List<RootNode> m_ReferencedBy = new List<RootNode>();
            List<RootNode> m_References = new List<RootNode>();

            public static RootNode CreateExisting(string path, string bundleName, bool isLocal)
            {
                var node = new RootNode(path);
                
                node.BundleName = bundleName;
                node.IsShared = false;
                node.IsLocal = isLocal;
                return node;
            }

            public static RootNode CreateShared(string path)
            {
                var node = new RootNode(path);
                node.IsShared = true;
                node.IsLocal = false;
                return node;
            }

            private RootNode(string path) : base(path, null) => Root = this;

            public void SetBundleName(string bundleName) => BundleName = bundleName;

            public void AddReference(RootNode node)
            {
                m_References.Add(node);
                node.m_ReferencedBy.Add(this);
                if (IsLocal) node.IsLocal = true;
            }

            public List<RootNode> GetReferencedBy() => m_ReferencedBy;
            public List<RootNode> GetReferences() => m_References;
        }

        public class Node
        {
            public RootNode Root { get; protected set; }
            public string Path { get; private set; }
            public bool IsRoot => Root == this;
            public Node(string path, RootNode root)
            {
                Root = root;
                Path = path;
            }

            public void RemoveFromTree(Context context)
            {
                context.IndirectNodes.Remove(Path);
            }

            public void CollectNodes(Context context, bool fromAtlas = false)
            {
                var childDeps = AssetDatabase.GetDependencies(Path, false);

#if BUNDLE_SPRITE_ATLAS
                //if sprite atlas, set from atlas to true
                //to prevent stack overflow
                if(Path.EndsWith(".spriteatlas")) fromAtlas = true;

                if(!fromAtlas && context.AtlasedSprites.TryGetValue(Path, out var atlases))
                {
                    childDeps = childDeps.Union(atlases).Distinct().ToArray();
                }
#endif

                //if it's a scene unwarp placed prefab directly into the scene
                if (Path.EndsWith(".unity")) childDeps = UnwarpSceneEncodedPrefabs(Path, childDeps);

                foreach (var child in childDeps)
                {
                    //is not bundled file
                    if (!IsAssetCanBundled(child)) continue;

                    //already root node, wont be included multiple times
                    if (context.RootNodes.TryGetValue(child, out var rootNode))
                    {
                        Root.AddReference(rootNode.Root);
                        continue;
                    }

                    //does not participate shared bundle generation
                    if(!context.SharedSetting.AllowSharedBundle(child))
                    {
                        continue;
                    }

                    //check if it's already indirect node
                    //circular dependency will be blocked by indirect check
                    if(!context.IndirectNodes.TryGetValue(child, out var node))
                    {
                        //if not, add to indirect node
                        var childNode = new Node(child, Root);
                        context.IndirectNodes.Add(child, childNode);

                        //continue collect
                        childNode.CollectNodes(context, fromAtlas);
                        continue;
                    }

                    //skip if it's from same bundle
                    if (node.Root.BundleName == Root.BundleName) continue;

                    context.IndirectNodes.Remove(child);
                    var newRoot = RootNode.CreateShared(child);

                    //add deps
                    node.Root.AddReference(newRoot);
                    Root.AddReference(newRoot);

                    context.RootNodes.Add(child, newRoot);
                    context.ResultSharedNodes.Add(newRoot);

                    //process new root
                    newRoot.CollectNodes(context, fromAtlas);
                }
            }
        }

        private static bool IsAssetCanBundled(string assetPath)
        {
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            return mainType != null && mainType != typeof(MonoScript) && mainType != typeof(DefaultAsset) && mainType.IsSubclassOf(typeof(Object));
        }

        private static string[] UnwarpSceneEncodedPrefabs(string scenePath, string[] sceneDeps)
        {
            var list = new List<string>(sceneDeps);
            var settings = new UnityEditor.Build.Content.BuildSettings();
            settings.target = EditorUserBuildSettings.activeBuildTarget;
            settings.group = BuildPipeline.GetBuildTargetGroup(settings.target);
            var usageTags = new UnityEditor.Build.Content.BuildUsageTagSet();
            var depsCache = new UnityEditor.Build.Content.BuildUsageCache();

            //extract deps form scriptable build pipeline
            var sceneInfo = UnityEditor.Build.Content.ContentBuildInterface.CalculatePlayerDependenciesForScene(scenePath, settings, usageTags, depsCache);

            //this is needed as calculate function actumatically pops up progress bar
            EditorUtility.ClearProgressBar();

            //we do care only prefab
            var hashSet = new HashSet<string>();
            foreach (var objInfo in sceneInfo.referencedObjects)
            {
                if (objInfo.fileType != UnityEditor.Build.Content.FileType.MetaAssetType) continue;
                var path = AssetDatabase.GUIDToAssetPath(objInfo.guid.ToString());
                if (!path.EndsWith(".prefab")) continue;
                hashSet.Add(path);
            }

            //remove direct reference of the prefab and append the deps of the prefab we removed
            var appendList = new List<string>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var child = list[i];
                if (AssetDatabase.GetMainAssetTypeAtPath(child) != typeof(UnityEngine.GameObject)) continue;
                if (hashSet.Contains(child)) continue;
                list.RemoveAt(i);
                var deps = AssetDatabase.GetDependencies(child, false);
                appendList.AddRange(deps);
            }

            //append we found into original list except prefab itself
            list.AddRange(appendList);

            //remove duplicates and return
            return list.Distinct().ToArray();
        }


        private class DefaultSharedBundleSetting : ISharedBundleSetting
        {
            public string GetSharedBundleName(string assetPath, bool isLocal)
            {
                var bundleName = System.IO.Path.GetDirectoryName(assetPath).Replace('/', '_').Replace('\\', '_');
                return bundleName + (isLocal? "_Local" : string.Empty);
            }
        }
    }

    public interface ISharedBundleSetting
    {
        string SharedPrefix => "Shared_";
        bool IsLocalBundle(string bundleName) => false;
        bool AllowSharedBundle(string assetPath) => true;
        string GetSharedBundleName(string assetPath, bool isLocal);
    }
}
