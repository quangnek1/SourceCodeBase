using AerationSterilize.Application.Features.V1.Users.Common.Dtos;
using Contracts.Common.Messages;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Users.Queries.GetUsers;
public sealed record GetUsersQuery(
    string? SearchTerm,
    int PageIndex,
    int PageSize) : IQuery<PagedResult<UserSummaryDto>>;
