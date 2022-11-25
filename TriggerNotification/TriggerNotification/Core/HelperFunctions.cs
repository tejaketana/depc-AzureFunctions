using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TriggerNotification.Core
{
    /// <summary>
    /// Helper functions.
    /// </summary>
    public static class HelperFunctions
    {
        #region Public Methods
        /// <summary>
        /// Make the REST API call.
        /// </summary>
        /// <param name="targetServiceUrl"></param>
        /// <param name="data"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<HttpStatusCode> PostToTargetUrl(string targetServiceUrl, string data, ILogger log)
        {
            try
            {
                // REST call to the Target Microservice.
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(GetHttpMethod(Environment.GetEnvironmentVariable("UseHttpMethod")), targetServiceUrl))
                    {
                        using (var content = new StringContent(data, Encoding.UTF8, Environment.GetEnvironmentVariable("ContentType")))
                        {
                            request.Content = content;

                            // TODO:
                            // Implement Retry logic

                            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                            {
                                response.EnsureSuccessStatusCode();
                                log.LogInformation($"C# Event Hub trigger function received response: {response}");

                                return response.StatusCode;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception making Http REST call: {ex}");
                throw ex;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Returns the Http method based on the configuration.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private static HttpMethod GetHttpMethod(string method)
        {
            switch (method.ToUpper())
            {
                case "GET":
                    return HttpMethod.Get;
                case "POST":
                    return HttpMethod.Post;
                case "PATCH":
                    return HttpMethod.Patch;
                case "PUT":
                    return HttpMethod.Put;
                case "HEAD":
                    return HttpMethod.Head;
                default:
                    return HttpMethod.Post;
            }
        }
        #endregion
    }
}
