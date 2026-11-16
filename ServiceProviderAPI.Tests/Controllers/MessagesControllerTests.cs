using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Tests.Controllers;

// Guards the type-aware party matching: a User and a Pro that share a numeric id must never be
// treated as the same participant.
public class MessagesControllerTests
{
    private static MessagesController Build(ApplicationDbContext db, int id, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, authenticationType: "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return new MessagesController(db, accessor, NullLogger<MessagesController>.Instance);
    }

    // ── MarkMessageAsRead ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkMessageAsRead_SameIdWrongType_IsForbidden()
    {
        var db = TestSupport.NewDb();
        // Message from a Pro (id 9) to the consumer User id 5 — recipient is User id 5.
        db.Messages.Add(new Message { Id = 1, SenderId = 9, RecipientId = 5, SenderType = "Pro", Content = "hi", MessageIndexId = 1 });
        await db.SaveChangesAsync();

        // A Pro that happens to also have id 5 must NOT be able to mark the User's message read.
        var result = await Build(db, id: 5, role: "Pro").MarkMessageAsRead(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MarkMessageAsRead_ActualRecipient_Succeeds()
    {
        var db = TestSupport.NewDb();
        db.Messages.Add(new Message { Id = 1, SenderId = 9, RecipientId = 5, SenderType = "Pro", Content = "hi", MessageIndexId = 1 });
        await db.SaveChangesAsync();

        // The real recipient (User id 5) can mark it read.
        var result = await Build(db, id: 5, role: "User").MarkMessageAsRead(1);

        Assert.IsType<OkObjectResult>(result);
        Assert.True((await db.Messages.FindAsync(1))!.IsRead);
    }

    // ── GetMessagesWithUser ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMessagesWithUser_ResolvesByCallerType()
    {
        var db = TestSupport.NewDb();
        db.MessageIndexes.Add(new MessageIndex { Id = 1, UserId1 = 5, UserType1 = "User", UserId2 = 9, UserType2 = "Pro", InitiatedAt = DateTime.UtcNow });
        db.Messages.Add(new Message { Id = 1, SenderId = 5, RecipientId = 9, SenderType = "User", Content = "hi", MessageIndexId = 1 });
        await db.SaveChangesAsync();

        // The User id 5 sees the conversation with Pro 9.
        var asUser = await Build(db, id: 5, role: "User").GetMessagesWithUser(9);
        Assert.NotEmpty((IEnumerable)((OkObjectResult)asUser).Value!);

        // A Pro that also has id 5 does NOT — there is no (Pro 5 ↔ User 9) conversation.
        var asPro = await Build(db, id: 5, role: "Pro").GetMessagesWithUser(9);
        Assert.Empty((IEnumerable)((OkObjectResult)asPro).Value!);
    }
}
