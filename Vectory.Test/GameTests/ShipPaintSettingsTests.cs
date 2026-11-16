using System.Collections.Generic;
using System.Linq;
using Vectory.Game;
using Vectory.Ghost;
using Vectory.Records;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.GameTests;

// Pintura guardada por nave (spec docs/selector-pinturas.md, 3 y pruebas 1–6)
public class ShipPaintSettingsTests
{
    private static SettingsStore Loaded(string? text)
    {
        var store = new SettingsStore(new MemoryRecordStorage { Text = text });
        store.Load();
        return store;
    }

    // ---------- 1. Sin archivo o sin shipPaints, las de por defecto ----------
    [Theory]
    [InlineData(null)]
    [InlineData("{\"version\":1,\"ghostEnabled\":true,\"lastShipDesignId\":\"ship_02\"}")]
    public void WithoutShipPaints_EachShipHasItsDefault(string? text)
    {
        SettingsStore store = Loaded(text);

        Assert.Null(store.Warning);
        Assert.Equal(new PaintChoice("graphite_red", false), store.Settings.PaintFor("ship_01"));
        Assert.Equal(new PaintChoice("woodland", true), store.Settings.PaintFor("ship_02"));
        Assert.Equal(new PaintChoice("site_yellow", true), store.Settings.PaintFor("ship_03"));
        Assert.Empty(store.Settings.ShipPaints);
    }

    // La de por defecto es la misma combinación que ya llevaban los cascos
    [Fact]
    public void TheDefaults_AreTheHullDefaultIndices()
    {
        var settings = new GameSettings();
        foreach (HullStyle style in new[] { HullStyle.Racing, HullStyle.Military, HullStyle.Industrial })
        {
            string ship = ShipDefinitions.All.Single(d => d.Visual.Hull3D?.Style == style).Id;
            Assert.Equal(HullPaints.DefaultIndex(style), HullPaints.IndexOf(style, settings.PaintFor(ship)!.Value));
        }
    }

    // ---------- 2. Guardar una y volver a cargar ----------
    [Fact]
    public void SaveBladeDesertWithMarkings_ThenLoad_OnlyBladeChanges()
    {
        var storage = new MemoryRecordStorage();
        var store = new SettingsStore(storage);
        store.Load();
        Assert.True(store.Settings.SetPaint("ship_02", new PaintChoice("desert", true)));
        Assert.True(store.Save());
        Assert.Contains("\"shipPaints\"", storage.Text);
        Assert.Contains("\"paint\": \"desert\"", storage.Text);
        Assert.Contains("\"marks\": true", storage.Text);

        var reloaded = new SettingsStore(storage);
        reloaded.Load();
        Assert.Null(reloaded.Warning);
        Assert.Equal(new PaintChoice("desert", true), reloaded.Settings.PaintFor("ship_02"));
        Assert.Equal(new PaintChoice("graphite_red", false), reloaded.Settings.PaintFor("ship_01"));
        Assert.Equal(new PaintChoice("site_yellow", true), reloaded.Settings.PaintFor("ship_03"));
    }

    [Fact]
    public void WithoutPaints_TheFieldIsNotWritten()
    {
        var storage = new MemoryRecordStorage();
        var store = new SettingsStore(storage);
        store.Load();
        store.Save();

        Assert.DoesNotContain("shipPaints", storage.Text);
    }

    // ---------- 3. Lectura tolerante ----------
    [Fact]
    public void APaintOfAnotherShip_IsTheDefaultOfThatShip()
    {
        SettingsStore store = Loaded("{\"version\":1,\"shipPaints\":{\"ship_01\":{\"paint\":\"site_yellow\",\"marks\":true}}}");

        Assert.Null(store.Warning);
        Assert.Equal(new PaintChoice("graphite_red", false), store.Settings.PaintFor("ship_01"));
    }

