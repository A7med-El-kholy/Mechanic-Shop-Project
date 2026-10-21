using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MechanicShop.Infrastructure.Identity;

/// <summary>
/// Issues signed JWTs using the symmetric key configured under "JwtSettings".
/// The key never lives in source control — it is supplied via user-secrets locally
/// and via environment variables in deployment.
/// </summary>
public sealed class TokenProvider(IConfiguration configuration) : ITokenProvider
{
    private readonly IConfiguration _configuration = configuration;

    public Result<TokenResponseDto> GenerateAccessToken(AppUserDto user)
    {
        var settings = _configuration.GetSection("JwtSettings");

        var secret = settings["Secret"];
        var issuer = settings["Issuer"];
        var audience = settings["Audience"];
        var expirationMinutes = settings["ExpirationMinutes"];

        if (string.IsNullOrWhiteSpace(secret) ||
            string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            !int.TryParse(expirationMinutes, out var minutes))
        {
            // Misconfiguration is a bug, not a business failure — fail loudly.
            throw new InvalidOperationException(
                "JwtSettings (Secret, Issuer, Audience, ExpirationMinutes) must be configured.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("full_name", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResponseDto(accessToken, "Bearer", minutes * 60, user);
    }
}
