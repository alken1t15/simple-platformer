using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelFlowMenuTests
{
    [TestMethod]
    public void InvalidMenuTransitionsDoNotResetOrReplaceSession()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        int resets = 0;
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), () => resets++);
        flow.Start("level1");
        var before = flow.Snapshot;
        foreach (Action transition in new Action[] { flow.OpenLevelSelect, flow.OpenSettings, flow.OpenSkins, flow.Back })
            Assert.ThrowsExactly<InvalidOperationException>(transition);
        Assert.AreSame(before, flow.Snapshot);
        Assert.AreEqual(1, resets);
        flow.Pause();
        Assert.ThrowsExactly<InvalidOperationException>(flow.OpenLevelSelect);
        Assert.ThrowsExactly<InvalidOperationException>(flow.OpenSkins);
        flow.OpenSettings();
        Assert.ThrowsExactly<InvalidOperationException>(flow.Continue);
        Assert.ThrowsExactly<InvalidOperationException>(flow.OpenSettings);
        flow.Back();
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        Assert.AreSame(before, flow.Snapshot);
    }

    [TestMethod]
    public void FailedResetCallbackDoesNotPublishMenuOrLoseItsReturnDestination()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        bool fail = false;
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), () =>
        {
            if (fail) throw new InvalidOperationException("reset failed");
        });
        flow.Start("level1");
        flow.Pause();
        fail = true;
        Assert.ThrowsExactly<InvalidOperationException>(flow.OpenSettings);
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        fail = false;
        flow.OpenSettings();
        fail = true;
        Assert.ThrowsExactly<InvalidOperationException>(flow.Back);
        Assert.AreEqual(AppScreen.Settings, flow.Screen);
        fail = false;
        flow.Back();
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
    }
}
