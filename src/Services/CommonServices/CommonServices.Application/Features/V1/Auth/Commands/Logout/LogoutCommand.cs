using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Auth.Commands.Logout;
public sealed record LogoutCommand(string RefreshToken) : ICommand;
