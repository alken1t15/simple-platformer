using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelCatalogTests
{
    [TestMethod]
    public void CopiesCatalogAndUsesExplicitOrderWithOrdinalIds()
    {
        var first = SessionFixtures.Level("z");
        var second = SessionFixtures.Level("A");
        var levels = new[] { first, second };
        var catalog = new LevelCatalog(levels);
        levels[0] = SessionFixtures.Level("changed");
        Assert.AreSame(first, catalog.Get("z"));
        Assert.AreEqual("A", catalog.GetNextId("z"));
        Assert.IsNull(catalog.GetNextId("A"));
        Assert.ThrowsExactly<KeyNotFoundException>(() => catalog.Get("a"));
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<LevelDefinition>)catalog.Levels).Clear());
        Assert.IsNull(new LevelCatalog([first]).GetNextId("z"));
    }

    [TestMethod]
    public void RejectsEmptyNullDuplicateAndUnknownEntries()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelCatalog(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new LevelCatalog([]));
        Assert.ThrowsExactly<ArgumentException>(() => new LevelCatalog([null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new LevelCatalog([SessionFixtures.Level(), SessionFixtures.Level()]));
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        Assert.ThrowsExactly<ArgumentNullException>(() => catalog.Get(null!));
        Assert.ThrowsExactly<ArgumentException>(() => catalog.GetNextId(" "));
        Assert.ThrowsExactly<KeyNotFoundException>(() => catalog.GetNextId("unknown"));
    }
}
