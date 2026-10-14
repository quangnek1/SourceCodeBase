using CommonServices.Application.Features.V1.Auth.Common.Dtos;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Auth.Commands.RefreshToken;
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginDto>;

