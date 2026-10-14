using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.RefreshToken;
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginDto>;

