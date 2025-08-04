using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Enums;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Document.API.Services.Implements
{
    public class BookmarkService : IBookmarkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<BookmarkService> _logger;
        private readonly IDocumentEnrichmentService _enrichmentService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BookmarkService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<BookmarkService> logger, IDocumentEnrichmentService enrichmentService, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _enrichmentService = enrichmentService;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Gets the current user's ID from JWT token
        /// </summary>
        /// <returns>User ID</returns>
        private string GetCurrentUserId()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            var userIdClaim = user?.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID not found in token");
            return userIdClaim;
        }

        public async Task AddBookmarkAsync(string documentId)
        {
            // Get current user ID from JWT token
            var userId = GetCurrentUserId();

            // 1. Check if the document version exists and is official
            var document = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(
                    predicate: d => d.Id == documentId
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentNotFound);

            // 2. Check if the bookmark already exists for this user and document version
            var existingBookmark = await _unitOfWork.GetRepository<Bookmark>()
                .SingleOrDefaultAsync(
                    predicate: b => b.UserId == userId && b.DocumentId == documentId
                );

            if (existingBookmark != null)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DocumentAlreadyBookmarked);
            }

            // 3. Create and save the new bookmark
            var bookmark = new Bookmark
            {
                UserId = userId,
                DocumentId = documentId,
                CreatedBy = userId
            };

            await _unitOfWork.GetRepository<Bookmark>().InsertAsync(bookmark);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("User {UserId} bookmarked document version {documentId}", userId, documentId);
        }

        public async Task RemoveBookmarkAsync(string documentId)
        {
            // Get current user ID from JWT token
            var userId = GetCurrentUserId();

            // 1. Find the bookmark to remove
            var bookmarkToRemove = await _unitOfWork.GetRepository<Bookmark>()
                .SingleOrDefaultAsync(
                    predicate: b => b.UserId == userId && b.DocumentId == documentId
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.BookmarkNotFound);

            // 2. Delete the bookmark
            _unitOfWork.GetRepository<Bookmark>().DeleteAsync(bookmarkToRemove);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("User {UserId} removed bookmark for document version {documentId}", userId, documentId);
        }

        public async Task<IPaginate<BookmarkResponse>> GetBookmarksAsync(int pageNumber, int pageSize)
        {
            // Get current user ID from JWT token
            var userId = GetCurrentUserId();

            var bookmarks = await _unitOfWork.GetRepository<Bookmark>().GetPagingListAsync(
                selector: b => new BookmarkResponse
                {
                    Id = b.Id,
                    DocumentId = b.Document.Id,
                    Title = b.Document.Title,
                    Description = b.Document.Description,
                    OwnerId = b.Document.OwnerId,
                    CreatedTime = b.CreatedTime
                },
                filter: null,
                predicate: b => b.UserId == userId,
                orderBy: q => q.OrderByDescending(b => b.CreatedTime),
                include: i => i.Include(b => b.Document),
                page: pageNumber,
                size: pageSize
            );

            // Enrich all bookmarks with names in bulk for better performance
            var enrichedBookmarks = await _enrichmentService.EnrichBookmarkResponsesAsync(bookmarks.Items.ToList());

            // Create new paginated result with enriched bookmarks
            var enrichedPaginated = new Paginate<BookmarkResponse>
            {
                Items = enrichedBookmarks,
                Page = bookmarks.Page,
                Size = bookmarks.Size,
                Total = bookmarks.Total,
                TotalPages = bookmarks.TotalPages
            };

            _logger.LogInformation("Enriched {Count} bookmarks with names for user {UserId}", enrichedBookmarks.Count, userId);
            return enrichedPaginated;
        }
    }
}