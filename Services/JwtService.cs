// JwtService.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Gemini.Services
{
    //public class JwtService(IConfiguration configuration)
    //{
    //    private readonly IConfiguration _configuration = configuration;

    //    public string GenerateToken(string username)
    //    {
    //        var jwtIssuer = _configuration["Jwt:Issuer"];
    //        var jwtAudience = _configuration["Jwt:Audience"];
    //        var jwtKey = _configuration["Jwt:Key"] ?? new Guid().ToString(); //Zufälliger Schlüssel, falls kein Key aus Konfiguration

    //        var tokenHandler = new JwtSecurityTokenHandler();
    //        var key = Encoding.UTF8.GetBytes(jwtKey);
    //        var tokenDescriptor = new SecurityTokenDescriptor
    //        {
    //            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, username)]),
    //            Expires = DateTime.UtcNow.AddMinutes(15),
    //            Issuer = jwtIssuer,
    //            Audience = jwtAudience,
    //            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    //        };
    //        var token = tokenHandler.CreateToken(tokenDescriptor);
    //        return tokenHandler.WriteToken(token);
    //    }
    //}

    public static class JwtExtensions
    {
        //    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        //    {
        //        // Werte aus appsettings.json oder Umgebungsvariablen
        //        var key = config["Jwt:Key"] ?? "StandardSichererSchlüssel1234567890!";
        //        var issuer = config["Jwt:Issuer"] ?? "meine-api";
        //        var audience = config["Jwt:Audience"] ?? "meine-nutzer";

        //        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        //            .AddJwtBearer(options =>
        //            {
        //                options.TokenValidationParameters = new TokenValidationParameters
        //                {
        //                    ValidateIssuer = true,
        //                    ValidateAudience = true,
        //                    ValidateLifetime = true,
        //                    ValidateIssuerSigningKey = true,
        //                    ValidIssuer = issuer,
        //                    ValidAudience = audience,
        //                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        //                };
        //            });

        //        services.AddAuthorizationBuilder();

        //        return services;
        //    }
        //}

        // Hilfsmethode & AOT Typen
        internal static string GenerateJwt(string user, byte[] key)
        {
            var claims = new[] { new Claim(ClaimTypes.Name, user) };
            var sKey = new SymmetricSecurityKey(key);
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                claims: claims, expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(sKey, SecurityAlgorithms.HmacSha256));
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}