using Vectory.Game;
using Vectory.Ghost;
using Vectory.Records;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.GameTests;

public class SettingsStoreTests
{
    [Fact]
    public void NoFile_GhostEnabledByDefault()
    {
        var store = new SettingsStore(new MemoryRecordStorage());

        store.Load();

        Assert.True(store.Settings.GhostEnabled);
        Assert.Null(store.Warning);
    }

    [Fact]
    public void SaveFalse_ThenLoad_IsFalse()
    {
        var storage = new MemoryRecordStorage();
        var store = new SettingsStore(storage);
        store.Load();
        store.Settings.GhostEnabled = false;

        Assert.True(store.Save());

        var reloaded = new SettingsStore(storage);
        reloaded.Load();
        Assert.False(reloaded.Settings.GhostEnabled);
    }

    [Fact]
    public void FileWithoutField_GhostEnabled()
    {
        var store = new SettingsStore(new MemoryRecordStorage { Text = "{\"version\":1,\"other\":3}" });

        store.Load();

        Assert.True(store.Settings.GhostEnabled);
        Assert.Null(store.Warning);
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("{\"version\":2,\"ghostEnabled\":false}")]
    [InlineData("{\"ghostEnabled\":false}")]
    public void CorruptOrNewer_DefaultsAndBackup(string text)
    {
        var storage = new MemoryRecordStorage { Text = text };
        var store = new SettingsStore(storage);

        store.Load();

        Assert.True(store.Settings.GhostEnabled);
        Assert.NotNull(store.Warning);
        Assert.Equal(text, storage.Backup);
    }

    [Fact]
    public void NoFile_StartsWithVector()
    {
        var store = new SettingsStore(new MemoryRecordStorage());

        store.Load();

        Assert.Null(store.Settings.LastShipDesignId);
        Assert.Equal("ship_01", ShipCatalog.StartShip(store.Settings.LastShipDesignId));
    }

    [Fact]
    public void OldFileWithoutTheField_ReadsWell_StartsWithVector()
    {
        var storage = new MemoryRecordStorage { Text = "{\"version\":1,\"ghostEnabled\":false}" };
        var store = new SettingsStore(storage);

        store.Load();

        Assert.Null(store.Warning);
        Assert.Null(storage.Backup);
        Assert.False(store.Settings.GhostEnabled);
        Assert.Equal("ship_01", ShipCatalog.StartShip(store.Settings.LastShipDesignId));
    }

    [Fact]
    public void SaveBlade_ThenLoad_StartsWithBlade()
    {
        var storage = new MemoryRecordStorage();
        var store = new SettingsStore(storage);
        store.Load();
        store.Settings.LastShipDesignId = "ship_02";

        Assert.True(store.Save());

        var reloaded = new SettingsStore(storage);
        reloaded.Load();
        Assert.Equal("ship_02", reloaded.Settings.LastShipDesignId);
        Assert.Equal("ship_02", ShipCatalog.StartShip(reloaded.Settings.LastShipDesignId));
    }

    [Fact]
    public void UnknownIdInFile_StartsWithVector_AndTheFileIsNotCorrupt()
    {
        var store = new SettingsStore(new MemoryRecordStorage { Text = "{\"version\":1,\"lastShipDesignId\":\"ship_99\"}" });

        store.Load();

        Assert.Null(store.Warning);
        Assert.Equal("ship_01", ShipCatalog.StartShip(store.Settings.LastShipDesignId));
    }

    [Fact]
    public void SavingTheShip_DoesNotChangeGhostEnabled()
    {
        var storage = new MemoryRecordStorage { Text = "{\"version\":1,\"ghostEnabled\":false}" };
        var store = new SettingsStore(storage);
        store.Load();
        store.Settings.LastShipDesignId = "ship_03";

        store.Save();

        var reloaded = new SettingsStore(storage);
        reloaded.Load();
        Assert.False(reloaded.Settings.GhostEnabled);
        Assert.Equal("ship_03", reloaded.Settings.LastShipDesignId);
    }

    // El cambio de GHOST en la pausa guarda los ajustes con su propio SettingsStore: la nave tiene que seguir ahí
    [Fact]
    public void SavingGhostEnabled_KeepsTheShip()
    {
        var storage = new MemoryRecordStorage { Text = "{\"version\":1,\"lastShipDesignId\":\"ship_03\"}" };
        var store = new SettingsStore(storage);
        store.Load();
        store.Settings.GhostEnabled = false;

        store.Save();

        var reloaded = new SettingsStore(storage);
        reloaded.Load();
        Assert.False(reloaded.Settings.GhostEnabled);
        Assert.Equal("ship_03", reloaded.Settings.LastShipDesignId);
    }

    [Fact]
    public void WithoutAShip_TheFieldIsNotWritten()
    {
        var storage = new MemoryRecordStorage();
        var store = new SettingsStore(storage);
        store.Load();

        store.Save();

        Assert.DoesNotContain("lastShipDesignId", storage.Text);
    }

    // RESET RECORDS vacía los récords y los fantasmas, cada uno en su archivo: los ajustes no los toca
    [Fact]
    public void ResetRecords_DoesNotEraseTheShip()
    {
        var settingsStorage = new MemoryRecordStorage { Text = "{\"version\":1,\"lastShipDesignId\":\"ship_02\"}" };
        var records = new RecordStore(new MemoryRecordStorage());
        records.Load();
        var ghosts = new GhostStore(new MemoryGhostStorage());

        records.Book.Clear();
        records.Save();
        ghosts.DeleteAll();

        var settings = new SettingsStore(settingsStorage);
        settings.Load();
        Assert.Equal("ship_02", settings.Settings.LastShipDesignId);
    }
}
