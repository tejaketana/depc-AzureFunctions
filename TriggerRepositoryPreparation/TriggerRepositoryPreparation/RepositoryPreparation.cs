using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TriggerRepositoryPreparation.Core;
using TriggerRepositoryPreparation.Models.Input;
using TriggerRepositoryPreparation.Models.Output;
using TriggerRepositoryPreparation.Services;

namespace TriggerRepositoryPreparation
{
    public static class RepositoryPreparation
    {
        [FunctionName("BuildStoreConfiguration")]
        public static async Task Run([EventHubTrigger("%EventHubName%", ConsumerGroup = "%MSConsumerGroup%", Connection = "EventHubNamespace")] EventData[] events, ILogger log)
        {
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
                        var pendingEvent = JsonConvert.DeserializeObject<DoDeployPendingEvent>(messageBody);

                        // Validate the input event, first.
                        if (await pendingEvent.IsValid(log))
                        {
                            // Reformat the input to save as "Input" entity type document in the DB.
                            StoreConfigurationDocument storeConfig = new StoreConfigurationDocument();
                            if (storeConfig.MapInputToDataModel(pendingEvent, log))
                            {
                                if (pendingEvent.status == GlobalConstants.FilterValues.DoDeployPending.ToString())
                                {
                                    // Create/Update the store config in the DB.
                                    // storeConfig + Mapping = Partial StoreConfiguration.
                                    if (await ConfigurationService.MapInputComponentsToTemplate(storeConfig, log))
                                    {
                                        // Save the Partial StoreConfiguration into "StoreConfigurationLogs" container.
                                        if (await ConfigurationService.LogInputEvent(storeConfig, log))
                                        {
                                            log.LogInformation($"Store Configuration successfully updated in {Environment.GetEnvironmentVariable("StoreConfigurationLogs_CollectionId")}");
                                        }
                                    }
                                }
                                else if (pendingEvent.IsActionEvent())
                                {
                                    // Update the property based on the Action event for the deployment+store.
                                    if (await ConfigurationService.UpdateDeployment(storeConfig, pendingEvent.status, log))
                                    {
                                        log.LogInformation($"Deployment successfully updated for deploymentId '{storeConfig.deploymentId}', storeId '{storeConfig.storeId}'");
                                    }
                                }
                            }
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
