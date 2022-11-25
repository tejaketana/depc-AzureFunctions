using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MCD.FN.ManageGit.Models
{
    /// <summary>
    /// Provides extension methods to manipulate Uri.
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// Append segments to Uri.
        /// </summary>
        /// <param name="uri">Instance of <see cref="Uri"/></param>
        /// <param name="segments">Segments to Append.</param>
        /// <returns>Instance of <see cref="Uri"/></returns>
        public static Uri Append(this Uri uri, params string[] segments)
        {
            return new Uri(segments.Aggregate(uri.AbsoluteUri, (current, path) => $"{current.TrimEnd('/')}/{path.TrimStart('/')}"));
        }

        /// <summary>
        /// Append a query string to a Uri
        /// </summary>
        /// <param name="uri">Instance of <see cref="Uri"/></param>
        /// <param name="query">The query to be appended into <paramref name="uri"/></param>
        /// <returns>Instance of <see cref="Uri"/></returns>
        public static Uri AppendQuery(this Uri uri, string query)
        {
            return new UriBuilder(uri) { Query = query ?? string.Empty }.Uri;
        }
    }
}
