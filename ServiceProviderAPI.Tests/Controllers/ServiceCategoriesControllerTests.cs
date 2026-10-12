using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Tests.Controllers;

public class ServiceCategoriesControllerTests
{
    private static ServiceCategory Cat(string name, bool active = true) =>
        new() { Name = name, IsActive = active };

    [Fact]
    public async Task GetCategories_ReturnsOnlyActive_OrderedByName()
    {
        var db = TestSupport.NewDb();
        db.ServiceCategories.AddRange(Cat("Plumbing"), Cat("Carpentry"), Cat("Hidden", active: false));
        await db.SaveChangesAsync();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.GetCategories();

        var list = Assert.IsAssignableFrom<IEnumerable<ServiceCategory>>(result.Value).ToList();
        Assert.Equal(new[] { "Carpentry", "Plumbing" }, list.Select(c => c.Name));
        Assert.All(list, c => Assert.Equal(0, c.ServiceCount)); // no services seeded
    }

    [Fact]
    public async Task GetCategory_Existing_ReturnsCategory()
    {
        var db = TestSupport.NewDb();
        var cat = Cat("Electrical");
        db.ServiceCategories.Add(cat);
        await db.SaveChangesAsync();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.GetCategory(cat.Id);

        Assert.Equal("Electrical", result.Value!.Name);
    }

