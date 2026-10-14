using CommonServices.Application.Features.V1.Auth.Common.Dtos;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Auth.Commands.Login;
public sealed record LoginCommand(string UserName, string Password) : ICommand<LoginDto>;
