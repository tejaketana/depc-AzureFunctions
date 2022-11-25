using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace MessageTrigger
{
    public static class ReadMessage
    {
        [FunctionName("subscribe")]
        public static async Task Run([EventHubTrigger("%EventHubName%", ConsumerGroup = "%MSConsumerGroup%", Connection = "EventHubNamespace")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            bool processEvent = false;
            string eventTypeKey = Environment.GetEnvironmentVariable("EventTypeKey");
            string eventTypeValue = Environment.GetEnvironmentVariable("EventTypeValue");

            string filterKey = Environment.GetEnvironmentVariable("FilterKey").ToString();
            string[] filters = Environment.GetEnvironmentVariable("Filters").Replace(" ", String.Empty).Split(",");

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    log.LogInformation($"C# Event Hub trigger function read message: {messageBody}");

                    // Check if Event is to be processed.
                    if (eventTypeKey != null && eventTypeValue != null)
                    {
                        for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                        {
                            // Filter string: EventType\":\"PackageApplication and status\":\"MigrationPending. 
                            // Pattern:  @"\b" + eventTypeKey + @""":\s*""" + eventTypeValue + @"\b" && @"\b" + filterKey + @""":\s*""" + filters[filterIndex] + @"\b"
                            if (Regex.Match(messageBody, @"\b" + eventTypeKey + @""":\s*""" + eventTypeValue + @"\b").Success && Regex.Match(messageBody, @"\b" + filterKey + @""":\s*""" + filters[filterIndex] + @"\b").Success)
                            {
                                processEvent = true;
                            }
                        }
                    }
                    else
                    {
                        for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                        {
                            // Filter string: status\":\"MigrationPending. 
                            // Pattern: @"\b" + filterKey + @""":\s*""" + filters[filterIndex] + @"\b"
                            if (Regex.Match(messageBody, @"\b" + filterKey + @""":\s*""" + filters[filterIndex] + @"\b").Success)
                            {
                                processEvent = true;
                            }
                        }
                    }


                    if (processEvent)
                    {
                        log.LogInformation($"C# Event Hub trigger function is processing message: {messageBody}");

                        // Re-init the flag, so status values for every event is evaluated.
                        processEvent = false;

                        // REST call to the Target Microservice.
                        using (var httpClient = new HttpClient())
                        {
                            using (var request = new HttpRequestMessage(HelperMethods.GetHttpMethod(Environment.GetEnvironmentVariable("UseHttpMethod")), Environment.GetEnvironmentVariable("TargetServiceUrl")))
                            {
                                using (var content = new StringContent(messageBody, Encoding.UTF8, Environment.GetEnvironmentVariable("ContentType")))
                                {
                                    request.Content = content;

                                    // TODO:
                                    // Implement Retry logic

                                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                                    {
                                        response.EnsureSuccessStatusCode();

                                        log.LogInformation($"C# Event Hub trigger function received response: {response}");
                                    }
                                }
                            }
                        }

                        // Added a delay of 3 seconds.
                        await Task.Delay(3000);
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
    }
}
