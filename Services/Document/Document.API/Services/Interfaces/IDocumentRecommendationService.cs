using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for document recommendations
    /// </summary>
    public interface IDocumentRecommendationService
    {
        /// <summary>
        /// Get document recommendations based on a source document
        /// </summary>
        /// <param name="documentId">Source document ID</param>
        /// <param name="request">Recommendation request parameters</param>
        /// <param name="userId">Current user ID</param>
        /// <returns>Document recommendations result</returns>
        Task<DocumentRecommendationsResult> GetRecommendationsAsync(
            string documentId, 
            DocumentRecommendationRequest request, 
            string userId);

        /// <summary>
        /// Clear recommendation cache for a specific document
        /// </summary>
        /// <param name="documentId">Document ID to clear cache for</param>
        Task ClearRecommendationCacheAsync(string documentId);
    }
}
