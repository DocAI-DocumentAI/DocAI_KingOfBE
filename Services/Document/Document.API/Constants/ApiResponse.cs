using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.API.Constants
{
    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    /// <typeparam name="T">Type of the data being returned</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// HTTP status code
        /// </summary>
        [Required]
        public int StatusCode { get; set; }
        
        /// <summary>
        /// Error code if an error occurred
        /// </summary>
        public string? ErrorCode { get; set; }
        
        /// <summary>
        /// Response message
        /// </summary>
        [Required]
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Response data
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <param name="data">Response data</param>
        /// <param name="message">Success message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>ApiResponse instance</returns>
        public static ApiResponse<T> Success(T data, string message = "Success", int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                StatusCode = statusCode,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="errorCode">Error code</param>
        /// <param name="message">Error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>ApiResponse instance</returns>
        public static ApiResponse<T> Error(string errorCode, string message, int statusCode = 500)
        {
            return new ApiResponse<T>
            {
                StatusCode = statusCode,
                ErrorCode = errorCode,
                Message = message,
            };
        }
    }
}
