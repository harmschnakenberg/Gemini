using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Gemini.Services
{
    //Quelle: https://dev.to/stevsharp/implementing-jwt-authentication-in-minimal-apis-4k8k


    //public class IdentityService(JwtConfiguration config)
    //{
    //    private readonly JwtConfiguration _config = config;

    //    public async Task<string> GenerateToken(string username)
    //    {
    //        await Task.Delay(100); // Simulate a database call

    //        var claims = new[]
    //        {
    //        new Claim(JwtRegisteredClaimNames.Sub, "123456"), // Example subject ID
    //        new Claim(JwtRegisteredClaimNames.Email, "admin@admin.gr"),
    //        new Claim(JwtRegisteredClaimNames.PreferredUsername, username)
    //    };

    //        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Secret));
    //        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    //        var token = new JwtSecurityToken(
    //            issuer: _config.Issuer,
    //            audience: _config.Audience,
    //            claims: claims,
    //            expires: DateTime.UtcNow.AddDays(_config.ExpireDays),
    //            signingCredentials: creds
    //        );

    //        return new JwtSecurityTokenHandler().WriteToken(token);
    //    }
    //}

    //public static class JwtAuthBuilderExtensions
    //{
    //    public static AuthenticationBuilder AddJwtAuthentication(this IServiceCollection services, JwtConfiguration jwtConfiguration)
    //    {
    //        services.AddAuthorization();

    //        return services.AddAuthentication(x =>
    //        {
    //            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    //            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    //        })
    //        .AddJwtBearer(x =>
    //        {
    //            x.SaveToken = true;
    //            x.TokenValidationParameters = new TokenValidationParameters
    //            {
    //                ValidateIssuer = true,
    //                ValidateAudience = true,
    //                ValidateLifetime = true,
    //                ValidateIssuerSigningKey = true,
    //                ValidIssuer = jwtConfiguration.Issuer,
    //                ValidAudience = jwtConfiguration.Audience,
    //                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfiguration.Secret)),
    //                RequireExpirationTime = true,
    //            };
    //        });
    //    }
    //}


    //public record JwtConfiguration
    //{
    //    public string Secret { get; set; } = string.Empty;
    //    public string Issuer { get; set; } = string.Empty;
    //    public string Audience { get; set; } = string.Empty;
    //    public int ExpireDays { get; set; } = 7;
    //}

    //public record LoginRequest
    //{
    //    public string Username { get; set; } = string.Empty;
    //    public string Password { get; set; } = string.Empty;
    //}


}
