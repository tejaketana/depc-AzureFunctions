using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TriggerDoDeploy.Models;

namespace TriggerDoDeploy
{
    public static class DoDeplouy
    {
        [FunctionName("DoDeploy")]
        public static async Task Run([EventHubTrigger("%EventHubName%", ConsumerGroup = "%MSConsumerGroup%", Connection = "EventHubNamespace")] EventData[] events, ILogger log)
        {
            HttpClient httpClient = new HttpClient();
            var exceptions = new List<Exception>();
            string[] filters = Environment.GetEnvironmentVariable("Filters").Replace(" ", String.Empty).Split(",");

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    log.LogInformation($"DoDeploy Event Hub trigger function read message: {messageBody}");

                    if (filters.Any(filterValue => messageBody.Contains(filterValue)))
                    {
                        log.LogInformation($"DoDeploy Event Hub trigger function is processing message: {messageBody}");
                        var triggerMessage = JsonConvert.DeserializeObject<TriggerMessage>(messageBody);

                        string[] messageArray = triggerMessage.message.Split(",");

                        // triggerMessage.components.Select(x=>x.location)==null || 
                        if (messageArray.FirstOrDefault(v => v.Contains("id")) == null || messageArray.FirstOrDefault(v => v.Contains("storeId")) == null || messageArray.FirstOrDefault(v => v.Contains("market")) == null 
                            || triggerMessage.components.Select(x=>x.version) == null || messageArray.FirstOrDefault(v => v.Contains("deploymentId")) == null)
                        {
                            log.LogError("Check id, storeId, market, components and deploymentId values - all are required");
                            triggerMessage.status = Environment.GetEnvironmentVariable("FailedResponse");
                            await SendEvent(triggerMessage, httpClient, log);
                            break;
                        }

                        string id = messageArray.FirstOrDefault(v => v.Contains("id")).Split(":")[1].Trim(),
                        storeId = messageArray.FirstOrDefault(v => v.Contains("storeId")).Split(":")[1].Trim(),
                        market = messageArray.FirstOrDefault(v => v.Contains("market")).Split(":")[1].Trim(),
                        deploymentId = messageArray.FirstOrDefault(v => v.Contains("deploymentId")).Split(":")[1].Trim(),
                        workflowTemplate = messageArray.FirstOrDefault(v => v.Contains("workflowTemplate")).Split(":")[1].Trim(),
                        workflowName = messageArray.FirstOrDefault(v => v.Contains("workflowName")).Split(":")[1].Trim(),
                        applyDate = messageArray.FirstOrDefault(v => v.Contains("applyDate")).Split(":", 2)[1].Trim(),
                        downloadDate = messageArray.FirstOrDefault(v => v.Contains("effectiveDateTime")).Split(":", 2)[1].Trim(),
                        profileRtpType = Environment.GetEnvironmentVariable("RtpType") == "DST" ? Environment.GetEnvironmentVariable("DSTRtpType") : Environment.GetEnvironmentVariable("SCRtpType");

                        // Reformat the Apply date & Download date for SmartUpdate.
                        // 2021-03-31T14:03:28Z
                        downloadDate = downloadDate.Replace("-", "").Replace("T", "").Replace(":", "").Substring(0, 14);
                        applyDate = applyDate.Replace("-", "").Replace("T", "").Replace(":", "").Substring(0, 8);

                        var components = triggerMessage.components.ToList();
                        List<Component> componentList = new List<Component>();
                        componentList.Add(new Component
                        {
                            name = Environment.GetEnvironmentVariable("NewPosLabel"),
                            version = components.Any(c => c.software.Trim() == Environment.GetEnvironmentVariable("NewPosComponent")) ? components.Where(c => c.software.Trim() == Environment.GetEnvironmentVariable("NewPosComponent")).Single().version : ""
                        });

                        componentList.Add(new Component
                        {
                            name = Environment.GetEnvironmentVariable("KioskLabel"),
                            version = components.Any(c => c.software.Trim() == Environment.GetEnvironmentVariable("KioskComponent")) ? components.Where(c => c.software.Trim() == Environment.GetEnvironmentVariable("KioskComponent")).Single().version : ""
                        });

                        // DeploymentId added into the Components collection to update into storeversion.json.
                        componentList.Add(new Component
                        {
                            name = Environment.GetEnvironmentVariable("DeploymentIdAsComponent"),
                            version = deploymentId
                        });
                        componentList.Add(new Component
                        {
                            name = Environment.GetEnvironmentVariable("ApplyDate"),
                            version = applyDate
                        });
                        componentList.Add(new Component
                        {
                            name = Environment.GetEnvironmentVariable("DownloadDate"),
                            version = downloadDate
                        });

                        DoDeployRequest doDeployRequest = new DoDeployRequest()
                        {
                            // "id" is the WorkFlow "id".
                            id = id,
                            deploymentId = deploymentId,
                            Components = componentList,
                            schedule = Environment.GetEnvironmentVariable("Schedule"),
                            market = market,
                            marketstoreid = storeId,
                            storeId = storeId.Replace(market, ""),
                            series = Environment.GetEnvironmentVariable("Series"),
                            createdBy = Environment.GetEnvironmentVariable("Series"),
                            description = messageArray.FirstOrDefault(v => v.Contains("deploymentName")).Split(":")[1].Trim(),
                            profile = $"{market}_{profileRtpType}_STORE",
                            region = Environment.GetEnvironmentVariable("Region"),
                            rtpType = Environment.GetEnvironmentVariable("RtpType"),
                            rtpVersion = Environment.GetEnvironmentVariable("RtpVersion"),
                            scheduledDate = "",
                            workflowTemplate = workflowTemplate,
                            workflowName = workflowName
                            
                        };

                        var requestBody = JsonConvert.SerializeObject(doDeployRequest);

                        log.LogInformation($"Logic App request : {requestBody}");

                        // REST call to the Target Microservice.
                        using (httpClient = new HttpClient())
                        {
                            using (var request = new HttpRequestMessage(HelperMethods.GetHttpMethod(Environment.GetEnvironmentVariable("UseHttpMethod")), Environment.GetEnvironmentVariable("TargetServiceUrl")))
                            {
                                using (var content = new StringContent(requestBody, Encoding.UTF8, Environment.GetEnvironmentVariable("ContentType")))
                                {
                                    request.Content = content;

                                    // TODO:
                                    // Implement Retry logic
                                    
                                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                                    {
                                        response.EnsureSuccessStatusCode();

                                        log.LogInformation($"DoDeploy Event Hub trigger function received Logic App response: {response.ToString()}");       

                                        if (response.IsSuccessStatusCode)
                                        {
                                            triggerMessage.status = Environment.GetEnvironmentVariable("SuccessResponse");
                                        }
                                        else
                                        {
                                            triggerMessage.status = Environment.GetEnvironmentVariable("FailedResponse");
                                        }

                                        //Sends Event to Event Hub
                                        await SendEvent(triggerMessage, httpClient, log);
                                    }
                                }
                            }
                            // Added a delay of 3 seconds.
                           // await Task.Delay(3000);
                        }
                    }
                    await Task.Yield();
                }

                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }


            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);
            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
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


        //Sends Event To Event Hub
        public async static Task SendEvent(TriggerMessage triggerMessage, HttpClient httpClient, ILogger log)
        {
            httpClient.Dispose();
            httpClient = new HttpClient();
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

            // Added a delay of 3 seconds.
            await Task.Delay(3000);
        } 
    }
}
