using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Tests.Controllers;

public class NotificationsControllerTests
{
    private static JobNotification Notif(int? userId = null, int? proId = null, bool read = false) => new()
    {
        JobId = 1,
        UserId = userId,
        ProId = proId,
        IsRead = read,
    };

    // Reads a property off an anonymous response object (the controller returns Ok(new { ... })).
    private static T Prop<T>(object obj, string name) => (T)obj.GetType().GetProperty(name)!.GetValue(obj)!;

    [Fact]
    public async Task GetMyNotifications_User_ReturnsOnlyOwn()
    {
        var db = TestSupport.NewDb();
        db.JobNotifications.AddRange(Notif(userId: 1), Notif(userId: 1, read: true), Notif(userId: 2));
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(1, "User");

        var result = await controller.GetMyNotifications();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, Prop<int>(ok.Value!, "total"));
        var items = Prop<IEnumerable<JobNotification>>(ok.Value!, "notifications");
        Assert.All(items, n => Assert.Equal(1, n.UserId));
    }

    [Fact]
    public async Task GetMyNotifications_UnreadOnly_FiltersRead()
    {
        var db = TestSupport.NewDb();
        db.JobNotifications.AddRange(Notif(userId: 1), Notif(userId: 1, read: true));
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(1, "User");

        var result = await controller.GetMyNotifications(unreadOnly: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, Prop<int>(ok.Value!, "total"));
    }

    [Fact]
    public async Task GetMyNotifications_NoCallerId_ReturnsUnauthorized()
    {
        var controller = new NotificationsController(TestSupport.NewDb()).AsUser(0, "User");

        var result = await controller.GetMyNotifications();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetUnreadCount_Pro_CountsOwnUnread()
    {
        var db = TestSupport.NewDb();
        db.JobNotifications.AddRange(
            Notif(proId: 5), Notif(proId: 5), Notif(proId: 5, read: true), Notif(proId: 9));
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(5, "Pro");

        var result = await controller.GetUnreadCount();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, Prop<int>(ok.Value!, "count"));
    }

    [Fact]
    public async Task MarkRead_Missing_ReturnsNotFound()
    {
        var controller = new NotificationsController(TestSupport.NewDb()).AsUser(1, "User");

        var result = await controller.MarkRead(404);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MarkRead_NotOwner_ReturnsForbid()
    {
        var db = TestSupport.NewDb();
        var n = Notif(userId: 2);
        db.JobNotifications.Add(n);
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(1, "User");

        var result = await controller.MarkRead(n.Id);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MarkRead_OwnerUnread_MarksRead()
    {
        var db = TestSupport.NewDb();
        var n = Notif(userId: 1);
        db.JobNotifications.Add(n);
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(1, "User");

        var result = await controller.MarkRead(n.Id);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.JobNotifications.FindAsync(n.Id);
        Assert.True(saved!.IsRead);
        Assert.NotNull(saved.ReadAt);
    }

    [Fact]
    public async Task MarkRead_ProOwner_MarksRead()
    {
        var db = TestSupport.NewDb();
        var n = Notif(proId: 7);
        db.JobNotifications.Add(n);
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(7, "Pro");

        var result = await controller.MarkRead(n.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.True((await db.JobNotifications.FindAsync(n.Id))!.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyCallersUnread()
    {
        var db = TestSupport.NewDb();
        db.JobNotifications.AddRange(
            Notif(userId: 1), Notif(userId: 1), Notif(userId: 1, read: true), Notif(userId: 2));
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db).AsUser(1, "User");

        var result = await controller.MarkAllRead();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("2 notifications marked as read", Prop<string>(ok.Value!, "message"));
        Assert.Equal(0, await db.JobNotifications.CountAsync(n => n.UserId == 1 && !n.IsRead));
        Assert.Equal(1, await db.JobNotifications.CountAsync(n => n.UserId == 2 && !n.IsRead)); // untouched
    }
}
