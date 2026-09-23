using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.Data.Models;
using MagicCSharp.UseCases;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface IGetNotificationsUseCase : IMagicUseCase
{
    Task<IReadOnlyList<Notification>> Execute(NotificationFilter filter);
    Task<Pagination<Notification>> Execute(PaginationRequest paginationRequest, NotificationFilter filter);
}

public class GetNotificationsUseCase(INotificationsRepository notificationsRepository) : IGetNotificationsUseCase
{
    public async Task<IReadOnlyList<Notification>> Execute(NotificationFilter filter)
    {
        return await notificationsRepository.Get(filter);
    }

    public async Task<Pagination<Notification>> Execute(PaginationRequest paginationRequest, NotificationFilter filter)
    {
        return await notificationsRepository.Get(paginationRequest, filter);
    }
}
