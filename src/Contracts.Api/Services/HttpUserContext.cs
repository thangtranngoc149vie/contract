using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Contracts.Api.Services;

public sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("No active HttpContext");
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            throw new InvalidOperationException("User id claim is missing");
        }

        return Guid.Parse(userId);
    }
}
