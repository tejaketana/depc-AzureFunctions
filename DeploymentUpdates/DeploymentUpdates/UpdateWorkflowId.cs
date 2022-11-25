using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.EventHubs;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using DeploymentUpdates.Models;

namespace DeploymentUpdates
{
    public static class DeploymentUpdates
    {
        // CosmosDB client.
        private static Lazy<CosmosClient> lazyCosmosClient = new Lazy<CosmosClient>(InitializeCosmosClient);
        public static CosmosClient client => lazyCosmosClient.Value;
        private static Container _targetContainer = client.GetDatabase(Environment.GetEnvironmentVariable("Target_DatabaseId_Deployment")).GetContainer(Environment.GetEnvironmentVariable("Target_ContainerId_Deployment"));

        private static CosmosClient InitializeCosmosClient()
        {
            return new CosmosClient(Environment.GetEnvironmentVariable("CosmosDB_Connection"));
        }

        [FunctionName("UpdateWorkflowId")]
        public static async Task Run([EventHubTrigger("%EventHubName%", ConsumerGroup = "%MSConsumerGroup%",
            Connection = "EventHubNamespace")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            // Workflow event property --> status
            string workflowStepStatusKey = Environment.GetEnvironmentVariable("WorkflowStepStatusKey").ToString();
            string[] workflowStepStatusValues = Environment.GetEnvironmentVariable("WorkflowStepStatusValues").Replace(" ", String.Empty).Split(",");

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    log.LogInformation($"C# Event Hub trigger function read message: {messageBody}");

                    // Process Workflow events.
                    if (!string.IsNullOrEmpty(workflowStepStatusKey))
                    {
                        for (int workflowStepStatusValuesIndex = 0; workflowStepStatusValuesIndex < workflowStepStatusValues.Length; workflowStepStatusValuesIndex++)
                        {
                            if (Regex.Match(messageBody, @"\b" + workflowStepStatusKey + @""":\s*""" + workflowStepStatusValues[workflowStepStatusValuesIndex] + @"\b").Success)
                            {
                                log.LogInformation($"C# Event Hub trigger function is processing Workflow event: {messageBody}");

                                var workflowEvent = JsonConvert.DeserializeObject<WorkflowEvent>(messageBody);

                                Deployment result = null;
                                if (workflowEvent.status == "UpdateWorkflowId")
                                {
                                    // Call the update method.
                                    result = await UpdateWorkflowId(workflowEvent, log);
                                }

                                if (result == null)
                                    log.LogInformation("No Action");
                                else
                                    log.LogInformation($"Successfully saved: {messageBody}");
                            }
                        }
                    }

                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                    log.LogError($"Exception: {e}");
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        private static async Task<Deployment> UpdateWorkflowId(WorkflowEvent updateDeploymentEvent, ILogger log)
        {
            try
            {
                //Extract values from the messageArray
                string[] messageArray = updateDeploymentEvent.message.Split(",");

                string market = "",
                deploymentId = "",
                storeId = "",
                workflowTemplate = "",
                selectedWorkflowTemplate = "",
                id = "";

                for (int i = 0; i < messageArray.Length; i++)
                {
                    switch (messageArray[i].Split(":")[0].Trim())
                    {
                        case "market":
                            market = messageArray[i].Split(":")[1].Trim();
                            break;
                        case "deploymentId":
                            deploymentId = messageArray[i].Split(":")[1].Trim();
                            break;
                        case "storeId":
                            storeId = messageArray[i].Split(":")[1].Trim();
                            break;
                        case "workflowTemplate":
                            workflowTemplate = messageArray[i].Split(":")[1].Trim();
                            break;
                        case "selectedWorkflowTemplate":
                            selectedWorkflowTemplate = messageArray[i].Split(":")[1].Trim();
                            break;
                        case "id":
                            id = messageArray[i].Split(":")[1].Trim();
                            break;
                    }
                }

                string sqlQuery = string.Empty;
                // Some events may not contain the market value.
                if (market == string.Empty)
                    market = storeId.Substring(0, 2);

                sqlQuery = "SELECT * FROM c WHERE c.market = '" + market + "' AND c.deploymentId = '" + deploymentId + "'";

                var iterator = _targetContainer.GetItemQueryIterator<Deployment>(new QueryDefinition(sqlQuery));

                // Should have max 1 document...
                while (iterator.HasMoreResults)
                {
                    var patchOperations = new List<PatchOperation>();

                    foreach (var document in await iterator.ReadNextAsync())
                    {
                        // Is this a new document?
                        if (document.isRecordCreated == "true")
                        {
                            // Mark the Deployment as created = false, this Deployment is being updated.
                            patchOperations.Add(PatchOperation.Replace<string>("/isRecordCreated", "false"));
                        }

                        int workflowsCreatedValue = (workflowTemplate == Deployment.WorkflowTemplates.Initiation.ToString()
                                || workflowTemplate == Deployment.WorkflowTemplates.DependencyCheck.ToString())
                                    ? (int)Deployment.WorkflowTriggered.FutureDeploymentTriggered
                                    : (int)Deployment.WorkflowTriggered.CurrentDeploymentTriggered;

                        // Update only the first time this Deployment document is being updated for a Store.
                        if (Int32.Parse(document.workflowsCreated) != workflowsCreatedValue)
                        {
                            // Switch to the Workflow selected in the UI.
                            // WorkflowTemplate will be the same for all stores in the Deployment.
                            patchOperations.Add(PatchOperation.Replace<string>("/currentWorkflowTemplate", selectedWorkflowTemplate));

                            // Flag this deployment that workflow creation has been triggered.
                            patchOperations.Add(PatchOperation.Replace<string>("/workflowsCreated", workflowsCreatedValue.ToString()));
                        }

                        // Get the store index for updating the workflowId.
                        int storeIndex = document.stores.FindIndex(s => s.storeId == storeId);

                        if (workflowTemplate == Deployment.WorkflowTemplates.Initiation.ToString()
                            || workflowTemplate == Deployment.WorkflowTemplates.DependencyCheck.ToString())
                        {
                            patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/workflowId", null));
                        }
                        else
                        {
                            patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/workflowId", id));
                        }

                        ItemResponse<Deployment> updated = await _targetContainer.PatchItemAsync<Deployment>(document.id, new PartitionKey(market), patchOperations);

                        return updated.Resource;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing : {updateDeploymentEvent}");
                throw ex;
            }
        }
    }
}
