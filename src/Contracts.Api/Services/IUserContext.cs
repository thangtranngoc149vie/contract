namespace Contracts.Api.Services;

public interface IUserContext
{
    Guid GetCurrentUserId();
}
