using System;

namespace Auth.API.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class SkipRateLimitAttribute : Attribute
    {
    }
}