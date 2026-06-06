using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Contracts.Identity;
using Microsoft.IdentityModel.Tokens;
using Shared.DTOs.Identity;
using Shared.Options;

namespace Infrastructure.Identity;
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    public TokenService(JwtOptions jwtOptions)
    {
        _jwtOptions = jwtOptions ?? throw new ArgumentNullException(nameof(jwtOptions));
    }
    public TokenResponse GetToken(TokenRequest request)
    {
        var accessToken = GenerateAccessToken(request);
        var freshToken = GenerateRefreshToken();
        var ExpiresIn = _jwtOptions.ExpiryMinutes * 60; // Convert minutes to seconds for the response

        var result = new TokenResponse(accessToken, freshToken, ExpiresIn);

        return result;
    }

    private string GenerateAccessToken(TokenRequest request)
        => GenerateEncryptedToken(request);

    private string GenerateEncryptedToken(TokenRequest request)
    {
        var credentials = GetSigningCredentials();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, request.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(request.Roles.Select(role =>
          new Claim(ClaimTypes.Role, role)));

        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var token = new JwtSecurityToken(
           issuer: _jwtOptions.Issuer,
           audience: _jwtOptions.Audience,
           claims: claims,
           expires: accessTokenExpiry,
           signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];

        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        return Convert.ToBase64String(randomNumber);
    }

    private SigningCredentials GetSigningCredentials()
    {
        byte[] secret = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);
        return new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
    }

}
