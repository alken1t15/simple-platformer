using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
namespace Platformer.Tests;
[TestClass]
public sealed class MachineArtTests
{
    [TestMethod]
    public void MachineMeshesStayInsidePhysicalBoundsAndHaveOutwardFaces()
    {
        var catalog=GameLevels.CreateCatalog();
        foreach(string id in new[]{"level2","piston-foundry","pendulum-gorge","winter-icicle","jump-circuit"})
        {
            var presenter=new GameScenePresenter(catalog.Get(id),true);
            foreach(var item in presenter.HazardObjects.Values)
            {
                foreach(var v in item.Mesh.Vertices)
                {
                    Assert.IsLessThanOrEqualTo(0.5001f, MathF.Max(MathF.Abs(v.Position.X),MathF.Max(MathF.Abs(v.Position.Y),MathF.Abs(v.Position.Z))));
                    Assert.IsTrue(float.IsFinite(v.Uv.X)&&float.IsFinite(v.Uv.Y));
                }
                for(int i=0;i<item.Mesh.IndexCount;i+=3)
                {
                    var a=item.Mesh.Vertices[(int)item.Mesh.Indices[i]];
                    var b=item.Mesh.Vertices[(int)item.Mesh.Indices[i+1]];
                    var c=item.Mesh.Vertices[(int)item.Mesh.Indices[i+2]];
                    Assert.IsGreaterThan(0f, Vector3.Dot(Vector3.Cross(b.Position-a.Position,c.Position-a.Position),a.Normal));
                }
                Assert.IsNotNull(item.Material.Texture);
            }
        }
    }
}