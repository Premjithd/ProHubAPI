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
}
