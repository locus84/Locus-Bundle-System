using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEditor;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace BundleSystem
{
    public static class TypeLinkerGenerator
    {
        public static string Generate(AssetbundleBuildSettings settings, IBundleBuildResults result)
        {
            var linkGenerator = new LinkXmlGenerator();
            foreach(var writeResult in result.WriteResults)
            {
                linkGenerator.AddTypes(writeResult.Value.includedTypes);
            }

            var settingsPath = Application.dataPath.Remove(Application.dataPath.Length - 6) + AssetDatabase.GetAssetPath(settings);
            settingsPath = settingsPath.Remove(settingsPath.LastIndexOf('/'));
            var linkPath = $"{settingsPath}/link.xml";
            linkGenerator.Save(linkPath);
            AssetDatabase.Refresh();
            return linkPath.Remove(0, Application.dataPath.Length - 6);
        }
    }

    public class LinkXmlGenerator
    {
        Dictionary<Type, Type> m_TypeConversion = new Dictionary<Type, Type>();
        HashSet<Type> m_Types = new HashSet<Type>();

        ///<Summary>link.xmlに登録する型を追加する</Summary>
        public void AddType(Type type)
        {
            if (type == null)
                return;
            AddTypeInternal(type);
        }

        ///<Summary>link.xmlに登録する型を追加する</Summary>
        public void AddTypes(params Type[] types)
        {
            if (types == null)
                return;
            foreach (var t in types)
                AddTypeInternal(t);
        }

        ///<Summary>link.xmlに登録する型を追加する</Summary>
        public void AddTypes(IEnumerable<Type> types)
        {
            if (types == null)
                return;
            foreach (var t in types)
                AddTypeInternal(t);
        }

        private void AddTypeInternal(Type t)
        {
            if (t == null)
                return;

            Type convertedType;
            if (m_TypeConversion.TryGetValue(t, out convertedType))
                m_Types.Add(convertedType);
            else
                m_Types.Add(t);
        }

        ///<Summary>代替する型を指定する。エディターとランタイムで利用する型が違う場合に使用</Summary>
        public void SetTypeConversion(Type a, Type b)
        {
            m_TypeConversion[a] = b;
        }


        ///<Summary>link.xmlに登録するアセットを追加する</Summary>
        public void AddAsset(string assetpath)
        {
            var assets = AssetDatabase.GetDependencies(assetpath);

            List<Type> types = new List<Type>();
            foreach (var asset in assets)
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(asset);
                if (type == typeof(GameObject))
                {
                    var obj = (GameObject)AssetDatabase.LoadAssetAtPath(asset, typeof(GameObject));
                    types.AddRange(obj.GetComponentsInChildren<Component>(true).Select(c => c.GetType()));

                }
                else
                {
                    types.Add(type);
                }
            }
            AddTypes(types);
        }

        ///<Summary>link.xmlに登録するアセットを追加する</Summary>
        public void AddAssets(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
                AddAsset(assetPath);
        }

        ///<Summary>link.xmlファイルを保存する</Summary>
        public void Save(string path)
        {
            var assemblyMap = new Dictionary<Assembly, List<Type>>();
            foreach (var t in m_Types)
            {
                var a = t.Assembly;
                List<Type> types;
                if (!assemblyMap.TryGetValue(a, out types))
                    assemblyMap.Add(a, types = new List<Type>());
                types.Add(t);
            }
            XmlDocument doc = new XmlDocument();
            var linker = doc.AppendChild(doc.CreateElement("linker"));
            foreach (var k in assemblyMap)
            {
                if (k.Key.FullName.Contains("UnityEditor"))
                    continue;

                var assembly = linker.AppendChild(doc.CreateElement("assembly"));
                var attr = doc.CreateAttribute("fullname");
                attr.Value = k.Key.FullName;
                if (assembly.Attributes != null)
                {
                    assembly.Attributes.Append(attr);

                    foreach (var t in k.Value)
                    {
                        var typeEl = assembly.AppendChild(doc.CreateElement("type"));
                        var tattr = doc.CreateAttribute("fullname");
                        tattr.Value = t.FullName;
                        if (typeEl.Attributes != null)
                        {
                            typeEl.Attributes.Append(tattr);
                            var pattr = doc.CreateAttribute("preserve");
                            pattr.Value = "all";
                            typeEl.Attributes.Append(pattr);
                        }
                    }
                }
            }
            doc.Save(path);
        }
    }
}

