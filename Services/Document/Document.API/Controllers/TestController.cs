using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Chỉ cho phép user đã login test
    public class TestController : ControllerBase
    {
        private readonly IDocumentRAGService _ragService;
        private readonly ILogger<TestController> _logger;

        public TestController(IDocumentRAGService ragService, ILogger<TestController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        /// <summary>
        /// Test document indexing status - Admin only
        /// </summary>
        [HttpGet("document-index")]
        public async Task<IActionResult> TestDocumentIndex()
        {
            try
            {
                var result = await _ragService.TestDocumentIndexAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test document index failed");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Test RAG search with simple query
        /// </summary>
        [HttpPost("rag-search")]
        public async Task<IActionResult> TestRAGSearch([FromBody] string query)
        {
            if (string.IsNullOrEmpty(query))
                return BadRequest("Query is required");

            try
            {
                // Get user info (simplified - adapt theo JWT helper của bạn)
                var userId = "test-user"; // Replace with actual user ID from JWT

                var result = await _ragService.GetRAGAnswerWithSourcesAsync(query, userId);
                return Ok(new { success = true, query, answer = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test RAG search failed for query: {Query}", query);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