    [Theory]
    [InlineData("{\"ship_09\":{\"paint\":\"woodland\",\"marks\":true},\"ship_02\":{\"paint\":\"arctic\",\"marks\":false}}")]
    [InlineData("{\"ship_02\":{\"paint\":\"arctic\",\"marks\":false},\"ship_01\":7,\"ship_03\":{\"paint\":12}}")]
    public void UnknownShipsAndOddValues_AreIgnored_WithoutCorruptingTheFile(string paints)
    {
        SettingsStore store = Loaded("{\"version\":1,\"ghostEnabled\":false,\"lastShipDesignId\":\"ship_03\",\"shipPaints\":" + paints + "}");

        Assert.Null(store.Warning);
        Assert.False(store.Settings.GhostEnabled);
        Assert.Equal("ship_03", store.Settings.LastShipDesignId);
        Assert.Equal(new PaintChoice("arctic", false), store.Settings.PaintFor("ship_02"));
        Assert.Equal(new PaintChoice("graphite_red", false), store.Settings.PaintFor("ship_01"));
        Assert.Equal(new PaintChoice("site_yellow", true), store.Settings.PaintFor("ship_03"));
        Assert.Null(store.Settings.PaintFor("ship_09"));
    }

    [Theory]
    [InlineData("{\"paint\":\"racing_red\"}")]
    [InlineData("{\"paint\":\"racing_red\",\"marks\":\"yes\"}")]
    [InlineData("{\"paint\":\"racing_red\",\"marks\":1}")]
    public void MissingOrNonBooleanMarks_TakeThoseOfTheDefault(string entry)
    {
        SettingsStore vector = Loaded("{\"version\":1,\"shipPaints\":{\"ship_01\":" + entry + "}}");
        SettingsStore blade = Loaded("{\"version\":1,\"shipPaints\":{\"ship_02\":" + entry.Replace("racing_red", "desert") + "}}");

        Assert.Null(vector.Warning);
        Assert.Equal(new PaintChoice("racing_red", false), vector.Settings.PaintFor("ship_01"));
        Assert.Equal(new PaintChoice("desert", true), blade.Settings.PaintFor("ship_02"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"ship_01\"")]
    [InlineData("null")]
    public void ShipPaintsThatIsNotAnObject_IsIgnored(string paints)
    {
        SettingsStore store = Loaded("{\"version\":1,\"shipPaints\":" + paints + "}");

        Assert.Null(store.Warning);
        Assert.Empty(store.Settings.ShipPaints);
    }

    [Fact]
    public void SetPaint_RejectsUnknownShipsAndPaints()
    {
        var settings = new GameSettings();

        Assert.False(settings.SetPaint("ship_09", new PaintChoice("woodland", true)));
        Assert.False(settings.SetPaint("ship_01", new PaintChoice("woodland", true)));
        Assert.False(settings.SetPaint("ship_01", new PaintChoice(null, false)));
        Assert.Empty(settings.ShipPaints);
    }

    // ---------- 4. Independiente del resto de ajustes y de los récords ----------
    [Fact]
    public void SavingAPaint_DoesNotChangeTheShipNorTheGhost_AndTheOtherWayRound()
    {
        var storage = new MemoryRecordStorage { Text = "{\"version\":1,\"ghostEnabled\":false,\"lastShipDesignId\":\"ship_02\"}" };
        var store = new SettingsStore(storage);
        store.Load();
        store.Settings.SetPaint("ship_03", new PaintChoice("orange_white", false));
        store.Save();

        var reloaded = new SettingsStore(storage);
        reloaded.Load();
        Assert.False(reloaded.Settings.GhostEnabled);
        Assert.Equal("ship_02", reloaded.Settings.LastShipDesignId);

        reloaded.Settings.GhostEnabled = true;
        reloaded.Settings.LastShipDesignId = "ship_01";
        reloaded.Save();

        var again = new SettingsStore(storage);
        again.Load();
        Assert.Equal(new PaintChoice("orange_white", false), again.Settings.PaintFor("ship_03"));
    }

    [Fact]
    public void ResetRecords_DoesNotEraseThePaints()
    {
        var settingsStorage = new MemoryRecordStorage
        {
            Text = "{\"version\":1,\"shipPaints\":{\"ship_01\":{\"paint\":\"racing_red\",\"marks\":true}}}"
        };
        var records = new RecordStore(new MemoryRecordStorage());
        records.Load();
        var ghosts = new GhostStore(new MemoryGhostStorage());

        records.Book.Clear();
        records.Save();
        ghosts.DeleteAll();

        var settings = new SettingsStore(settingsStorage);
        settings.Load();
        Assert.Equal(new PaintChoice("racing_red", true), settings.Settings.PaintFor("ship_01"));
    }

    // ---------- PaintChoice y las 40 posiciones ----------
    [Fact]
    public void ChoiceAt_AndIndexOf_GoBothWays()
    {
        foreach (HullStyle style in new[] { HullStyle.Racing, HullStyle.Military, HullStyle.Industrial })
        {
            Assert.Equal(40, HullPaints.Count(style));
            for (int i = 0; i < 40; i++)
                Assert.Equal(i, HullPaints.IndexOf(style, HullPaints.ChoiceAt(style, i)));
            Assert.Equal(-1, HullPaints.IndexOf(style, new PaintChoice("no_such_paint", false)));
            Assert.Equal(HullPaints.ChoiceAt(style, 0), HullPaints.ChoiceAt(style, 40));
        }
        Assert.Equal(new PaintChoice("desert", true), HullPaints.ChoiceAt(HullStyle.Military, 21));
        Assert.Equal(16, HullPaints.IndexOf(HullStyle.Industrial, new PaintChoice("orange_white", false)));
    }

    // ---------- 5. Nombres en inglés ----------
    private static readonly string[] VectorIds =
    {
        "graphite_red", "piano_cyan", "black_chrome", "stealth_matte", "pearl_white", "liquid_silver", "titanium",
        "racing_red", "lava", "amber", "taxi", "gold", "acid_green", "british_green", "cobalt", "midnight", "ice",
        "violet", "neon_pink", "chameleon"
    };

    private static readonly string[] BladeIds =
    {
        "woodland", "desert", "arctic", "urban", "night", "jungle", "tundra", "naval_split", "autumn", "ash", "olive",
        "sand", "field_grey", "ops_black", "nato_green", "earth", "navy_grey", "winter_white", "slate_blue", "khaki"
    };

    private static readonly string[] HammerIds =
    {
        "site_yellow", "bright_yellow", "orange", "terracotta", "tractor_green", "crane_red", "fleet_white", "site_blue",
        "plant_grey", "coal_black", "khaki", "safety_green", "cream", "maroon", "petrol_blue", "yellow_black",
        "orange_white", "white_red", "green_white", "grey_orange"
    };

    private static void AssertEnglishNames(IReadOnlyList<string> names)
    {
        Assert.Equal(20, names.Count);
        Assert.Equal(20, names.Distinct().Count());
        foreach (string name in names)
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.Equal(name.ToUpperInvariant(), name);
            Assert.All(name, c => Assert.True(c < 128 || c == '·', $"{name} has a non English character"));
        }
    }

