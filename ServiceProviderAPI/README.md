# Service Provider API - Verification System

## Overview
The verification system provides email and phone verification functionality for both users and professionals in the Service Provider API. It generates time-limited verification codes, sends them via email/SMS, and validates them to confirm user contact information.

## Features
- Email verification
- Phone number verification
- Separate verification flows for users and professionals
- 6-digit verification codes
- 15-minute code expiration
- One-time use codes
- Automatic user/pro verification status updates

## Components

### 1. VerificationCode Model
Located in `Models/VerificationCode.cs`, stores:
- Unique verification codes
- Email/phone number being verified
- Expiration timestamp
- Usage status
- Verification type (Email/Phone)
- User type (User/Pro)

### 2. Verification Service
Located in `Services/VerificationService.cs`, provides:
- Code generation and storage
- Email/SMS sending
- Code verification
- User/Pro status updates

### 3. Verification Controller
Located in `Controllers/VerificationController.cs`, exposes endpoints:
```
POST /api/verification/send-email-code
POST /api/verification/send-phone-code
POST /api/verification/verify-email
POST /api/verification/verify-phone
```

## API Endpoints

### Send Email Verification Code
```http
POST /api/verification/send-email-code
Content-Type: application/json

{
    "contact": "user@example.com",
    "userType": "User"  // or "Pro"
}
```

### Send Phone Verification Code
```http
POST /api/verification/send-phone-code
Content-Type: application/json

{
    "contact": "+1234567890",
    "userType": "User"  // or "Pro"
}
```

### Verify Email
```http
POST /api/verification/verify-email
Content-Type: application/json

{
    "contact": "user@example.com",
    "code": "123456",
    "userType": "User"  // or "Pro"
}
```

### Verify Phone
```http
POST /api/verification/verify-phone
Content-Type: application/json

{
    "contact": "+1234567890",
    "code": "123456",
    "userType": "User"  // or "Pro"
}
```

## Configuration

### Email Settings
In `appsettings.json`:
```json
{
  "Email": {
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "Username": "your-email@example.com",
    "Password": "your-email-password",
    "From": "noreply@yourapp.com"
  }
}
```

## Development vs Production

### Development Mode
- Email sending is disabled
- SMS sending is disabled
- Verification codes are logged to the console

### Production Setup Required
1. Configure SMTP server settings in `appsettings.json`
2. Implement SMS service (e.g., Twilio) in `SendVerificationSms`
3. Uncomment email sending code in `SendVerificationEmail`
4. Update security settings as needed

## Security Considerations
- Verification codes expire after 15 minutes
- Codes can only be used once
- Failed verification attempts are logged
- Rate limiting should be implemented in production
- Secure your email/SMS service credentials

## Database Tables
The system uses the following tables:
- VerificationCodes: Stores verification codes and their status
- Users/Pros: Contains verification status flags (IsEmailVerified, IsPhoneVerified)

## Dependencies
- .NET 8
- Entity Framework Core
- System.Net.Mail for email functionality
