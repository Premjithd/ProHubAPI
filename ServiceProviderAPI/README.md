# ProHubAPI — ServiceProviderAPI

ASP.NET Core 8 backend for the yProHub professional services marketplace.

## Technology Stack

- ASP.NET Core 8, C# 13 (nullable reference types, implicit usings)
- Entity Framework Core 8 + SQL Server
- JWT Bearer authentication with token blacklisting
- Razorpay payment integration
- Msg91 SMS + SMTP email (console-only in dev)
- Nominatim proxy for address autofill

## Setup

```bash
dotnet restore
dotnet watch run        # https://localhost:7042 — migrations auto-apply on startup
```

Swagger UI: `https://localhost:7042/swagger`

Add a new migration:

```bash
dotnet build                                    # always build first
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Architecture

**Layered**: Controllers → Services → EF Core `ApplicationDbContext`

**Plugin abstractions** (`Services/Abstractions/`):
- `IPaymentProvider` — Razorpay implementation
- `INotificationChannel` — SMTP and Msg91 implementations
- `IFileStorageService`, `IInsuranceProvider` — placeholder interfaces

## API Surface

| Controller | Prefix | Responsibility |
|---|---|---|
| AuthController | `/api/auth` | Login, registration, JWT issue, logout (token revocation) |
| VerificationController | `/api/verification` | Send/verify email and phone codes |
| UsersController | `/api/users` | User profile CRUD |
| ProsController | `/api/pros` | Pro profile, service categories, service areas |
| JobsController | `/api/jobs` | Job posting, bidding, status lifecycle |
| JobCompletionController | `/api/jobcompletion` | Completion sign-off flow |
| JobInsuranceController | `/api/jobinsurance` | Insurance records per job |
| JobNotificationController | `/api/notifications` | Job activity notifications |
| MessagesController | `/api/messages` | Direct messaging (MessageIndex + Message) |
| PaymentsController | `/api/payments` | Razorpay order creation, verification |
| MaterialsController | `/api/materials` | Job materials tracking |
| ServicesController | `/api/services` | Service category listing |
| AdminController | `/api/admin` | Category CRUD, service area CRUD, user management, refunds, disputes, settings |
| AddressController | `/api/address` | Nominatim proxy for address autofill |

## Key Features

### Authentication & Verification
- JWT tokens include a `jti` claim. On logout, `jti` is written to `RevokedTokens`; an `OnTokenValidated` hook rejects blacklisted tokens on every request.
- Verification: 6-digit codes, 15-minute expiry, one-time use; separate flows for users and pros.
- Two-step pro registration: create account, then complete profile.

### Service Categories
- Admin-managed via `AdminController`; seeded on startup by `SeedDataService`.
- Exposes pro counts per category for browse/search pages.

### Service Areas
- Country → State → District → PIN hierarchy.
- Admin CRUD; pros and jobs validated against registered areas.
- Find-a-pro filtered by service area; seeded with Trivandrum data.

### Payments & Refunds
- Razorpay order creation and signature verification.
- Refund tracking per payment; admin refund action and dispute UI.
- `RateSplitService` calculates platform fee vs. pro payout.

### Address Autofill
- `AddressController` proxies requests to Nominatim (OpenStreetMap) to keep the API key server-side.

## Data Models (EF Entities)

`User`, `Pro`, `Job`, `JobBid`, `JobCompletion`, `JobInsurance`, `JobNotification`, `Service`, `ServiceArea`, `Message`, `MessageIndex`, `Payment`, `RevokedToken`, `VerificationCode`

## Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ServiceProviderDB;..." },
  "Jwt": { "Key": "...", "Issuer": "https://localhost:7042", "Audience": "https://localhost:7042" },
  "Email": { "SmtpServer": "...", "Port": 587, "Username": "...", "Password": "...", "From": "..." },
  "Payment": { "Razorpay": { "KeyId": "...", "KeySecret": "..." } }
}
```

Override with `appsettings.Development.json` for local dev.

## Production Checklist

- Replace the example JWT secret key.
- Restrict CORS (dev uses `AllowAnyOrigin()`).
- Configure SMTP and Msg91 credentials (console-only in dev).
- Swap Razorpay test keys for live keys.
- Remove or secure the Swagger endpoint.