    [Fact]
    public void AllSixtyPaints_HaveUniqueEnglishNames_AndKeepTheirIds()
    {
        Assert.Equal(VectorIds, HullPaintCatalog.Paints.Select(p => p.Id));
        Assert.Equal(BladeIds, MilitaryPaintCatalog.Paints.Select(p => p.Id));
        Assert.Equal(HammerIds, IndustrialPaintCatalog.Paints.Select(p => p.Id));

        AssertEnglishNames(HullPaintCatalog.Paints.Select(p => p.ScreenName).ToList());
        AssertEnglishNames(MilitaryPaintCatalog.Paints.Select(p => p.ScreenName).ToList());
        AssertEnglishNames(IndustrialPaintCatalog.Paints.Select(p => p.ScreenName).ToList());

        Assert.Equal("GRAPHITE", HullPaintCatalog.Find("graphite_red").ScreenName);
        Assert.Equal("EARTH BROWN", MilitaryPaintCatalog.Find("earth").ScreenName);
        Assert.Equal("ORANGE · WHITE CAB", IndustrialPaintCatalog.Find("orange_white").ScreenName);
    }

    // ---------- 6. Acabados en inglés ----------
    [Fact]
    public void Finishes_HaveEnglishNames()
    {
        Assert.Equal(new[] { "GLOSS", "METALLIC", "SATIN", "MATTE", "CHROME", "PEARL", "CHAMELEON" },
            HullPaintCatalog.Finishes.Select(f => f.ScreenName));
        Assert.Equal("SATIN", HullPaintCatalog.Find("graphite_red").Finish.ScreenName);
        Assert.Equal("CHAMELEON", HullPaintCatalog.Find("chameleon").Finish.ScreenName);
        Assert.Equal("MATTE", MilitaryFinish.Matte.ScreenName);
        Assert.Equal("SATIN", MilitaryFinish.Satin.ScreenName);
    }
}
