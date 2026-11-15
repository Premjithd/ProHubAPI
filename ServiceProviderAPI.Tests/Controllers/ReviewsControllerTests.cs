using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Tests.Controllers;

public class ReviewsControllerTests
{
    private static T Prop<T>(object obj, string name) => (T)obj.GetType().GetProperty(name)!.GetValue(obj)!;

    private static Job CompletedJob(int userId, int? proId, string status = "Completed") => new()
    {
        UserId = userId, Title = "Fix sink", Description = "leaky", Location = "TVM",
        Budget = "1000", Timeline = "asap", Status = status, AssignedProId = proId,
    };

    private static User Customer(int id) => new()
    {
        Id = id, FirstName = "Asha", LastName = "K", Email = $"u{id}@test.com", UserType = "User",
    };

    private static Pro Professional(int id) => new()
    {
        Id = id, ProName = "FixIt", Email = $"p{id}@test.com", PhoneNumber = "9", BusinessName = "B",
    };

    private static CreateReviewRequest Req(int rating = 5, string? comment = "Great") =>
        new() { Rating = rating, Comment = comment };

    // ── SubmitReview (customer → pro) ─────────────────────────────────────────

    [Fact]
    public async Task SubmitReview_Anonymous_ReturnsUnauthorized()
    {
        var controller = new ReviewsController(TestSupport.NewDb()).AsAnonymous();

        var result = await controller.SubmitReview(1, Req());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task SubmitReview_JobMissing_ReturnsNotFound()
    {
        var controller = new ReviewsController(TestSupport.NewDb()).AsUser(1, "User");

        var result = await controller.SubmitReview(999, Req());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task SubmitReview_JobNotCompleted_ReturnsBadRequest()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5, status: "Open");
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.SubmitReview(job.Id, Req());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SubmitReview_NotJobOwner_ReturnsForbid()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 2, proId: 5);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.SubmitReview(job.Id, Req());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task SubmitReview_NoAssignedPro_ReturnsBadRequest()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: null);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.SubmitReview(job.Id, Req());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SubmitReview_AlreadyReviewed_ReturnsConflict()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        db.Reviews.Add(new Review { JobId = job.Id, ReviewerId = 1, ProId = 5, Rating = 4 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.SubmitReview(job.Id, Req());

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task SubmitReview_Valid_CreatesReviewWithReviewerName()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        db.Users.Add(Customer(1));
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.SubmitReview(job.Id, Req(rating: 5, comment: "Excellent"));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ReviewDto>(created.Value);
        Assert.Equal("Asha K", dto.ReviewerName);
        Assert.Equal(5, dto.Rating);
        Assert.Equal(1, await db.Reviews.CountAsync());
    }

    // ── GetJobReview ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetJobReview_Missing_ReturnsNotFound()
    {
        var controller = new ReviewsController(TestSupport.NewDb()).AsUser(1, "User");

        var result = await controller.GetJobReview(1);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetJobReview_Existing_ReturnsReview()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        db.Users.Add(Customer(1));
        await db.SaveChangesAsync();
        db.Reviews.Add(new Review { JobId = job.Id, ReviewerId = 1, ProId = 5, Rating = 4, Comment = "Good" });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.GetJobReview(job.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ReviewDto>(ok.Value);
        Assert.Equal(4, dto.Rating);
        Assert.Equal("Asha K", dto.ReviewerName);
    }

    // ── GetProReviews / summaries / platform stats ────────────────────────────

    [Fact]
    public async Task GetProReviews_ReturnsOnlyForThatPro()
    {
        var db = TestSupport.NewDb();
        db.Reviews.AddRange(
            new Review { JobId = 1, ReviewerId = 1, ProId = 5, Rating = 5 },
            new Review { JobId = 2, ReviewerId = 2, ProId = 5, Rating = 4 },
            new Review { JobId = 3, ReviewerId = 3, ProId = 9, Rating = 3 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db);

        var result = await controller.GetProReviews(5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(2, Prop<int>(ok.Value!, "total"));
    }

    [Fact]
    public async Task GetProRatingSummary_ComputesAverageAndBreakdown()
    {
        var db = TestSupport.NewDb();
        db.Reviews.AddRange(
            new Review { JobId = 1, ReviewerId = 1, ProId = 5, Rating = 5 },
            new Review { JobId = 2, ReviewerId = 2, ProId = 5, Rating = 4 },
            new Review { JobId = 3, ReviewerId = 3, ProId = 5, Rating = 4 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db);

        var result = await controller.GetProRatingSummary(5);

        var summary = Assert.IsType<ProRatingSummary>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(3, summary.TotalReviews);
        Assert.Equal(4.3, summary.AverageRating);
        Assert.Equal(2, summary.RatingBreakdown[3]); // two 4-star
        Assert.Equal(1, summary.RatingBreakdown[4]); // one 5-star
    }

    [Fact]
    public async Task GetProRatingSummary_NoReviews_ReturnsZero()
    {
        var controller = new ReviewsController(TestSupport.NewDb());

        var result = await controller.GetProRatingSummary(5);

        var summary = Assert.IsType<ProRatingSummary>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(0, summary.TotalReviews);
        Assert.Equal(0, summary.AverageRating);
    }

    [Fact]
    public async Task GetPlatformStats_AveragesAllReviews()
    {
        var db = TestSupport.NewDb();
        db.Reviews.AddRange(
            new Review { JobId = 1, ReviewerId = 1, ProId = 5, Rating = 5 },
            new Review { JobId = 2, ReviewerId = 2, ProId = 9, Rating = 3 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db);

        var result = await controller.GetPlatformStats();

        var stats = Assert.IsType<PlatformRatingStats>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, stats.TotalReviews);
        Assert.Equal(4.0, stats.AverageRating);
    }

    // ── SubmitUserReview (pro → customer) ─────────────────────────────────────

    [Fact]
    public async Task SubmitUserReview_NotAssignedPro_ReturnsForbid()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(7, "Pro"); // caller 7 != assigned 5

        var result = await controller.SubmitUserReview(job.Id, Req());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task SubmitUserReview_Conflict_WhenAlreadyReviewed()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        db.UserReviews.Add(new UserReview { JobId = job.Id, ReviewerId = 5, UserId = 1, Rating = 4 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(5, "Pro");

        var result = await controller.SubmitUserReview(job.Id, Req());

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task SubmitUserReview_Valid_CreatesWithProName()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        db.Pros.Add(Professional(5));
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(5, "Pro");

        var result = await controller.SubmitUserReview(job.Id, Req(rating: 5));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<UserReviewDto>(created.Value);
        Assert.Equal("FixIt", dto.ReviewerName);
        Assert.Equal(1, dto.UserId);
        Assert.Equal(1, await db.UserReviews.CountAsync());
    }

    // ── GetJobUserReview / user reviews / summary ─────────────────────────────

    [Fact]
    public async Task GetJobUserReview_Existing_ReturnsReview()
    {
        var db = TestSupport.NewDb();
        var job = CompletedJob(userId: 1, proId: 5);
        db.Jobs.Add(job);
        db.Pros.Add(Professional(5));
        await db.SaveChangesAsync();
        db.UserReviews.Add(new UserReview { JobId = job.Id, ReviewerId = 5, UserId = 1, Rating = 5 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(5, "Pro");

        var result = await controller.GetJobUserReview(job.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("FixIt", Assert.IsType<UserReviewDto>(ok.Value).ReviewerName);
    }

    [Fact]
    public async Task GetUserReviews_ReturnsOnlyForThatUser()
    {
        var db = TestSupport.NewDb();
        db.UserReviews.AddRange(
            new UserReview { JobId = 1, ReviewerId = 5, UserId = 1, Rating = 5 },
            new UserReview { JobId = 2, ReviewerId = 6, UserId = 2, Rating = 3 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.GetUserReviews(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, Prop<int>(ok.Value!, "total"));
    }

    [Fact]
    public async Task GetUserRatingSummary_ComputesAverage()
    {
        var db = TestSupport.NewDb();
        db.UserReviews.AddRange(
            new UserReview { JobId = 1, ReviewerId = 5, UserId = 1, Rating = 5 },
            new UserReview { JobId = 2, ReviewerId = 6, UserId = 1, Rating = 2 });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db).AsUser(1, "User");

        var result = await controller.GetUserRatingSummary(1);

        var summary = Assert.IsType<UserRatingSummary>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, summary.TotalReviews);
        Assert.Equal(3.5, summary.AverageRating);
        Assert.Equal(1, summary.RatingBreakdown[4]); // one 5-star
        Assert.Equal(1, summary.RatingBreakdown[1]); // one 2-star
    }
}
