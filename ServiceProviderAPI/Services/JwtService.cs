using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

public interface IJwtService
{
    string GenerateToken(object user, string role);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(object user, string role)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // resolve display name safely (User has FirstName/LastName, Pro has ProName)
        string displayName = string.Empty;
        try
        {
            var userType = user.GetType();
            var firstNameProp = userType.GetProperty("FirstName");
            var proNameProp = userType.GetProperty("ProName");
            var nameProp = userType.GetProperty("Name");

            if (firstNameProp != null)
            {
                var val = firstNameProp.GetValue(user);
                displayName = val?.ToString() ?? string.Empty;
            }
            else if (proNameProp != null)
            {
                var val = proNameProp.GetValue(user);
                displayName = val?.ToString() ?? string.Empty;
            }
            else if (nameProp != null)
            {
                var val = nameProp.GetValue(user);
                displayName = val?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            displayName = string.Empty;
        }

        // Get Id and Email via reflection to avoid dynamic binder issues
        string id = string.Empty;
        string email = string.Empty;
        try
        {
            var idProp = user.GetType().GetProperty("Id");
            var emailProp = user.GetType().GetProperty("Email");
            if (idProp != null)
            {
                var idVal = idProp.GetValue(user);
                id = idVal?.ToString() ?? string.Empty;
            }
            if (emailProp != null)
            {
                var emailVal = emailProp.GetValue(user);
                email = emailVal?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // leave id/email empty
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, displayName),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
