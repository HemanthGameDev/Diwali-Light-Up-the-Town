using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Editor;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    public sealed class SurfacePaintTargetSetTests
    {
        private SurfacePaintTargetSet targetSet;

        [SetUp]
        public void SetUp()
        {
            targetSet = ScriptableObject.CreateInstance<SurfacePaintTargetSet>();
        }

        [TearDown]
        public void TearDown()
        {
            if (targetSet != null) Object.DestroyImmediate(targetSet);
        }

        [Test]
        public void Add_RejectsBlankAndDuplicateIds()
        {
            Assert.IsFalse(targetSet.Add(null));
            Assert.IsFalse(targetSet.Add("  "));
            Assert.IsTrue(targetSet.Add("global-id-a"));
            Assert.IsFalse(targetSet.Add("global-id-a"));
            Assert.AreEqual(1, targetSet.Count);
        }

        [Test]
        public void Remove_OnlyRemovesRequestedId()
        {
            targetSet.Add("a");
            targetSet.Add("b");
            Assert.IsTrue(targetSet.Remove("a"));
            Assert.IsFalse(targetSet.Remove("missing"));
            Assert.AreEqual(1, targetSet.Count);
            Assert.AreEqual("b", targetSet.TargetGlobalObjectIds[0]);
        }

        [Test]
        public void RemoveAt_IgnoresOutOfRangeIndices()
        {
            targetSet.Add("a");
            targetSet.RemoveAt(-1);
            targetSet.RemoveAt(1);
            Assert.AreEqual(1, targetSet.Count);
            targetSet.RemoveAt(0);
            Assert.AreEqual(0, targetSet.Count);
        }


        [Test]
        public void TargetSet_PersistsIdsThroughAssetSaveAndReload()
        {
            string path = "Assets/SurfaceWeaver/Editor/Tests/__SurfaceWeaverTargetSetPersistenceTest_" + System.Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                targetSet.Add("scene-global-id-01");
                targetSet.Add("scene-global-id-02");
                AssetDatabase.CreateAsset(targetSet, path);
                AssetDatabase.SaveAssets();
                targetSet = null; // AssetDatabase owns the object after CreateAsset.

                var loaded = AssetDatabase.LoadAssetAtPath<SurfacePaintTargetSet>(path);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(2, loaded.Count);
                Assert.AreEqual("scene-global-id-01", loaded.TargetGlobalObjectIds[0]);
                Assert.AreEqual("scene-global-id-02", loaded.TargetGlobalObjectIds[1]);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void Clear_RemovesAllIds()
        {
            targetSet.Add("a");
            targetSet.Add("b");
            targetSet.Clear();
            Assert.AreEqual(0, targetSet.Count);
        }
    }
}
