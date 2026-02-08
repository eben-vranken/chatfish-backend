using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BackEnd.DTOs;
using BackEnd.Models;
using dotenv.net.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;

namespace BackEnd.Services;

public class JwtService : IJwtService
{
    private readonly string _secret;

    public JwtService()
    {
        _secret = EnvReader.GetStringValue("JWT_SECRET");
    }

    public string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secret);
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim("userid", user.UserId.ToString()),
                new Claim("role", user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Audience = EnvReader.GetStringValue("JWT_AUDIENCE"),
            Issuer = EnvReader.GetStringValue("JWT_AUTHORITY"),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ObjectId? ValidateJwtToken(string? token)
    {
        if (token == null)
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secret);
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = ObjectId.Parse(jwtToken.Claims.First(x => x.Type == "userid").Value);
            
            return userId;
        }
        catch
        {
            return null;
        }
    }
}