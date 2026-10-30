using Vectory.Game;
using Vectory.Records;
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
}
