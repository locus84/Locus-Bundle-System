using System.Collections.Generic;
using UnityEditor;

public class AssetTree
{
    public static Dictionary<string, RootNode> RootNodes = new Dictionary<string, RootNode>();
    public static Dictionary<string, Node> IndirectNodes = new Dictionary<string, Node>();
    public static List<RootNode> ResultSharedNodes = new List<RootNode>();

    public class RootNode : Node
    {
        public string BundleName { get; private set; }
        public bool IsShared { get; private set; }
        public HashSet<string> ReferencedBundleNames = new HashSet<string>();

        public RootNode(string path, string bundleName, bool isShared) : base(path, null)
        {
            IsShared = isShared;
            BundleName = bundleName;
            Root = this;
        }
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

        public void RemoveFromTree()
        {
            IndirectNodes.Remove(Path);
            foreach (var kv in Children) kv.Value.RemoveFromTree();
        }

        public void CollectChildNodes()
        {
            var childDeps = AssetDatabase.GetDependencies(Path, false);
            foreach(var child in childDeps)
            {
                //already root node, wont be included multiple times
                if (RootNodes.TryGetValue(child, out var rootNode))
                {
                    rootNode.ReferencedBundleNames.Add(child);
                    continue;
                }

                //check if it's already indirect node (skip if it's same bundle)
                //circular dependency will be blocked by indirect check
                if (IndirectNodes.TryGetValue(child, out var node))
                {
                    if (node.Root.BundleName != Root.BundleName)
                    {
                        node.RemoveFromTree();
                        var newName = $"Shared_{AssetDatabase.AssetPathToGUID(child)}";
                        var newRoot = new RootNode(child, newName, true);
                        RootNodes.Add(child, newRoot);
                        ResultSharedNodes.Add(newRoot);
                        //is it okay to do here?
                        newRoot.CollectChildNodes();
                    }
                    continue;
                }

                //if not, add to indirect node
                var childNode = new Node(child, Root);
                IndirectNodes.Add(child, childNode);
                childNode.CollectChildNodes();
            }
        }
    }
}
