using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

public class SeedDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(ApplicationDbContext context, ILogger<SeedDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Check if already seeded
            if (_context.ServiceCategories.Any())
            {
                _logger.LogInformation("Database already seeded, skipping.");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            // Seed Service Categories
            var categories = new[]
            {
                new ServiceCategory { Name = "Plumbing", Description = "Plumbing repairs and installations", IsActive = true },
                new ServiceCategory { Name = "Electrical", Description = "Electrical work and installations", IsActive = true },
                new ServiceCategory { Name = "Carpentry", Description = "Carpentry and wood work", IsActive = true },
                new ServiceCategory { Name = "Painting", Description = "Interior and exterior painting", IsActive = true },
                new ServiceCategory { Name = "Cleaning", Description = "Home and office cleaning", IsActive = true },
            };

            await _context.ServiceCategories.AddRangeAsync(categories);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Seeded {categories.Length} service categories");

            // Seed Materials by Category
            var materials = new List<Material>();

            // Plumbing Materials
            var plumbingCat = categories.FirstOrDefault(c => c.Name == "Plumbing");
            if (plumbingCat != null)
            {
                materials.AddRange(new[]
                {
                    new Material
                    {
                        Name = "Copper Pipe (1/2 inch)",
                        Description = "Standard copper pipe for water supply - 1 meter length",
                        UnitPrice = 125.00m,
                        ServiceCategoryId = plumbingCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "PVC Pipe (1 inch)",
                        Description = "PVC pipe for drainage - 1 meter length",
                        UnitPrice = 45.00m,
                        ServiceCategoryId = plumbingCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Brass Tap",
                        Description = "Standard brass water tap",
                        UnitPrice = 350.00m,
                        ServiceCategoryId = plumbingCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Toilet Seat",
                        Description = "Standard ceramic toilet seat",
                        UnitPrice = 1500.00m,
                        ServiceCategoryId = plumbingCat.Id,
                        IsActive = true
                    },
                });
            }

            // Electrical Materials
            var electricalCat = categories.FirstOrDefault(c => c.Name == "Electrical");
            if (electricalCat != null)
            {
                materials.AddRange(new[]
                {
                    new Material
                    {
                        Name = "Wire (1.5 sqmm)",
                        Description = "Single core electrical wire - per meter",
                        UnitPrice = 8.50m,
                        ServiceCategoryId = electricalCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "LED Bulb (9W)",
                        Description = "LED bulb for standard fixtures",
                        UnitPrice = 150.00m,
                        ServiceCategoryId = electricalCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Wall Switch",
                        Description = "Standard electric wall switch",
                        UnitPrice = 75.00m,
                        ServiceCategoryId = electricalCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Power Outlet",
                        Description = "Standard electrical outlet",
                        UnitPrice = 100.00m,
                        ServiceCategoryId = electricalCat.Id,
                        IsActive = true
                    },
                });
            }

            // Carpentry Materials
            var carpentryCat = categories.FirstOrDefault(c => c.Name == "Carpentry");
            if (carpentryCat != null)
            {
                materials.AddRange(new[]
                {
                    new Material
                    {
                        Name = "Plywood (18mm)",
                        Description = "Standard plywood sheet",
                        UnitPrice = 2500.00m,
                        ServiceCategoryId = carpentryCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Wood Polish (1L)",
                        Description = "Wood finishing polish - per liter",
                        UnitPrice = 450.00m,
                        ServiceCategoryId = carpentryCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Door Handle",
                        Description = "Stainless steel door handle",
                        UnitPrice = 200.00m,
                        ServiceCategoryId = carpentryCat.Id,
                        IsActive = true
                    },
                });
            }

            // Painting Materials
            var paintingCat = categories.FirstOrDefault(c => c.Name == "Painting");
            if (paintingCat != null)
            {
                materials.AddRange(new[]
                {
                    new Material
                    {
                        Name = "Emulsion Paint (20L)",
                        Description = "Indoor wall emulsion paint",
                        UnitPrice = 3500.00m,
                        ServiceCategoryId = paintingCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Primer (10L)",
                        Description = "Wall primer coating",
                        UnitPrice = 1200.00m,
                        ServiceCategoryId = paintingCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Paint Brush Set",
                        Description = "Assorted paint brushes",
                        UnitPrice = 250.00m,
                        ServiceCategoryId = paintingCat.Id,
                        IsActive = true
                    },
                });
            }

            // Cleaning Materials
            var cleaningCat = categories.FirstOrDefault(c => c.Name == "Cleaning");
            if (cleaningCat != null)
            {
                materials.AddRange(new[]
                {
                    new Material
                    {
                        Name = "Disinfectant (1L)",
                        Description = "Multi-purpose disinfectant - per liter",
                        UnitPrice = 250.00m,
                        ServiceCategoryId = cleaningCat.Id,
                        IsActive = true
                    },
                    new Material
                    {
                        Name = "Floor Cleaner (1L)",
                        Description = "Floor cleaning solution - per liter",
                        UnitPrice = 150.00m,
                        ServiceCategoryId = cleaningCat.Id,
                        IsActive = true
                    },
                });
            }

            await _context.Materials.AddRangeAsync(materials);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Seeded {materials.Count} materials");

            // Seed Test Consumer Users
            var consumers = new[]
            {
                new User
                {
                    FirstName = "Rajesh",
                    LastName = "Kumar",
                    Email = "rajesh@example.com",
                    PhoneNumber = "+91 98765 43201",
                    UserType = "User",
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "Priya",
                    LastName = "Singh",
                    Email = "priya@example.com",
                    PhoneNumber = "+91 98765 43202",
                    UserType = "User",
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "Amit",
                    LastName = "Patel",
                    Email = "amit@example.com",
                    PhoneNumber = "+91 98765 43203",
                    UserType = "User",
                    CreatedAt = DateTime.UtcNow
                },
            };

            await _context.Users.AddRangeAsync(consumers);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Seeded {consumers.Length} test consumers");

            // Seed Test Pro Users
            var pros = new[]
            {
                new Pro
                {
                    ProName = "Vijay Sharma",
                    Email = "vijay.pro@example.com",
                    PhoneNumber = "+91 99876 12345",
                    BusinessName = "Vijay Plumbing Services",
                    Latitude = 28.6139d,
                    Longitude = 77.2090d,
                    ServiceRadiusKm = 25,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Pro
                {
                    ProName = "Ramesh Nair",
                    Email = "ramesh.pro@example.com",
                    PhoneNumber = "+91 99876 12346",
                    BusinessName = "Ramesh Electrical Works",
                    Latitude = 28.6155d,
                    Longitude = 77.2100d,
                    ServiceRadiusKm = 30,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Pro
                {
                    ProName = "Suresh Verma",
                    Email = "suresh.pro@example.com",
                    PhoneNumber = "+91 99876 12347",
                    BusinessName = "Suresh Carpentry Solutions",
                    Latitude = 28.6200d,
                    Longitude = 77.2050d,
                    ServiceRadiusKm = 20,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Pro
                {
                    ProName = "Anita Desai",
                    Email = "anita.pro@example.com",
                    PhoneNumber = "+91 99876 12348",
                    BusinessName = "Anita Interior Painting",
                    Latitude = 28.6100d,
                    Longitude = 77.2110d,
                    ServiceRadiusKm = 35,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Pro
                {
                    ProName = "Raj Kumar",
                    Email = "raj.pro@example.com",
                    PhoneNumber = "+91 99876 12349",
                    BusinessName = "Raj Cleaning Services",
                    Latitude = 28.6080d,
                    Longitude = 77.2120d,
                    ServiceRadiusKm = 40,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow
                },
            };

            await _context.Pros.AddRangeAsync(pros);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Seeded {pros.Length} test pro users");

            // Seed sample jobs
            var consumerUser = consumers.FirstOrDefault();
            var sampleJobs = new[]
            {
                new Job
                {
                    UserId = consumerUser?.Id ?? 0,
                    CategoryId = categories.FirstOrDefault(c => c.Name == "Plumbing")?.Id,
                    Title = "Bathroom tap repair and replacement",
                    Description = "Main bathroom tap is leaking, needs replacement",
                    Status = "Open",
                    Budget = "2500",
                    EstimatedBudget = 2500.00m,
                    Timeline = "asap",
                    // Structured Address Fields
                    Latitude = 28.6139d,
                    Longitude = 77.2090d,
                    ServiceAddressStreet1 = "123 Main Street",
                    ServiceAddressCity = "New Delhi",
                    ServiceAddressState = "Delhi",
                    ServiceAddressPIN = "110001",
                    ServiceAddressCountry = "India",
                    ContactPersonName = consumerUser?.FirstName,
                    ContactPersonPhone = consumerUser?.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                },
                new Job
                {
                    UserId = consumerUser?.Id ?? 0,
                    CategoryId = categories.FirstOrDefault(c => c.Name == "Electrical")?.Id,
                    Title = "New electrical outlet installation",
                    Description = "Need 3 new outlets in living room",
                    Status = "Open",
                    Budget = "1500",
                    EstimatedBudget = 1500.00m,
                    Timeline = "1-week",
                    Latitude = 28.6139d,
                    Longitude = 77.2090d,
                    ServiceAddressStreet1 = "123 Main Street",
                    ServiceAddressCity = "New Delhi",
                    ServiceAddressState = "Delhi",
                    ServiceAddressPIN = "110001",
                    ServiceAddressCountry = "India",
                    ContactPersonName = consumerUser?.FirstName,
                    ContactPersonPhone = consumerUser?.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                },
                new Job
                {
                    UserId = consumerUser?.Id ?? 0,
                    CategoryId = categories.FirstOrDefault(c => c.Name == "Painting")?.Id,
                    Title = "Living room indoor painting",
                    Description = "Repaint living room with light blue color",
                    Status = "Open",
                    Budget = "5000",
                    EstimatedBudget = 5000.00m,
                    Timeline = "1-month",
                    Latitude = 28.6139d,
                    Longitude = 77.2090d,
                    ServiceAddressStreet1 = "123 Main Street",
                    ServiceAddressCity = "New Delhi",
                    ServiceAddressState = "Delhi",
                    ServiceAddressPIN = "110001",
                    ServiceAddressCountry = "India",
                    ContactPersonName = consumerUser?.FirstName,
                    ContactPersonPhone = consumerUser?.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                },
            };

            await _context.Jobs.AddRangeAsync(sampleJobs);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Seeded {sampleJobs.Length} sample jobs");

            // Seed sample bids
            var firstJob = sampleJobs.FirstOrDefault();
            var firstPro = pros.FirstOrDefault();

            if (firstPro != null && firstJob != null)
            {
                var bids = new[]
                {
                    new JobBid
                    {
                        JobId = firstJob.Id,
                        ProId = firstPro.Id,
                        BidAmount = 2000.00m,
                        CommenceDate = DateTime.UtcNow.AddDays(2),
                        ExpectedDurationDays = 1,
                        MaterialsDescription = "Copper pipe, brass tap, plumbing fittings",
                        ExpiresAt = DateTime.UtcNow.AddDays(7),
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    },
                };

                await _context.JobBids.AddRangeAsync(bids);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Seeded {bids.Length} sample bids");
            }

            _logger.LogInformation("Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during database seeding: {ex.Message}");
            throw;
        }
    }
}

