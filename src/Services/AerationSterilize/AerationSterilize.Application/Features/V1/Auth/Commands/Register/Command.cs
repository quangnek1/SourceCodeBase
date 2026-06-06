using Contracts.Common.Messages;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Register;
public sealed record RegisterCommand(
        string UserName,
        string Password,
        string Email,
        string FirstName,
        string LastName,
        string FullName) : ICommand;
