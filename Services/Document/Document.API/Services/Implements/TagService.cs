using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Document.API.Constants; // For StatusCodes

namespace Document.API.Services.Implements
{
    public class TagService : ITagService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TagService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<TagResponse> CreateTagAsync(CreateTagRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.TagNameCannotBeEmpty);
            }

            var normalizedTagName = request.Name.ToLowerInvariant();
            var existingTag = await _unitOfWork.GetRepository<Tag>()
                .SingleOrDefaultAsync(predicate: t => t.Name == normalizedTagName);

            if (existingTag != null)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.TagWithNameExists);
            }

            var tag = new Tag
            {
                Name = normalizedTagName,
                CreatedBy = userId
            };

            await _unitOfWork.GetRepository<Tag>().InsertAsync(tag);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<TagResponse>(tag);
        }

        public async Task<TagResponse> GetTagByIdAsync(string tagId)
        {
            var tag = await _unitOfWork.GetRepository<Tag>()
                .SingleOrDefaultAsync(predicate: t => t.Id == tagId)
                ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.TagNotFound);

            return _mapper.Map<TagResponse>(tag);
        }

        public async Task<IPaginate<TagResponse>> GetAllTagsAsync(int pageNumber, int pageSize)
        {
            var tags = await _unitOfWork.GetRepository<Tag>().GetPagingListAsync(
                selector: t => _mapper.Map<TagResponse>(t),
                filter: null,
                orderBy: q => q.OrderBy(t => t.Name),
                page: pageNumber,
                size: pageSize
            );

            return tags;
        }

        public async Task<TagResponse> UpdateTagAsync(string tagId, UpdateTagRequest request, string userId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.TagNameCannotBeEmpty);
            }

            var tagToUpdate = await _unitOfWork.GetRepository<Tag>()
                .SingleOrDefaultAsync(predicate: t => t.Id == tagId)
                ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.TagNotFound);

            var normalizedTagName = request.Name.ToLowerInvariant();
            if (tagToUpdate.Name != normalizedTagName)
            {
                var existingTag = await _unitOfWork.GetRepository<Tag>()
                    .SingleOrDefaultAsync(predicate: t => t.Name == normalizedTagName);
                if (existingTag != null)
                {
                    throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.TagWithNameExists);
                }
            }

            tagToUpdate.Name = normalizedTagName;
            tagToUpdate.LastUpdatedBy = userId;
            tagToUpdate.LastUpdatedTime = DateTime.UtcNow;

            await _unitOfWork.GetRepository<Tag>().UpdateAsync(tagToUpdate);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<TagResponse>(tagToUpdate);
        }

        public async Task DeleteTagAsync(string tagId)
        {
            var tagToDelete = await _unitOfWork.GetRepository<Tag>()
                .SingleOrDefaultAsync(predicate: t => t.Id == tagId)
                ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.TagNotFound);

            // Check if the tag is associated with any documents
            var isTagUsed = await _unitOfWork.GetRepository<DocumentTag>()
                .CountAsync(predicate: dt => dt.TagId == tagId) > 0;

            if (isTagUsed)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CannotDeleteUsedTag);
            }

            _unitOfWork.GetRepository<Tag>().DeleteAsync(tagToDelete);
            await _unitOfWork.CommitAsync();
        }
    }
}
