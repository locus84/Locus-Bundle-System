using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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
            public Dictionary<string, List<string>> BundleDependencies = new Dictionary<string, List<string>>();
            public List<RootNode> SharedNodes;
        }

        public static ProcessResult ProcessDependencyTree(List<AssetBundleBuild> bundles, bool generateSharedNodes, bool folderBasedSharedGeneration, HashSet<string> localBundles)
        {
            var context = new Context() { FolderBasedSharedBundle = folderBasedSharedGeneration, GenerateSharedNodes = generateSharedNodes };
            var rootNodesToProcess = new List<RootNode>();

            //collecting reference should be done after adding all root nodes
            //if not, there might be false positive shared bundle that already exist in bundle defines
            foreach(var bundle in bundles)
            {
                var isLocal = localBundles?.Contains(bundle.assetBundleName) ?? false;
                foreach(var asset in bundle.assetNames)
                {
                    var rootNode = new RootNode(asset, bundle.assetBundleName, isLocal, false);
                    context.RootNodes.Add(asset, rootNode);
                    rootNodesToProcess.Add(rootNode);
                }
            }

            //actually analize and create shared bundles
            foreach (var node in rootNodesToProcess) node.CollectNodes(context);

            bundles.Clear();
            var dependencies = new Dictionary<string, List<string>>();
            
            foreach(var grp in context.RootNodes.Select(kv => kv.Value).GroupBy(node => node.BundleName))
            {
                var assets = grp.Select(node => node.Path).ToArray();
                bundles.Add(new AssetBundleBuild()
                {
                    assetBundleName = grp.Key,
                    assetNames = assets,
                    addressableNames = assets
                });

                var deps = grp.SelectMany(node => node.GetReferences().Select(refNode => refNode.BundleName)).Distinct().ToList();
                dependencies.Add(grp.Key, deps);
            }

            return new ProcessResult() { 
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
            public bool FolderBasedSharedBundle;
            public bool GenerateSharedNodes;
        }

        public class RootNode : Node
        {
            public string BundleName { get; private set; }
            public bool IsLocal { get; private set; }
            public bool IsShared { get; private set; }
            List<RootNode> m_ReferencedBy = new List<RootNode>();
            List<RootNode> m_References = new List<RootNode>();

            public RootNode(string path, string bundleName, bool isLocal, bool isShared) : base(path, null)
            {
                IsLocal = isLocal;
                IsShared = isShared;
                BundleName = bundleName;
                Root = this;
            }

            public void AddReference(RootNode node)
            {
                m_References.Add(node);
                node.m_ReferencedBy.Add(this);
                if(IsLocal && !node.IsLocal)
                {
                    node.IsLocal = true;
                    if(node.IsShared) BundleName += "_Local";
                }
            }

            public List<RootNode> GetReferencedBy() => m_ReferencedBy;
            public List<RootNode> GetReferences() => m_References;
        }

        public class Node
        {
            public RootNode Root { get; protected set; }
            public string Path { get; private set; }
            public Dictionary<string, Node> Children = new Dictionary<string, Node>();
            public bool IsRoot => Root == this;
            public bool HasChild => Children.Count > 0;

            public Node(string path, RootNode root)
            {
                Root = root;
                Path = path;
            }

            public void RemoveFromTree(Context context)
            {
                context.IndirectNodes.Remove(Path);
                foreach (var kv in Children) kv.Value.RemoveFromTree(context);
            }

            public void CollectNodes(Context context)
            {
                var childDeps = AssetDatabase.GetDependencies(Path, false);

                //if it's a scene unwarp placed prefab directly into the scene
                if(Path.EndsWith(".unity")) childDeps = Utility.UnwarpSceneEncodedPrefabs(Path, childDeps);

                foreach (var child in childDeps)
                {
                    //is not bundled file
                    if (!Utility.IsAssetCanBundled(child)) continue;

                    //already root node, wont be included multiple times
                    if (context.RootNodes.TryGetValue(child, out var rootNode))
                    {
                        Root.AddReference(rootNode.Root);
                        continue;
                    }

                    //check if it's already indirect node (skip if it's same bundle)
                    //circular dependency will be blocked by indirect check
                    if (context.GenerateSharedNodes && context.IndirectNodes.TryGetValue(child, out var node))
                    {
                        if (node.Root.BundleName != Root.BundleName)
                        {
                            node.RemoveFromTree(context);

                            var newName = GetSharedBundleName(child, context.FolderBasedSharedBundle);
                            var newRoot = new RootNode(child, newName, false, true);

                            //add deps
                            node.Root.AddReference(newRoot);
                            Root.AddReference(newRoot);

                            context.RootNodes.Add(child, newRoot);
                            context.ResultSharedNodes.Add(newRoot);
                            //is it okay to do here?
                            newRoot.CollectNodes(context);
                        }
                        continue;
                    }

                    //if not, add to indirect node
                    var childNode = new Node(child, Root);
                    context.IndirectNodes.Add(child, childNode);
                    Children.Add(child, childNode);
                    childNode.CollectNodes(context);
                }
            }
        }

        private static string GetSharedBundleName(string path, bool folderBased)
        {
            if(!folderBased) return $"Shared_{AssetDatabase.AssetPathToGUID(path)}";
            path = System.IO.Path.GetDirectoryName(path).Replace('/', '|').Replace('\\', '|');
            return $"Shared_{path}";
        }
    }
}
