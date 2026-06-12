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
            // Seed initial service area (Trivandrum) — runs independently of category seed
            if (!_context.ServiceAreas.Any())
            {
                await _context.ServiceAreas.AddAsync(new ServiceArea
                {
                    Country = "India",
                    State = "Kerala",
                    District = "Thiruvananthapuram",
                    PinCode = null,
                    IsActive = true,
                    IsAutoEnrolled = false,
                    Notes = "Initial launch area — Thiruvananthapuram, Kerala",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded initial service area: Thiruvananthapuram, Kerala, India");
            }

            _logger.LogInformation("Starting database seeding...");

            // Seed Service Categories — idempotent: adds any canonical category that
            // isn't already present, so existing databases top up on restart.
            var canonicalCategories = new[]
            {
                new ServiceCategory { Name = "Cleaning", Description = "Home and office cleaning", Icon = "🧹", IsActive = true },
                new ServiceCategory { Name = "Plumbing", Description = "Plumbing repairs and installations", Icon = "🔧", IsActive = true },
                new ServiceCategory { Name = "Electrical", Description = "Electrical work and installations", Icon = "⚡", IsActive = true },
                new ServiceCategory { Name = "Painting", Description = "Interior and exterior painting", Icon = "🎨", IsActive = true },
                new ServiceCategory { Name = "Landscaping", Description = "Landscaping and garden design", Icon = "🌿", IsActive = true },
                new ServiceCategory { Name = "Handyman", Description = "A category for general handy man task", Icon = "🔨", IsActive = true },
                new ServiceCategory { Name = "Cooking", Description = "New category for Cooking related task", Icon = "👨‍🍳", IsActive = true },
                new ServiceCategory { Name = "Pest Control", Description = "Pest control and extermination services", Icon = "🐜", IsActive = true },
                new ServiceCategory { Name = "AC & Appliance Repair", Description = "Air conditioning and home appliance repair", Icon = "❄️", IsActive = true },
                new ServiceCategory { Name = "Interior Design", Description = "Home and office interior design and decoration", Icon = "🏗️", IsActive = true },
                new ServiceCategory { Name = "Hair & Beauty", Description = "Hair styling, makeup and beauty services", Icon = "💇", IsActive = true },
                new ServiceCategory { Name = "Massage & Spa", Description = "Therapeutic massage and spa treatments", Icon = "💆", IsActive = true },
                new ServiceCategory { Name = "Yoga & Fitness", Description = "Personal training, yoga and fitness coaching", Icon = "🧘", IsActive = true },
                new ServiceCategory { Name = "Babysitting & Childcare", Description = "Trusted babysitting and childcare services", Icon = "👶", IsActive = true },
                new ServiceCategory { Name = "Pet Care", Description = "Pet grooming, sitting and veterinary assistance", Icon = "🐾", IsActive = true },
                new ServiceCategory { Name = "Tutoring", Description = "Academic tutoring and coaching for all levels", Icon = "📚", IsActive = true },
                new ServiceCategory { Name = "IT Support & Repair", Description = "Computer setup, troubleshooting and IT support", Icon = "💻", IsActive = true },
                new ServiceCategory { Name = "Phone & Laptop Repair", Description = "Screen replacements, battery and hardware repairs", Icon = "📱", IsActive = true },
                new ServiceCategory { Name = "Music Lessons", Description = "Guitar, keyboard, vocals and other instruments", Icon = "🎸", IsActive = true },
                new ServiceCategory { Name = "Photography & Videography", Description = "Event photography, portraits and video production", Icon = "📷", IsActive = true },
                new ServiceCategory { Name = "Vehicle Repair & Service", Description = "Car, bike and auto repair and maintenance", Icon = "🚗", IsActive = true },
                new ServiceCategory { Name = "Real Estate & Vastu", Description = "Property consulting, staging and Vastu services", Icon = "🏠", IsActive = true },
                new ServiceCategory { Name = "Movers & Packers", Description = "Home and office relocation and packing services", Icon = "📦", IsActive = true },
                new ServiceCategory { Name = "Security & CCTV", Description = "CCTV installation and security system setup", Icon = "🔒", IsActive = true },
                new ServiceCategory { Name = "Gardening & Landscaping", Description = "Garden maintenance, landscaping and planting", Icon = "🌿", IsActive = true },
                new ServiceCategory { Name = "Catering & Cooking", Description = "Home catering, personal chefs and cooking services", Icon = "🍳", IsActive = true },
                new ServiceCategory { Name = "Masonry & Tiling", Description = "Brick work, plastering and tile installation", Icon = "🧱", IsActive = true },
                new ServiceCategory { Name = "Waterproofing", Description = "Roof, terrace and basement waterproofing", Icon = "🪣", IsActive = true },
                new ServiceCategory { Name = "Bathroom Renovation", Description = "Full bathroom remodelling and fixture installation", Icon = "🚿", IsActive = true },
                new ServiceCategory { Name = "Locksmith", Description = "Lock installation, repair and emergency unlock", Icon = "🔑", IsActive = true },
                new ServiceCategory { Name = "Other", Description = "Something that doesn't come under any of the listed categories.", Icon = "➕", IsActive = true },
            };

            var existingCategoryNames = _context.ServiceCategories.Select(c => c.Name).ToList();
            var missingCategories = canonicalCategories
                .Where(c => !existingCategoryNames.Contains(c.Name))
                .ToArray();
            if (missingCategories.Length > 0)
            {
                await _context.ServiceCategories.AddRangeAsync(missingCategories);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Seeded {missingCategories.Length} service categories");
            }
            else
            {
                _logger.LogInformation("Service categories already up to date");
            }

            // Demo data below (materials + sample pros) is only for a fresh local
            // database — skip once it has been seeded.
            if (_context.Materials.Any())
            {
                _logger.LogInformation("Demo data already present, skipping materials and sample pros.");
                return;
            }

            // Load categories with their assigned Ids for the material seeding below.
            var categories = _context.ServiceCategories.ToList();

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
                    ServiceRadiusKm = 25,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    Address = new Models.Address { AddressType = "Pro", City = "New Delhi", State = "Delhi", Country = "India", Latitude = 28.6139d, Longitude = 77.2090d }
                },
                new Pro
                {
                    ProName = "Ramesh Nair",
                    Email = "ramesh.pro@example.com",
                    PhoneNumber = "+91 99876 12346",
                    BusinessName = "Ramesh Electrical Works",
                    ServiceRadiusKm = 30,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    Address = new Models.Address { AddressType = "Pro", City = "New Delhi", State = "Delhi", Country = "India", Latitude = 28.6155d, Longitude = 77.2100d }
                },
                new Pro
                {
                    ProName = "Suresh Verma",
                    Email = "suresh.pro@example.com",
                    PhoneNumber = "+91 99876 12347",
                    BusinessName = "Suresh Carpentry Solutions",
                    ServiceRadiusKm = 20,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    Address = new Models.Address { AddressType = "Pro", City = "New Delhi", State = "Delhi", Country = "India", Latitude = 28.6200d, Longitude = 77.2050d }
                },
                new Pro
                {
                    ProName = "Anita Desai",
                    Email = "anita.pro@example.com",
                    PhoneNumber = "+91 99876 12348",
                    BusinessName = "Anita Interior Painting",
                    ServiceRadiusKm = 35,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    Address = new Models.Address { AddressType = "Pro", City = "New Delhi", State = "Delhi", Country = "India", Latitude = 28.6100d, Longitude = 77.2110d }
                },
                new Pro
                {
                    ProName = "Raj Kumar",
                    Email = "raj.pro@example.com",
                    PhoneNumber = "+91 99876 12349",
                    BusinessName = "Raj Cleaning Services",
                    ServiceRadiusKm = 40,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    Address = new Models.Address { AddressType = "Pro", City = "New Delhi", State = "Delhi", Country = "India", Latitude = 28.6080d, Longitude = 77.2120d }
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
                    ContactPersonName = consumerUser?.FirstName,
                    ContactPersonPhone = consumerUser?.PhoneNumber,
                    CreatedAt = DateTime.UtcNow,
                    ServiceAddress = new Models.Address { AddressType = "Job", Street1 = "123 Main Street", City = "New Delhi", State = "Delhi", ZipPostalCode = "110001", Country = "India", Latitude = 28.6139d, Longitude = 77.2090d }
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
                    ContactPersonName = consumerUser?.FirstName,
                    ContactPersonPhone = consumerUser?.PhoneNumber,
                    CreatedAt = DateTime.UtcNow,
                    ServiceAddress = new Models.Address { AddressType = "Job", Street1 = "123 Main Street", City = "New Delhi", State = "Delhi", ZipPostalCode = "110001", Country = "India", Latitude = 28.6139d, Longitude = 77.2090d }
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
                    ContactPersonName = consumerUser?.FirstName,
                    ContactPersonPhone = consumerUser?.PhoneNumber,
                    CreatedAt = DateTime.UtcNow,
                    ServiceAddress = new Models.Address { AddressType = "Job", Street1 = "123 Main Street", City = "New Delhi", State = "Delhi", ZipPostalCode = "110001", Country = "India", Latitude = 28.6139d, Longitude = 77.2090d }
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

