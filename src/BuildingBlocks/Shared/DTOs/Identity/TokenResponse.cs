namespace Shared.DTOs.Identity;
/// <summary>
/// represents the response returned after a successful authentication, containing the access token, refresh token, and expiration information.
/// </summary>
/// <param name="AccessToken"></param>
/// <param name="RefreshToken"></param>
/// <param name="ExpiresIn">(ExpiresIn = Access Token)</param>
public record TokenResponse(string AccessToken, string RefreshToken, double ExpiresIn);