    [Fact]
    public async Task GetCategory_Missing_ReturnsNotFound()
    {
        var controller = new ServiceCategoriesController(TestSupport.NewDb());

        var result = await controller.GetCategory(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateCategory_PersistsAndReturnsCreated()
    {
        var db = TestSupport.NewDb();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.CreateCategory(Cat("Painting"));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<ServiceCategory>(created.Value);
        Assert.Equal("Painting", value.Name);
        Assert.True(value.CreatedAt > default(DateTime));
        Assert.Equal(1, await db.ServiceCategories.CountAsync());
    }

    [Fact]
    public async Task UpdateCategory_IdMismatch_ReturnsBadRequest()
    {
        var controller = new ServiceCategoriesController(TestSupport.NewDb());

        var result = await controller.UpdateCategory(1, new ServiceCategory { Id = 2, Name = "X" });

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task UpdateCategory_Missing_ReturnsNotFound()
    {
        var controller = new ServiceCategoriesController(TestSupport.NewDb());

        var result = await controller.UpdateCategory(7, new ServiceCategory { Id = 7, Name = "Ghost" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateCategory_Existing_UpdatesFields()
    {
        var db = TestSupport.NewDb();
        var cat = Cat("Old");
        db.ServiceCategories.Add(cat);
        await db.SaveChangesAsync();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.UpdateCategory(cat.Id,
            new ServiceCategory { Id = cat.Id, Name = "New", Description = "desc", IsActive = false });

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.ServiceCategories.FindAsync(cat.Id);
        Assert.Equal("New", updated!.Name);
        Assert.Equal("desc", updated.Description);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task DeleteCategory_Existing_RemovesAndReturnsNoContent()
    {
        var db = TestSupport.NewDb();
        var cat = Cat("Temp");
        db.ServiceCategories.Add(cat);
        await db.SaveChangesAsync();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.DeleteCategory(cat.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.ServiceCategories.CountAsync());
    }

    [Fact]
    public async Task DeleteCategory_Missing_ReturnsNotFound()
    {
        var controller = new ServiceCategoriesController(TestSupport.NewDb());

        var result = await controller.DeleteCategory(123);

        Assert.IsType<NotFoundResult>(result);
    }

    private static Pro CompletePro(string email) => new()
    {
        ProName = "Pro", Email = email, PhoneNumber = "9000000000", BusinessName = "Biz", IsProfileComplete = true,
    };

    [Fact]
    public async Task GetCategories_CountsDistinctProsWithCompleteProfile()
    {
        var db = TestSupport.NewDb();
        var cat = Cat("Plumbing");
        var proA = CompletePro("a@test.com");
        var proB = CompletePro("b@test.com");
        var proIncomplete = new Pro
        {
            ProName = "P", Email = "c@test.com", PhoneNumber = "9", BusinessName = "B", IsProfileComplete = false,
        };
        db.ServiceCategories.Add(cat);
        db.Pros.AddRange(proA, proB, proIncomplete);
        await db.SaveChangesAsync();
        db.Services.AddRange(
            new Service { Name = "s1", Description = "d", ProId = proA.Id, ServiceCategoryId = cat.Id },
            new Service { Name = "s2", Description = "d", ProId = proB.Id, ServiceCategoryId = cat.Id },
            new Service { Name = "s3", Description = "d", ProId = proIncomplete.Id, ServiceCategoryId = cat.Id });
        await db.SaveChangesAsync();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.GetCategories();

        var only = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ServiceCategory>>(result.Value));
        Assert.Equal(2, only.ServiceCount); // proA + proB; the incomplete pro is excluded
    }

    [Fact]
    public async Task GetAllCategories_ReturnsActiveAndInactive_WithCounts()
    {
        var db = TestSupport.NewDb();
        var active = Cat("Active");
        db.ServiceCategories.AddRange(active, Cat("Inactive", active: false));
        var pro = CompletePro("x@test.com");
        db.Pros.Add(pro);
        await db.SaveChangesAsync();
        db.Services.Add(new Service { Name = "s", Description = "d", ProId = pro.Id, ServiceCategoryId = active.Id });
        await db.SaveChangesAsync();
        var controller = new ServiceCategoriesController(db);

        var result = await controller.GetAllCategories();

        var list = Assert.IsAssignableFrom<IEnumerable<ServiceCategory>>(result.Value).ToList();
        Assert.Equal(new[] { "Active", "Inactive" }, list.Select(c => c.Name)); // inactive included
        Assert.Equal(1, list.Single(c => c.Name == "Active").ServiceCount);
    }

    [Fact]
    public async Task UpdateCategory_ConcurrencyAndStillExists_Rethrows()
    {
        var (ctx, id, _) = SeedThrowingContext(deleteOnSave: false);
        var controller = new ServiceCategoriesController(ctx);

        // SaveChanges throws concurrency, the row still exists → the exception propagates.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => controller.UpdateCategory(id, new ServiceCategory { Id = id, Name = "New" }));
    }

    [Fact]
    public async Task UpdateCategory_ConcurrencyAfterConcurrentDelete_ReturnsNotFound()
    {
        var (ctx, id, _) = SeedThrowingContext(deleteOnSave: true);
        var controller = new ServiceCategoriesController(ctx);

        // SaveChanges throws concurrency and the row was deleted meanwhile → NotFound.
        var result = await controller.UpdateCategory(id, new ServiceCategory { Id = id, Name = "New" });

        Assert.IsType<NotFoundResult>(result);
    }

    private static (ConcurrencyThrowingContext ctx, int id, string dbName) SeedThrowingContext(bool deleteOnSave)
    {
        var dbName = $"db-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;
        int id;
        using (var seed = new ApplicationDbContext(options))
        {
            var cat = new ServiceCategory { Name = "Existing" };
            seed.ServiceCategories.Add(cat);
            seed.SaveChanges();
            id = cat.Id;
        }
        var ctx = new ConcurrencyThrowingContext(options)
        {
            DbName = dbName,
            DeleteCategoryIdOnSave = deleteOnSave ? id : null,
        };
        return (ctx, id, dbName);
    }

    /// <summary>
    /// A context whose async save always throws a concurrency exception, optionally
    /// deleting a row first (via a sibling context on the same in-memory store) to
    /// simulate a concurrent delete committed by another request.
    /// </summary>
    private sealed class ConcurrencyThrowingContext : ApplicationDbContext
    {
        public required string DbName { get; init; }
        public int? DeleteCategoryIdOnSave { get; init; }

        public ConcurrencyThrowingContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (DeleteCategoryIdOnSave is int id)
            {
                var opts = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(DbName).Options;
                using var sibling = new ApplicationDbContext(opts);
                var row = sibling.ServiceCategories.Find(id);
                if (row != null)
                {
                    sibling.ServiceCategories.Remove(row);
                    sibling.SaveChanges();
                }
            }
            throw new DbUpdateConcurrencyException("simulated concurrency conflict");
        }
    }
}
