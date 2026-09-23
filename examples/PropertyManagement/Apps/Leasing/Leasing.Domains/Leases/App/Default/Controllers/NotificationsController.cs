using Acme.Leasing.Domains.Leases.App.Models;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Leasing.Domains.Leases.App.Controllers;

[ApiController]
[Route("notifications")]
public class NotificationsController : ControllerBase
{
    [HttpGet]
    public async Task<PaginationDto<NotificationDto>> GetNotifications(
        [FromQuery] PaginationRequestDto? pagination,
        [FromQuery] NotificationFilterDto? filter,
        [FromServices] IGetNotificationsUseCase getNotifications)
    {
        var paginationRequest = pagination?.ToRequest() ?? new PaginationRequestDto().ToRequest();
        var notificationFilter = filter?.ToFilter() ?? new NotificationFilter();

        var notifications = await getNotifications.Execute(paginationRequest, notificationFilter);
        return PaginationDto<NotificationDto>.From(notifications, NotificationDto.FromEntity);
    }
}
