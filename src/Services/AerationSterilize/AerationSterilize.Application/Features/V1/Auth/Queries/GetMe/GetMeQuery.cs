using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Auth.Queries.GetMe;
public sealed record GetMeQuery : IQuery<MeDto>;
