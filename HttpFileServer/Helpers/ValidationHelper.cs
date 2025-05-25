using System.Text.RegularExpressions;
using HttpFileServer.Common;
using Microsoft.VisualBasic;
using Constants = HttpFileServer.Common.Constants;

namespace HttpFileServer.Helpers
{
    public static class ValidationHelper
    {
        private static readonly Regex IdValidationRegex = new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

        public static string? ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "ID is required";

            if (id.Length > Constants.MAX_ID_LENGTH)
                return $"ID exceeds maximum length of {Constants.MAX_ID_LENGTH}";

            if (!IdValidationRegex.IsMatch(id))
                return "ID contains invalid characters. Only a-z, A-Z, 0-9, dot, underscore, and minus are allowed";

            return null;
        }

        public static (Dictionary<string, string> StoredHeaders, string? Error) ValidateHeaders(IHeaderDictionary headers)
        {
            var storedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (headers.ContainsKey("Content-Type"))
            {
                var contentType = headers["Content-Type"].ToString();
                if (contentType.Length > Constants.MAX_HEADER_LENGTH)
                    return (storedHeaders, $"Content-Type header exceeds maximum length of {Constants.MAX_HEADER_LENGTH}");

                storedHeaders["Content-Type"] = contentType;
            }

            foreach (var header in headers)
            {
                if (header.Key.StartsWith("x-rebase-", StringComparison.OrdinalIgnoreCase))
                {
                    var headerValue = header.Value.ToString();
                    if (header.Key.Length + headerValue.Length > Constants.MAX_HEADER_LENGTH)
                        return (storedHeaders, $"Header {header.Key} exceeds maximum length of {Constants.MAX_HEADER_LENGTH}");

                    storedHeaders[header.Key] = headerValue;
                }
            }

            if (storedHeaders.Count > Constants.MAX_HEADER_COUNT)
                return (storedHeaders, $"Number of stored headers ({storedHeaders.Count}) exceeds maximum of {Constants.MAX_HEADER_COUNT}");

            return (storedHeaders, null);
        }
    }
}