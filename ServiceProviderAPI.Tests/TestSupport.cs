using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Tests;

/// <summary>Shared helpers for building isolated in-memory contexts and configuration.</summary>
internal static class TestSupport
{
    /// <summary>
    /// Attach a signed-in caller (NameIdentifier + role claims) to a controller so actions
    /// that read <c>User</c> work. Pass <paramref name="id"/> = 0 to simulate a missing id.
    /// </summary>
    public static TController AsUser<TController>(this TController controller, int id, string role)
        where TController : ControllerBase
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, authenticationType: "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
    }

    /// <summary>A fresh in-memory <see cref="ApplicationDbContext"/> with a unique database name.</summary>
    public static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>An in-memory <see cref="IConfiguration"/> from the supplied key/value pairs.</summary>
    public static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
