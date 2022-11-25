using Newtonsoft.Json;
using PrepareGithubRepo.Models;
using PrepareGithubRepository.Models;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PrepareGithubRepository.Core
{
    /// <summary>
    /// Helper class for miscellaneous methods.
    /// </summary>
    public static class Helper
    {
        #region Public Methods
        /// <summary>
        /// Return GitHub repository name which is in the format "<Market>-<RtpType>".
        /// </summary>
        /// <param name="repoType"></param>
        /// <param name="market"></param>
        /// <param name="rtpType"></param>
        /// <returns></returns>
        public static string GetRepositoryName(string repoType, string market, string rtpType)
        {
            switch (repoType)
            {
                case "RTP":
                    return (Environment.GetEnvironmentVariable("RepositoryNamePrefix") + market + "-" + rtpType + Environment.GetEnvironmentVariable("RepositoryEnvironmentSuffix")).ToLower();
                case "RTPS":
                    return (Environment.GetEnvironmentVariable("RepositoryNamePrefix") + rtpType + Environment.GetEnvironmentVariable("RepositoryEnvironmentSuffix")).ToLower();
                case "RCT":
                    return (Environment.GetEnvironmentVariable("RepositoryNamePrefix") + repoType + "-" + rtpType + "-" + market + Environment.GetEnvironmentVariable("RepositoryEnvironmentSuffix")).ToLower();
                case "RCTS":
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Constructs the Event and calls the method to post the event to EventHub.
        /// </summary>
        /// <param name="storeObject"></param>
        /// <param name="success"></param>
        /// <returns></returns>
        public static HttpStatusCode SendEvent(StoreDeploymentDocument storeDeploymentObject, bool success)
        {
            // Construct the message.
            var statusMessage = new GitHubStatusMessage
            {
                message = $"id: {storeDeploymentObject.id}, market: {storeDeploymentObject.market}, deploymentId:{storeDeploymentObject.deploymentId} , storeId : {storeDeploymentObject.storeId} , workflowName: {storeDeploymentObject.workflowName}, workflowTemplate: {storeDeploymentObject.workflowTemplate}"
            };

            if (storeDeploymentObject.isCanceled == "true")
            {
                statusMessage.status = success ? Environment.GetEnvironmentVariable("CancelEventSuccess") : Environment.GetEnvironmentVariable("CancelEventFailed");
            }
            else if (storeDeploymentObject.rollback == "true")
            {
                statusMessage.status = success ? Environment.GetEnvironmentVariable("RollbackEventSuccess") : Environment.GetEnvironmentVariable("RollbackEventFailed");
            }
            else
            {
                statusMessage.status = success ? Environment.GetEnvironmentVariable("SuccessEvent") : Environment.GetEnvironmentVariable("FailedEvent");
            }

            return PostToEventHub(statusMessage);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Post the Success/Failed event for Repository Preparation.
        /// </summary>
        /// <param name="githubStatus"></param>
        /// <returns></returns>
        private static HttpStatusCode PostToEventHub(GitHubStatusMessage githubStatus)
        {
            string keyValue = Environment.GetEnvironmentVariable("EventHubNamespaceKeyValue");
            string sasToken = CreateToken(Environment.GetEnvironmentVariable("EventHubNamespaceUrl") + Environment.GetEnvironmentVariable("ResourceUri"), Environment.GetEnvironmentVariable("EventHubNamespaceKeyName"), keyValue);
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(Environment.GetEnvironmentVariable("RequestType")), "");

            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("EventHubNamespaceUrl"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("ContentTypeKey"), Environment.GetEnvironmentVariable("ContentTypeValue"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("HostKey"), Environment.GetEnvironmentVariable("HostValue"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("AuthKey"), sasToken);

            var content = new StringContent(JsonConvert.SerializeObject(githubStatus));
            var response = httpClient.PostAsync(Environment.GetEnvironmentVariable("ResourceUri"), content);

            return response.Result.StatusCode;
        }

        /// <summary>
        /// Creates a Token used as auth key to post events to the EventHub.
        /// </summary>
        /// <param name="resourceUri"></param>
        /// <param name="keyName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string CreateToken(string resourceUri, string keyName, string key)
        {
            TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var week = 60 * 60 * 24 * 7;
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, keyName);
            return sasToken;
        }
        #endregion
    }
}
