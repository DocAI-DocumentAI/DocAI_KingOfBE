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

        public BookmarkService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<BookmarkService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task AddBookmarkAsync(string documentVersionId, string userId)
        {
            // 1. Check if the document version exists and is official
            var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: dv => dv.Id == documentVersionId && dv.IsOfficial,
                    include: i => i.Include(dv => dv.DocumentFile)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Official document version not found.");

            // 2. Check if the bookmark already exists for this user and document version
            var existingBookmark = await _unitOfWork.GetRepository<Bookmark>()
                .SingleOrDefaultAsync(
                    predicate: b => b.UserId == userId && b.DocumentVersionId == documentVersionId
                );

            if (existingBookmark != null)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, "Document already bookmarked by this user.");
            }

            // 3. Create and save the new bookmark
            var bookmark = new Bookmark
            {
                UserId = userId,
                DocumentVersionId = documentVersionId,
                CreatedBy = userId
            };

            await _unitOfWork.GetRepository<Bookmark>().InsertAsync(bookmark);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("User {UserId} bookmarked document version {DocumentVersionId}", userId, documentVersionId);
        }

        public async Task RemoveBookmarkAsync(string documentVersionId, string userId)
        {
            // 1. Find the bookmark to remove
            var bookmarkToRemove = await _unitOfWork.GetRepository<Bookmark>()
                .SingleOrDefaultAsync(
                    predicate: b => b.UserId == userId && b.DocumentVersionId == documentVersionId
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Bookmark not found.");

            // 2. Delete the bookmark
            _unitOfWork.GetRepository<Bookmark>().DeleteAsync(bookmarkToRemove);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("User {UserId} removed bookmark for document version {DocumentVersionId}", userId, documentVersionId);
        }

        public async Task<IPaginate<BookmarkResponse>> GetBookmarksAsync(string userId, int pageNumber, int pageSize)
        {
            var bookmarks = await _unitOfWork.GetRepository<Bookmark>().GetPagingListAsync(
                selector: b => new BookmarkResponse
                {
                    Id = b.Id,
                    DocumentVersionId = b.DocumentVersion.Id,
                    Title = b.DocumentVersion.Title,
                    VersionName = b.DocumentVersion.VersionName,
                    FilePath = b.DocumentVersion.FilePath,
                    FileName = b.DocumentVersion.FileName,
                    FileSize = b.DocumentVersion.FileSize,
                    FileType = b.DocumentVersion.FileType,
                    CreatedTime = b.CreatedTime
                },
                filter: null,
                predicate: b => b.UserId == userId,
                orderBy: q => q.OrderByDescending(b => b.CreatedTime),
                include: i => i.Include(b => b.DocumentVersion),
                page: pageNumber,
                size: pageSize
            );

            return bookmarks;
        }
    }
}