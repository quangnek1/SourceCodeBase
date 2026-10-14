using AerationSterilize.Application.Features.V1.Users.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Users.Queries.GetUsers;
internal sealed class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, PagedResult<UserSummaryDto>>
{
    private readonly UserManager<AppUser> _userManager;

    public GetUsersQueryHandler(UserManager<AppUser> userManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<Result<PagedResult<UserSummaryDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            users = users.Where(u => u.UserName!.Contains(term) || u.Email!.Contains(term) || u.FullName.Contains(term));
        }

        var query = users
            .OrderBy(u => u.UserName)
            .Select(u => new UserSummaryDto(
                u.Id,
                u.UserName!,
                u.Email,
                u.FullName,
                u.UserRoles.Select(ur => ur.RoleId).ToList()));

        var result = await PagingExtensions.ToPagedResultAsync(query, request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
