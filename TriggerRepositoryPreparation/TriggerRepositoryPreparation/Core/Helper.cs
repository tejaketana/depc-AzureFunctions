using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TriggerRepositoryPreparation.Models.Input;

namespace TriggerRepositoryPreparation.Core
{
    public static class Helper
    {
        public static HttpMethod GetHttpMethod(string method)
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

        public async static Task SendEvent(DoDeployPendingEvent triggerMessage, ILogger log)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    string keyValue = Environment.GetEnvironmentVariable("EventHubNamespaceKeyValue");
                    string sasToken = CreateToken(Environment.GetEnvironmentVariable("EventHubNamespaceUrl") + Environment.GetEnvironmentVariable("ResourceUri"), Environment.GetEnvironmentVariable("EventHubNamespaceKeyName"), keyValue);

                    HttpRequestMessage requestPost = new HttpRequestMessage(new HttpMethod(Environment.GetEnvironmentVariable("UseHttpMethod")), "");
                    httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("EventHubNamespaceUrl"));
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("ContentTypeKey"), Environment.GetEnvironmentVariable("ContentType"));
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("HostKey"), Environment.GetEnvironmentVariable("HostValue"));
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("AuthKey"), sasToken);

                    var contentEventSend = new StringContent(JsonConvert.SerializeObject(triggerMessage));
                    var responseFromEventHub = await httpClient.PostAsync(Environment.GetEnvironmentVariable("ResourceUri"), contentEventSend);

                    log.LogInformation($"DoDeploy Event Hub trigger function posted response to EventHub : {responseFromEventHub.ToString()}");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"DoDeploy Event Hub trigger function posted response to EventHub : {ex}");
                throw ex;
            }
        }

        #region Private Methods
        private static string CreateToken(string resourceUri, string keyName, string key)
        {
            TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var week = 60 * 60 * 24 * 7;
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            string stringToSign = System.Web.HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, keyName);
            return sasToken;
        }
        #endregion
    }
}
