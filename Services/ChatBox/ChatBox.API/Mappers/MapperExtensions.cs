using AutoMapper;
using ChatBox.Infrastructure.Paginate;
using System.Text.Json;

namespace ChatBox.API.Mappers
{
    /// <summary>
    /// Extension methods for AutoMapper to handle complex mapping scenarios
    /// </summary>
    public static class MapperExtensions
    {
        /// <summary>
        /// Maps paginated results from one type to another
        /// </summary>
        public static IPaginate<TDestination> MapPaginate<TSource, TDestination>(
            this IMapper mapper, 
            IPaginate<TSource> source)
        {
            var mappedItems = mapper.Map<IList<TDestination>>(source.Items);
            return new Paginate<TDestination>(mappedItems, source.Page, source.Size, source.Total);
        }

        /// <summary>
        /// Safely deserializes JSON string to Dictionary
        /// </summary>
        public static Dictionary<string, object> SafeDeserializeJson(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return new Dictionary<string, object>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) 
                       ?? new Dictionary<string, object>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Safely serializes Dictionary to JSON string
        /// </summary>
        public static string SafeSerializeJson(Dictionary<string, object>? dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
                return string.Empty;

            try
            {
                return JsonSerializer.Serialize(dictionary);
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Safely converts comma-separated string to List
        /// </summary>
        public static List<string> SafeStringToList(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return new List<string>();

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToList();
        }

        /// <summary>
        /// Safely converts List to comma-separated string
        /// </summary>
        public static string SafeListToString(List<string>? list)
        {
            if (list == null || list.Count == 0)
                return string.Empty;

            return string.Join(",", list.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Maps with null safety
        /// </summary>
        public static TDestination? SafeMap<TSource, TDestination>(
            this IMapper mapper, 
            TSource? source) 
            where TDestination : class
        {
            return source == null ? null : mapper.Map<TDestination>(source);
        }

        /// <summary>
        /// Maps collection with null safety
        /// </summary>
        public static List<TDestination> SafeMapList<TSource, TDestination>(
            this IMapper mapper, 
            IEnumerable<TSource>? source)
        {
            if (source == null)
                return new List<TDestination>();

            return mapper.Map<List<TDestination>>(source);
        }

        /// <summary>
        /// Ensures DateTime is in UTC
        /// </summary>
        public static DateTime EnsureUtc(DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) 
                : dateTime.ToUniversalTime();
        }

        /// <summary>
        /// Safely parses Guid from string
        /// </summary>
        public static Guid SafeParseGuid(string? value)
        {
            return Guid.TryParse(value, out var result) ? result : Guid.Empty;
        }

        /// <summary>
        /// Safely parses TimeSpan from string
        /// </summary>
        public static TimeSpan SafeParseTimeSpan(string? value)
        {
            return TimeSpan.TryParse(value, out var result) ? result : TimeSpan.Zero;
        }

        /// <summary>
        /// Safely parses enum from string
        /// </summary>
        public static TEnum SafeParseEnum<TEnum>(string? value, TEnum defaultValue = default) 
            where TEnum : struct, Enum
        {
            return Enum.TryParse<TEnum>(value, true, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Truncates string to specified length
        /// </summary>
        public static string SafeTruncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        /// <summary>
        /// Merges two dictionaries safely
        /// </summary>
        public static Dictionary<string, object> MergeDictionaries(
            Dictionary<string, object>? first, 
            Dictionary<string, object>? second)
        {
            var result = new Dictionary<string, object>();

            if (first != null)
            {
                foreach (var kvp in first)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            if (second != null)
            {
                foreach (var kvp in second)
                {
                    result[kvp.Key] = kvp.Value; // Second dictionary overwrites first
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a mapping context with additional data
        /// </summary>
        public static object CreateMappingContext<TSource, TDestination>(
            this IMapper mapper,
            TSource source,
            Dictionary<string, object>? additionalData = null)
        {
            // Simplified context creation
            return new { Source = source, AdditionalData = additionalData };
        }

        /// <summary>
        /// Maps with conditional logic
        /// </summary>
        public static TDestination MapWithCondition<TSource, TDestination>(
            this IMapper mapper,
            TSource source,
            Func<TSource, bool> condition,
            TDestination defaultValue)
        {
            return condition(source) ? mapper.Map<TDestination>(source) : defaultValue;
        }

        /// <summary>
        /// Maps and validates the result
        /// </summary>
        public static TDestination MapAndValidate<TSource, TDestination>(
            this IMapper mapper,
            TSource source,
            Func<TDestination, bool> validator)
        {
            var result = mapper.Map<TDestination>(source);
            
            if (!validator(result))
            {
                throw new InvalidOperationException($"Mapped object of type {typeof(TDestination).Name} failed validation");
            }

            return result;
        }
    }
}
