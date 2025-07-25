using AutoMapper;
using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.API.Payload.Response.DocumentServiceResponse;

namespace ChatBox.API.Mappers
{
    public class DocumentServiceMapper : Profile
    {
        public DocumentServiceMapper()
        {
            // Basic document service mappings
            CreateMap<DocumentSearchRequest, object>()
                .ConvertUsing(src => new
                {
                    Query = src.Query,
                    MaxResults = src.MaxResults,
                    IncludeContent = src.IncludeContent
                });

            CreateMap<DocumentAccessRequest, object>()
                .ConvertUsing(src => new
                {
                    DocumentId = src.DocumentId,
                    UserId = src.UserId,
                    AccessType = src.AccessType
                });
        }
    }
}
