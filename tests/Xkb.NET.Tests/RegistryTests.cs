using Xunit;

namespace Xkb.Tests;

/// <summary>
/// Registry tests; they need an installed xkeyboard-config and skip otherwise.
/// </summary>
public class RegistryTests
{
    private static XkbRegistry CreateOrSkip()
    {
        try
        {
            return XkbRegistry.Create();
        }
        catch (XkbException)
        {
            Assert.Skip("xkeyboard-config is not installed");
            throw; // unreachable
        }
    }

    [Fact]
    public void Registry_ListsModels()
    {
        using var registry = CreateOrSkip();
        var models = registry.Models;
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Name == "pc105");
    }

    [Fact]
    public void Registry_ListsLayouts()
    {
        using var registry = CreateOrSkip();
        var layouts = registry.Layouts;
        Assert.NotEmpty(layouts);

        var us = layouts.First(l => l.Name == "us" && l.Variant is null);
        Assert.Equal(XkbPopularity.Standard, us.Popularity);
        Assert.Contains("eng", us.Iso639Codes);
        Assert.Contains("US", us.Iso3166Codes);

        // Variants are separate entries.
        Assert.Contains(layouts, l => l.Name == "us" && l.Variant is not null);
    }

    [Fact]
    public void Registry_ListsOptionGroups()
    {
        using var registry = CreateOrSkip();
        var groups = registry.OptionGroups;
        Assert.NotEmpty(groups);

        var grp = groups.First(g => g.Name == "grp");
        Assert.NotEmpty(grp.Options);
        Assert.All(grp.Options, o => Assert.StartsWith("grp:", o.Name));
    }
}
