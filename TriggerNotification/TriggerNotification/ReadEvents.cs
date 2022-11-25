using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using TriggerNotification.Core;

namespace TriggerNotification
{
    public static class ReadEvents
    {
        [FunctionName("Notification")]
        public static async Task Run([EventHubTrigger("%EventHubName%", 
            ConsumerGroup = "%MSConsumerGroup%", 
            Connection = "EventHubNamespace")] EventData[] events, 
            ILogger log)
        {
            var exceptions = new List<Exception>();

            string processingEventType = string.Empty, processingEventStatus = string.Empty, restAPIUrl = string.Empty;

            // Future development - will be used for subscription to SmartUpdate events.
            //string smartUpdateEventTypeKey = Environment.GetEnvironmentVariable("SmartUpdateEventTypeKey");
            //string[] smartUpdateEventTypeValues = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SmartUpdateEventTypeValues").Trim()) ? null : Environment.GetEnvironmentVariable("SmartUpdateEventTypeValues").Replace(" ", string.Empty).Split(",");
            //string smartUpdateEventStatusKey = Environment.GetEnvironmentVariable("SmartUpdateEventStatusKey");
            //string[] smartUpdateEventStatusValues = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SmartUpdateEventStatusValues").Trim()) ? null : Environment.GetEnvironmentVariable("SmartUpdateEventStatusValues").Replace(" ", string.Empty).Split(",");

            // Future development - will be used for subscription to Workflow events.
            //string filterKey = Environment.GetEnvironmentVariable("FilterKey").ToString();
            //string[] filterValues = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FilterValues").Trim()) ? null : Environment.GetEnvironmentVariable("FilterValues").Replace(" ", String.Empty).Split(",");

            // Events for Notification service.
            string eventTypeKey = Environment.GetEnvironmentVariable("EventTypeKey");
            string[] eventTypeValues = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EventTypeValues").Trim()) ? null : Environment.GetEnvironmentVariable("EventTypeValues").Replace(" ", string.Empty).Split(",");
            string eventStatusKey = Environment.GetEnvironmentVariable("EventStatusKey").ToString();
            string[] eventStatusValues = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EventStatusValues").Trim()) ? null : Environment.GetEnvironmentVariable("EventStatusValues").Replace(" ", String.Empty).Split(",");

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    log.LogInformation($"Notification Event Hub trigger function read message: {messageBody}");

                    // This EventHub trigger needs to read 3 types of events:-
                    // 1. Events created for the Workflow steps,
                    // 2. Events from SmartUpdate, and
                    // 3. Events specifically for Notification service triggered by UI or deployment Completion.

                    // Implementation of no. 3.
                    if (eventTypeKey == null || eventTypeValues == null || eventStatusKey == null || eventStatusValues == null)
                    {
                        //log.LogError("Some or all configuration settings are missing: EventTypeKey, EventTypeValues, EventStatusKey, EventStatusValues");
                        throw new Exception("Some or all configuration settings are missing: EventTypeKey, EventTypeValues, EventStatusKey, EventStatusValues");
                    }
                    else
                    {
                        // Filter string:- eventType\":\"NotificationEmail .... eventStatus\":\"Pending 
                        // "eventType": "NotificationEmail"
                        // "eventType": "NotificationSMS"
                        // "eventStatus": "Pending"
                        for (int index = 0; index < eventTypeValues.Length; index++)
                        {
                            if (Regex.Match(messageBody, @"\b" + eventTypeKey + @""":\s*""" + eventTypeValues[index] + @"\b").Success
                                && Regex.Match(messageBody, @"\b" + eventStatusKey + @""":\s*""" + eventStatusValues[index] + @"\b").Success)
                            {
                                processingEventType = eventTypeValues[index].Trim();
                                processingEventStatus = eventStatusValues[index].Trim();

                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(processingEventType) && !string.IsNullOrEmpty(processingEventStatus))
                        {
                            // Lookup the REST API url from the configurations.
                            // Example event contains:
                            // eventType: NotificationEmail
                            // eventStatus: Pending
                            // REST API url configuration will be:
                            // "NotificationEmailPending": "{url}"
                            restAPIUrl = Environment.GetEnvironmentVariable(processingEventType + processingEventStatus);

                            if (!string.IsNullOrEmpty(restAPIUrl))
                            {
                                log.LogInformation($"C# Event Hub trigger function is processing message: {messageBody}");

                                var response = await HelperFunctions.PostToTargetUrl(restAPIUrl, messageBody, log);

                                if (response == System.Net.HttpStatusCode.OK)
                                    log.LogInformation("C# Event Hub trigger function completed processing message");
                            }
                            else
                            {
                                //log.LogError($"REST API url {restAPIUrl} not configured");
                                throw new Exception($"REST API url for '{processingEventType + processingEventStatus}' not configured");
                            }

                            // Re-init.
                            processingEventType = processingEventStatus = restAPIUrl = string.Empty;
                        }
                    }

                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    log.LogError($"Exception: {e}");
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
