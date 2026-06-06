namespace AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
public sealed record LoginDto(string AccessToken, string RefreshToken, double ExpiresIn);
