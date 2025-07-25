using AutoMapper;
using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class AIServiceMapper : Profile
    {
        public AIServiceMapper()
        {
            // Basic AI service mappings
            CreateMap<EstimateTokenRequest, object>()
                .ConvertUsing(src => new
                {
                    Input = src.Input,
                    SystemPrompt = src.SystemPrompt
                });

            CreateMap<object, TokenBreakdown>()
                .ConvertUsing(src => new TokenBreakdown
                {
                    InputTokens = 0,
                    OutputTokens = 0,
                    TotalTokens = 0,
                    EstimatedCost = 0.0m
                });
        }
    }
}
