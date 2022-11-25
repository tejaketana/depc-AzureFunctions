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
using WorkflowUpdates.Models;
using System.Globalization;

namespace WorkflowUpdates
{
    public static class WorkflowUpdates
    {
        // CosmosDB client.
        private static Lazy<CosmosClient> lazyCosmosClient = new Lazy<CosmosClient>(InitializeCosmosClient);
        public static CosmosClient client => lazyCosmosClient.Value;
        private static Container _targetContainer = client.GetDatabase(Environment.GetEnvironmentVariable("Target_DatabaseId_Deployment")).GetContainer(Environment.GetEnvironmentVariable("Target_ContainerId_Deployment"));

        private static CosmosClient InitializeCosmosClient()
        {
            return new CosmosClient(Environment.GetEnvironmentVariable("CosmosDB_Connection"));
        }

        [FunctionName("WorkflowUpdates")]
        public static async Task Run([EventHubTrigger("%EventHubName%", ConsumerGroup = "%MSConsumerGroup%",
            Connection = "EventHubNamespace")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            // Workflow event property --> status.
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

                                // Call the update method.
                                var result = await UpdateWorkflowStepStatus(workflowEvent, log);

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
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        private static async Task<Workflow> UpdateWorkflowStepStatus(WorkflowEvent updateWorkflowEvent, ILogger log)
        {
            try
            {
                //Extract values from the messageArray
                string[] messageArray = updateWorkflowEvent.message.Split(",");

                string market = "",
                deploymentId = "",
                storeId = "",
                workflowTemplate = "",
                workflowName = "",
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
                        case "workflowName":
                            workflowName = messageArray[i].Split(":")[1].Trim();
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

                sqlQuery = "SELECT * FROM c WHERE c.id = '" + id + "'";

                var iterator = _targetContainer.GetItemQueryIterator<Workflow>(new QueryDefinition(sqlQuery));

                // Should have max 1 document...
                while (iterator.HasMoreResults)
                {
                    var patchOperations = new List<PatchOperation>();

                    foreach (var document in await iterator.ReadNextAsync())
                    {
                        // Get the index of the workflow step to update the "workflowStartDate", "state".
                        int workflowIndex = document.workflowSystems.FindIndex(w => w.name == workflowName);

                        patchOperations.Add(PatchOperation.Replace<string>("/unstructuredData/status", Workflow.EventStatus.EventSent.ToString()));
                        patchOperations.Add(PatchOperation.Replace<string>("/workflowSystems/" + workflowIndex + "/workflowStartDate", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffK", CultureInfo.InvariantCulture)));
                        patchOperations.Add(PatchOperation.Replace<string>("/workflowSystems/" + workflowIndex + "/state", WorkflowSystem.WorkflowSystemStatus.Started.ToString()));

                        ItemResponse<Workflow> updated = await _targetContainer.PatchItemAsync<Workflow>(document.id, new PartitionKey(workflowTemplate), patchOperations);

                        return updated.Resource;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing : {updateWorkflowEvent}");
                throw ex;
            }
        }
    }
}
