using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Exceptions
{
    public class ErrorCode
    {
        public const string NOT_FOUND = "NOT_FOUND";
        public const string SUCCESS = "SUCCESS";
        public const string FAILED = "FAILED";
        public const string EXISTED = "EXISTED";
        public const string DUPLICATE = "DUPLICATE";
        public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
        public const string INVALID_INPUT = "INVALID_INPUT";
        public const string UNAUTHORIZED = "UNAUTHORIZED";
        public const string BADREQUEST = "BADREQUEST";
        public const string CONFLICT = "CONFLICT";
        public const string FORBIDDEN = "FORBIDDEN";
    }
}
