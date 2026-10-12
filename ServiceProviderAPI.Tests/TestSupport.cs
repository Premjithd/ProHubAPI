using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Tests;

/// <summary>Shared helpers for building isolated in-memory contexts and configuration.</summary>
internal static class TestSupport
{
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
