using System.Numerics;
using Platformer.Core;

namespace Platformer.Tests;

internal static class SessionFixtures
{
    internal static readonly GameCommand Right = new(Vector2.UnitX, false, false);
    internal static LevelDefinition Level(string id = "level1", IReadOnlyList<CoinDefinition>? coins = null,
        float finishX = 2f, bool floor = true, float floorHalfWidth = 20f) =>
        new(id, new Vector3(0f, 0.501f, 0f), -3f,
            new OrientedBox(new Vector3(finishX, 0.5f, 0f), new Vector3(0.1f, 0.75f, 1f), Quaternion.Identity),
            floor ? [new SurfaceDefinition("floor", new OrientedBox(new Vector3(0f, -0.5f, 0f),
                new Vector3(floorHalfWidth, 0.5f, 20f), Quaternion.Identity))] : [], coins ?? [], []);

    internal static void Until(Func<bool> done, Action step, int maximum = 500)
    {
        for (int i = 0; i < maximum && !done(); i++) step();
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(done(), "Expected state not reached within bounded ticks");
    }
}
