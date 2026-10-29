using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Platformer.Tests;

[TestClass]
public sealed class PreviewSceneTests
{
    [TestMethod]
    public void SceneMapsLevelGeometryAndHasAValidCamera()
    {
        var snapshot = PreviewScene.Create();
        Assert.HasCount(PreviewLevel.Platforms.Count, snapshot.Scene.Objects);
        for (int i = 0; i < PreviewLevel.Platforms.Count; i++)
        {
            Assert.AreEqual(PreviewLevel.Platforms[i].Center, snapshot.Scene.Objects[i].Transform.Position);
            Assert.AreEqual(PreviewLevel.Platforms[i].Size, snapshot.Scene.Objects[i].Transform.Scale);
        }
        snapshot.Camera.Validate();
        var projection = snapshot.Camera.GetProjectionMatrix(16f / 9f);
        Assert.IsTrue(float.IsFinite(projection.M11));
    }

    [TestMethod]
    public void ScenesOwnTheirTransformsAndReuseMeshWithinOneScene()
    {
        var first = PreviewScene.Create();
        var second = PreviewScene.Create();
        Assert.AreSame(first.Scene.Objects[0].Mesh, first.Scene.Objects[1].Mesh);
        first.Scene.Objects[0].Transform.Position = new Vector3(999f);
        Assert.AreEqual(PreviewLevel.Platforms[0].Center, second.Scene.Objects[0].Transform.Position);
    }
}
