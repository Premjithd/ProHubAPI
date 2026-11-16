using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Tests.Controllers;

public class SettingsControllerTests
{
    [Fact]
    public async Task GetSetting_PublicKey_Anonymous_ReturnsOk()
    {
        var db = TestSupport.NewDb();
        db.AppSettings.Add(new AppSetting { Key = "show_our_team", Value = "true" });
        await db.SaveChangesAsync();
        var controller = new SettingsController(db).AsAnonymous();

        var result = await controller.GetSetting("show_our_team");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSetting_NonPublicKey_Anonymous_ReturnsNotFound()
    {
        var db = TestSupport.NewDb();
        db.AppSettings.Add(new AppSetting { Key = "commission.user_percent", Value = "10" });
        await db.SaveChangesAsync();
        var controller = new SettingsController(db).AsAnonymous();

        // A non-public key must not be readable (or even confirmable) by a non-admin.
        var result = await controller.GetSetting("commission.user_percent");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSetting_NonPublicKey_Admin_ReturnsOk()
    {
        var db = TestSupport.NewDb();
        db.AppSettings.Add(new AppSetting { Key = "commission.user_percent", Value = "10" });
        await db.SaveChangesAsync();
        var controller = new SettingsController(db).AsUser(1, "Admin");

        var result = await controller.GetSetting("commission.user_percent");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSetting_Missing_PublicKey_ReturnsNotFound()
    {
        var controller = new SettingsController(TestSupport.NewDb()).AsAnonymous();

        var result = await controller.GetSetting("show_our_team");

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
