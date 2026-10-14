using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Login;
public sealed record LoginCommand(string UserName, string Password) : ICommand<LoginDto>;
