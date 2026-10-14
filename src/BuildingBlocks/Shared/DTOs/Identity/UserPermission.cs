using Shared.Common.Constants.Authorization;

namespace Shared.DTOs.Identity;
public sealed record UserPermission(string Code, AccessLevel Level);
