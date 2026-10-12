using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Tests.Controllers;

public class SettingsControllerTests
{
    [Fact]
    public async Task GetSetting_Existing_ReturnsOk()
    {
        var db = TestSupport.NewDb();
        db.AppSettings.Add(new AppSetting { Key = "theme", Value = "dark" });
        await db.SaveChangesAsync();
        var controller = new SettingsController(db);

        var result = await controller.GetSetting("theme");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSetting_Missing_ReturnsNotFound()
    {
        var controller = new SettingsController(TestSupport.NewDb());

        var result = await controller.GetSetting("nope");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateSetting_NewKey_CreatesSetting()
    {
        var db = TestSupport.NewDb();
        var controller = new SettingsController(db);

        var result = await controller.UpdateSetting("commission.user_percent", new UpdateSettingRequest("12"));

        Assert.IsType<OkObjectResult>(result.Result);
        var saved = await db.AppSettings.SingleAsync();
        Assert.Equal("commission.user_percent", saved.Key);
        Assert.Equal("12", saved.Value);
    }

    [Fact]
    public async Task UpdateSetting_ExistingKey_UpdatesValue()
    {
        var db = TestSupport.NewDb();
        db.AppSettings.Add(new AppSetting { Key = "maintenance", Value = "false" });
        await db.SaveChangesAsync();
        var controller = new SettingsController(db);

        var result = await controller.UpdateSetting("maintenance", new UpdateSettingRequest("true"));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("true", (await db.AppSettings.SingleAsync()).Value);
    }
}
