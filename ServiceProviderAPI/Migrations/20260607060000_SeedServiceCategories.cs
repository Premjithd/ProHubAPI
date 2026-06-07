using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    public partial class SeedServiceCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add icons to the 5 existing seeded categories
            migrationBuilder.Sql("UPDATE ServiceCategories SET Icon = N'🔧', UpdatedAt = GETUTCDATE() WHERE Name = 'Plumbing'   AND (Icon IS NULL OR Icon = '')");
            migrationBuilder.Sql("UPDATE ServiceCategories SET Icon = N'⚡', UpdatedAt = GETUTCDATE() WHERE Name = 'Electrical' AND (Icon IS NULL OR Icon = '')");
            migrationBuilder.Sql("UPDATE ServiceCategories SET Icon = N'🪟', UpdatedAt = GETUTCDATE() WHERE Name = 'Carpentry'  AND (Icon IS NULL OR Icon = '')");
            migrationBuilder.Sql("UPDATE ServiceCategories SET Icon = N'🎨', UpdatedAt = GETUTCDATE() WHERE Name = 'Painting'   AND (Icon IS NULL OR Icon = '')");
            migrationBuilder.Sql("UPDATE ServiceCategories SET Icon = N'🧹', UpdatedAt = GETUTCDATE() WHERE Name = 'Cleaning'   AND (Icon IS NULL OR Icon = '')");

            // Insert new categories (idempotent — skips if name already exists)
            var categories = new[]
            {
                ("Pest Control",              "Pest control and extermination services",           "🐜"),
                ("AC & Appliance Repair",     "Air conditioning and home appliance repair",        "❄️"),
                ("Interior Design",           "Home and office interior design and decoration",    "🏗️"),
                ("Hair & Beauty",             "Hair styling, makeup and beauty services",          "💇"),
                ("Massage & Spa",             "Therapeutic massage and spa treatments",            "💆"),
                ("Yoga & Fitness",            "Personal training, yoga and fitness coaching",      "🧘"),
                ("Babysitting & Childcare",   "Trusted babysitting and childcare services",        "👶"),
                ("Pet Care",                  "Pet grooming, sitting and veterinary assistance",   "🐾"),
                ("Tutoring",                  "Academic tutoring and coaching for all levels",     "📚"),
                ("IT Support & Repair",       "Computer setup, troubleshooting and IT support",   "💻"),
                ("Phone & Laptop Repair",     "Screen replacements, battery and hardware repairs","📱"),
                ("Music Lessons",             "Guitar, keyboard, vocals and other instruments",    "🎸"),
                ("Photography & Videography", "Event photography, portraits and video production","📷"),
                ("Vehicle Repair & Service",  "Car, bike and auto repair and maintenance",        "🚗"),
                ("Real Estate & Vastu",       "Property consulting, staging and Vastu services",  "🏠"),
                ("Movers & Packers",          "Home and office relocation and packing services",  "📦"),
                ("Security & CCTV",           "CCTV installation and security system setup",      "🔒"),
                ("Gardening & Landscaping",   "Garden maintenance, landscaping and planting",     "🌿"),
                ("Catering & Cooking",        "Home catering, personal chefs and cooking services","🍳"),
                ("Masonry & Tiling",          "Brick work, plastering and tile installation",     "🧱"),
                ("Waterproofing",             "Roof, terrace and basement waterproofing",         "🪣"),
                ("Bathroom Renovation",       "Full bathroom remodelling and fixture installation","🚿"),
                ("Locksmith",                 "Lock installation, repair and emergency unlock",   "🔑"),
            };

            foreach (var (name, description, icon) in categories)
            {
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM ServiceCategories WHERE Name = N'{name}') " +
                    $"INSERT INTO ServiceCategories (Name, Description, Icon, ServiceCount, IsActive, CreatedAt, UpdatedAt) " +
                    $"VALUES (N'{name}', N'{description}', N'{icon}', 0, 1, GETUTCDATE(), GETUTCDATE())");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var names = new[]
            {
                "Pest Control", "AC & Appliance Repair", "Interior Design", "Hair & Beauty",
                "Massage & Spa", "Yoga & Fitness", "Babysitting & Childcare", "Pet Care",
                "Tutoring", "IT Support & Repair", "Phone & Laptop Repair", "Music Lessons",
                "Photography & Videography", "Vehicle Repair & Service", "Real Estate & Vastu",
                "Movers & Packers", "Security & CCTV", "Gardening & Landscaping",
                "Catering & Cooking", "Masonry & Tiling", "Waterproofing",
                "Bathroom Renovation", "Locksmith",
            };

            foreach (var name in names)
                migrationBuilder.Sql($"DELETE FROM ServiceCategories WHERE Name = N'{name}'");

            migrationBuilder.Sql("UPDATE ServiceCategories SET Icon = NULL WHERE Name IN ('Plumbing','Electrical','Carpentry','Painting','Cleaning')");
        }
    }
}
