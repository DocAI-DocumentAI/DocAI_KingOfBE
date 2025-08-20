using AutoMapper;
using Microsoft.Extensions.Logging;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;
using Notification.Infrastructure.Repository.Interfaces;
using System.Linq.Expressions;

namespace Notification.API.Services.Implement
{
    public class NotificationReadService : INotificationReadService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<NotificationReadService> _logger;
        private readonly IMapper _mapper;

        public NotificationReadService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            ILogger<NotificationReadService> logger,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<NotificationLog>();
                var notification = await repo.SingleOrDefaultAsync(predicate: n => n.Id == notificationId);

                if (notification == null)
                    throw new ArgumentException($"Notification {notificationId} not found");

                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                    repo.UpdateAsync(notification);
                    await _unitOfWork.CommitAsync();

                    _logger.LogDebug("Marked notification {Id} as read", notificationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {Id} as read", notificationId);
                throw;
            }
        }

        public async Task MarkAllAsReadAsync(string userEmail)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<NotificationLog>();

                // ✅ Sử dụng GetListAsync từ GenericRepository
                var unreadNotifications = await repo.GetListAsync(
                    predicate: n => n.RecipientAddress == userEmail &&
                                   n.IsSent == true &&
                                   n.IsRead == false);

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                    repo.UpdateAsync(notification);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Marked {Count} notifications as read for {UserEmail}",
                    unreadNotifications.Count, userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for {UserEmail}", userEmail);
                throw;
            }
        }

        public async Task<int> GetUnreadCountAsync(string userEmail)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<NotificationLog>();

                // ✅ Sử dụng AnyAsync để count - cần tự implement count
                var unreadNotifications = await repo.GetListAsync(
                    predicate: n => n.RecipientAddress == userEmail &&
                                   n.IsSent == true &&
                                   n.IsRead == false);

                return unreadNotifications.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for {UserEmail}", userEmail);
                return 0;
            }
        }

        public async Task<IPaginate<NotificationResponse>> GetUserNotificationsAsync(
            string userEmail, int page, int size, bool? isRead = null)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<NotificationLog>();

                // ✅ Build predicate based on parameters
                Expression<Func<NotificationLog, bool>> predicate;

                if (isRead.HasValue)
                {
                    predicate = n => n.RecipientAddress == userEmail &&
                                    n.IsSent == true &&
                                    n.IsRead == isRead.Value;
                }
                else
                {
                    predicate = n => n.RecipientAddress == userEmail && n.IsSent == true;
                }

                // ✅ Sử dụng GetPagingListAsync từ GenericRepository
                var notifications = await repo.GetPagingListAsync(
                    selector: n => _mapper.Map<NotificationResponse>(n),
                    filter: null, // Không dùng filter
                    predicate: predicate,
                    orderBy: null, // Sẽ dùng sortBy thay thế
                    include: null,
                    page: page,
                    size: size,
                    sortBy: "SentAt", // ✅ Sử dụng sortBy
                    isAsc: false // ✅ Mới nhất trước
                );

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for {UserEmail}", userEmail);
                throw;
            }
        }
    }
}
