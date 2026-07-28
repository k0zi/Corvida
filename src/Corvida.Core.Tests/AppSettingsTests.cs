using Corvida.Models;

namespace Corvida.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DataPath_DefaultsToUserProfileCorvidaData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "CorvidaData");
        Assert.Equal(expected, new AppSettings().DataPath);
    }

    [Fact]
    public void StorageMode_DefaultsToLocalFolder()
    {
        Assert.Equal(StorageMode.LocalFolder, new AppSettings().StorageMode);
    }

    [Fact]
    public void ServerUrl_DefaultsToNull()
    {
        Assert.Null(new AppSettings().ServerUrl);
    }

    [Fact]
    public void DataPath_CanBeOverridden()
    {
        var settings = new AppSettings { DataPath = "/custom/path" };
        Assert.Equal("/custom/path", settings.DataPath);
    }

    [Fact]
    public void StorageMode_CanBeSetToServerHosted()
    {
        var settings = new AppSettings { StorageMode = StorageMode.ServerHosted };
        Assert.Equal(StorageMode.ServerHosted, settings.StorageMode);
    }
}
