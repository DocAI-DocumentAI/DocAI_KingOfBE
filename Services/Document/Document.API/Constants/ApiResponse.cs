using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.API.Constants
{
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        // Success constructor
        public static ApiResponse<T> Success(T data, string message = "Success", int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                StatusCode = statusCode,
                Message = message,
                Data = data
            };
        }

        // Error constructor
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
