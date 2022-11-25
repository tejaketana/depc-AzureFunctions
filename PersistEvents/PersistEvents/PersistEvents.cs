using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PersistEvents.Models;

namespace PersistEvents
{
    public static class PersistEvents
    {
        // CosmosDB client.
        private static Lazy<CosmosClient> lazyCosmosClient = new Lazy<CosmosClient>(InitializeCosmosClient);
        public static CosmosClient client => lazyCosmosClient.Value;
        private static Container _targetContainer = client.GetDatabase(Environment.GetEnvironmentVariable("Target_DatabaseId")).GetContainer(Environment.GetEnvironmentVariable("Target_ContainerId"));

        private static CosmosClient InitializeCosmosClient()
        {
            return new CosmosClient(Environment.GetEnvironmentVariable("CosmosDB_Connection"));
        }

        // Http client.
        private static Lazy<HttpClient> lazyHttpClient = new Lazy<HttpClient>(new HttpClient());
        public static HttpClient httpClient => lazyHttpClient.Value;

        [FunctionName("PersistEvents")]
        public static async Task Run([EventHubTrigger("%EventHubName%", ConsumerGroup = "%MSConsumerGroup%", 
            Connection = "EventHubNamespace")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            // Device event property --> EventType
            string storeEventTypeKey = Environment.GetEnvironmentVariable("StoreEventTypeKey");
            // EventType possible values --> PackageDownload, PackageApplication
            string[] storeEventTypeValues = Environment.GetEnvironmentVariable("StoreEventTypeValues").Replace(" ", String.Empty).Split(",");
            // Device event property --> EventStatus
            string storeEventStatusKey = Environment.GetEnvironmentVariable("StoreEventStatusKey");
            // EventStatus possible values for "PackageDownload", "PackageApplication" --> Started, Succeeded, Failed
            string[] storeEventStatusValues = Environment.GetEnvironmentVariable("StoreEventStatusValues").Replace(" ", String.Empty).Split(",");

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

                                var result = await MonitoringWorkflowEventHandler(workflowEvent, log);

                                if (result == null)
                                    log.LogInformation("No Action");
                                else
                                    log.LogInformation($"Successfully saved: {messageBody}");
                            }
                        }
                    }

                    // Process Store events.
                    if (!(string.IsNullOrEmpty(storeEventTypeKey) && string.IsNullOrEmpty(storeEventStatusKey)))
                    {
                        for (int storeEventTypeIndex = 0; storeEventTypeIndex < storeEventTypeValues.Length; storeEventTypeIndex++)
                        {
                            for (int storeEventStatusIndex = 0; storeEventStatusIndex < storeEventStatusValues.Length; storeEventStatusIndex++)
                            {
                                if (Regex.Match(messageBody, @"\b" + storeEventTypeKey + @""":\s*""" + storeEventTypeValues[storeEventTypeIndex] + @"\b").Success && Regex.Match(messageBody, @"\b" + storeEventStatusKey + @""":\s*""" + storeEventStatusValues[storeEventStatusIndex] + @"\b").Success)
                                {
                                    log.LogInformation($"C# Event Hub trigger function is processing Store event: {messageBody}");

                                    var storeEvent = JsonConvert.DeserializeObject<DeviceEvent>(messageBody);

                                    Deployment result = null;

                                    // Call the save method for Cancel events.
                                    if (storeEvent.statusEvent.eventType == StoreEventTypes.PackageDownload.ToString()
                                        && storeEvent.statusEvent.eventStatus == StoreEventStatuses.Cancelled.ToString())
                                        result = await SaveCancelEvent(storeEvent, log);
                                    else
                                        // Call the save method for all other store events.
                                        result = await SaveStoreEvent(storeEvent, log);

                                    if (result == null)
                                        log.LogInformation($"No Action : {storeEvent.deploymentId}, {storeEvent.storeId}, {storeEvent.deviceId}");
                                    else
                                        log.LogInformation($"Successfully saved: {storeEvent.deploymentId}, {storeEvent.storeId}, {storeEvent.deviceId}");
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

        #region Workflow Events
        /// <summary>
        /// Update the workflow event status in the Deployment document.
        /// </summary>
        /// <param name="workflowEvent"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<Deployment> SaveWorkflowEvent(WorkflowEvent workflowEvent, ILogger log)
        {
            try
            {
                //Extract values from the messageArray
                string[] messageArray = workflowEvent.message.Split(",");

                string market = "",
                deploymentId = "",
                storeId = "",
                workflowTemplate = "";

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
                    }
                }

                string sqlQuery = string.Empty;
                // Migration service events do not contain the market value.
                if (market == string.Empty)
                    market = storeId.Substring(0, 2);

                sqlQuery = "SELECT * FROM c WHERE c.market = '" + market + "' AND c.deploymentId = '" + deploymentId + "'";

                var iterator = _targetContainer.GetItemQueryIterator<Deployment>(new QueryDefinition(sqlQuery));

                // Should have max 1 document...
                while (iterator.HasMoreResults)
                {
                    foreach (var document in await iterator.ReadNextAsync())
                    {
                        // Get the index for updating the device event.
                        int storeIndex = document.stores.FindIndex(s => s.storeId == storeId);

                        document.stores[storeIndex].status = DetailedStatusExtensions.ToFriendlyString(workflowEvent.status);

                        List<PatchOperation> patchOperations = new List<PatchOperation>();
                        if ((workflowEvent.status == DeploymentDetailStatus.DependencycheckSuccess.ToString() 
                            || workflowEvent.status == DeploymentDetailStatus.DependencycheckFailed.ToString()) 
                            && workflowEvent.dependencyCheckResultDetails != null)
                        {
                            // Persist the Dependency Check results.
                            if (document.stores[storeIndex].dependencyCheckDetails == null)
                            {
                                document.stores[storeIndex].InitializeDependencyCheckDetails(workflowEvent.dependencyCheckResultDetails);

                                patchOperations.Add(PatchOperation.Add<List<DependencyCheckDetails>>("/stores/" + storeIndex + "/dependencyCheckDetails", document.stores[storeIndex].dependencyCheckDetails));
                            }
                            else
                                patchOperations.Add(PatchOperation.Add<DependencyCheckDetails>("/stores/" + storeIndex + "/dependencyCheckDetails/-", document.stores[storeIndex].UpdateDependencyCheckDetails(workflowEvent.dependencyCheckResultDetails)));
                        }

                        // If Repository Preparation is success, init "Device Stats".
                        if (workflowEvent.status == DeploymentDetailStatus.DoDeploySuccess.ToString())
                        {
                            // Check if the Device Stats is initialized.
                            if (document.stores[storeIndex].deviceStats == null)
                            {
                                document.stores[storeIndex].deviceStats = await GetDeviceStats(document, storeId, storeIndex);

                                patchOperations.Add(PatchOperation.Add<DeviceStats>("/stores/" + storeIndex + "/deviceStats", document.stores[storeIndex].deviceStats));
                            }
                        }

                        // Deployment status - Scheduled --> In Progress.
                        if (document.status == DeploymentStatus.Scheduled.ToString() && (workflowTemplate != Deployment.WorkflowTemplates.Initiation.ToString()
                        && workflowTemplate != Deployment.WorkflowTemplates.DependencyCheck.ToString()))
                        {
                            patchOperations.Add(PatchOperation.Replace<string>("/status", document.ToFriendlyString(DeploymentStatus.InProgress.ToString())));
                        }
                        else if (document.stores.All(s => s.status == DeploymentStatus.Canceled.ToString()))
                        {
                            // Deployment status - Canceled, if all stores status = Canceled.
                            patchOperations.Add(PatchOperation.Replace<string>("/status", DeploymentStatus.Canceled.ToString()));
                        }

                        patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));
                        patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", document.stores[storeIndex].status));

                        ItemResponse<Deployment> updated = await _targetContainer.PatchItemAsync<Deployment>(document.id, new PartitionKey(market), patchOperations);

                        return updated.Resource;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing : {workflowEvent}");
                throw ex;
            }
        }

        /// <summary>
        /// Updates to the Monitoring container - for now only currentWorkflowTemplate, isRecordCreated, workflowId.
        /// </summary>
        /// <param name="updateMonitoringEvent"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<Deployment> UpdateMonitoringEvent(WorkflowEvent updateMonitoringEvent, ILogger log)
        {
            try
            {
                //Extract values from the messageArray
                string[] messageArray = updateMonitoringEvent.message.Split(",");

                string market = "",
                deploymentId = "",
                storeId = "",
                workflowTemplate = "",
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

                        // Update only the first time this document is being updated for a Store.
                        if (Int32.Parse(document.workflowsCreated) == (int)Deployment.WorkflowTriggered.NotTriggered)
                        {
                            // WorkflowTemplate will be the same for all stores in the Deployment.
                            patchOperations.Add(PatchOperation.Replace<string>("/currentWorkflowTemplate", workflowTemplate));

                            // Flag this deployment that workflow creation has been triggered.
                            // Flag this deployment that workflow creation has been triggered.
                            patchOperations.Add(PatchOperation.Replace<string>("/workflowsCreated",
                                (workflowTemplate == Deployment.WorkflowTemplates.Initiation.ToString()
                                || workflowTemplate == Deployment.WorkflowTemplates.DependencyCheck.ToString())
                                    ? ((int)Deployment.WorkflowTriggered.FutureDeploymentTriggered).ToString()
                                    : ((int)Deployment.WorkflowTriggered.CurrentDeploymentTriggered).ToString()));
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
                log.LogError($"Error processing : {updateMonitoringEvent}");
                throw ex;
            }
        }

        /// <summary>
        /// This method handles the "RecalculateStatus" status.
        /// </summary>
        /// <param name="updateMonitoringEvent"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<Deployment> MonitoringStatusEvent(WorkflowEvent updateMonitoringEvent, ILogger log)
        {
            try
            {
                //Extract values from the messageArray
                string[] messageArray = updateMonitoringEvent.message.Split(",");

                string market = "",
                deploymentId = "",
                storeId = "",
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
                        case "id":
                            id = messageArray[i].Split(":")[1].Trim();
                            break;
                    }
                }

                string sqlQuery = "SELECT * FROM c WHERE c.market = '" + market + "' AND c.deploymentId = '" + deploymentId + "'";

                var iterator = _targetContainer.GetItemQueryIterator<Deployment>(new QueryDefinition(sqlQuery));

                // Should have max 1 document...
                while (iterator.HasMoreResults)
                {
                    foreach (var document in await iterator.ReadNextAsync())
                    {
                        // Get the store index for updating the workflowId.
                        int storeIndex = document.stores.FindIndex(s => s.storeId == storeId);

                        // One last check to make sure all device events are "Succeeded" but store status is incorrect.
                        if (document.stores[storeIndex].IsAllDeviceStatusSuccess() && document.stores[storeIndex].status != DeploymentDetailStatus.Completed.ToString())
                        {
                            var patchOperations = new List<PatchOperation>();

                            document.stores[storeIndex].status = DeploymentDetailStatus.Completed.ToString();

                            // Update the store status.
                            patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", DeploymentDetailStatus.Completed.ToString()));
                            // Date completed for the store.
                            patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/dateCompleted", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffK", CultureInfo.InvariantCulture)));
                            // Deployment detailed status.
                            patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));

                            if (document.IsDeploymentCompleted())
                            {
                                patchOperations.Add(PatchOperation.Replace<string>("/status", DeploymentStatus.Completed.ToString()));
                            }

                            ItemResponse<Deployment> updated = await _targetContainer.PatchItemAsync<Deployment>(document.id, new PartitionKey(market), patchOperations);

                            return updated.Resource;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing : {updateMonitoringEvent}");
                throw ex;
            }
        }

        /// <summary>
        /// This method is simply to redirect to different methods based on the "status" value in the Workflow event.
        /// </summary>
        /// <param name="monitoringEvent"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<Deployment> MonitoringWorkflowEventHandler(WorkflowEvent monitoringEvent, ILogger log)
        {
            switch (monitoringEvent.status)
            {
                case "UpdateWorkflowId":
                    return await UpdateMonitoringEvent(monitoringEvent, log);
                case "RecalculateStatus":
                    return await MonitoringStatusEvent(monitoringEvent, log);
                default:
                    return await SaveWorkflowEvent(monitoringEvent, log);
            }
        }
        #endregion

        #region Store Events
        /// <summary>
        /// Update the SmartUpdate events in the Deployment document.
        /// </summary>
        /// <param name="deviceEvent"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<Deployment> SaveStoreEvent(DeviceEvent deviceEvent, ILogger log)
        {
            try
            {
                string sqlQuery = "SELECT * FROM c WHERE c.market = '" + deviceEvent.market + "' AND c.deploymentId = '" + deviceEvent.deploymentId + "'";
                var iterator = _targetContainer.GetItemQueryIterator<Deployment>(new QueryDefinition(sqlQuery));

                // Should have max 1 document...
                while (iterator.HasMoreResults)
                {
                    foreach (var document in await iterator.ReadNextAsync())
                    {
                        // Get the index for updating the device event.
                        int storeIndex = document.stores.FindIndex(s => s.storeId == deviceEvent.storeId);

                        PatchItemRequestOptions patchItemRequestOptions = new PatchItemRequestOptions
                        {
                            FilterPredicate = "from c join s in c.stores where s.storeId = '" + deviceEvent.storeId + "'"
                        };

                        List<PatchOperation> patchOperations = null;
                        if (deviceEvent.statusEvent.eventType == StoreEventTypes.PackageDownload.ToString())
                        {
                            patchOperations = new List<PatchOperation>();

                            document.stores[storeIndex].status = DetailedStatusExtensions.ToFriendlyString(DownloadPackageStatusMapping.MapTo(deviceEvent.statusEvent.eventStatus).ToString());

                            // If Binary download has succeeded, check "Device Stats".
                            if (deviceEvent.statusEvent.eventStatus == DownloadPackageStatus.Succeeded.ToString())
                            {
                                // 2nd attempt to initialize.
                                if (document.stores[storeIndex].deviceStats == null)
                                {
                                    // Use DCS/ RAM to get the list of Devices in the store.
                                    patchOperations.Add(PatchOperation.Add<DeviceStats>("/stores/" + storeIndex + "/deviceStats", await GetDeviceStats(document, deviceEvent.storeId, storeIndex)));
                                }
                            }

                            patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));
                            patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", document.stores[storeIndex].status));
                        }
                        else if (deviceEvent.statusEvent.eventType == StoreEventTypes.PackageApplication.ToString())
                        {
                            // Incoming device event.
                            var device = (DeviceStatus)deviceEvent;

                            if (IsMigrationEvent(deviceEvent.statusEvent.eventType, deviceEvent.statusEvent.eventStatus))
                                patchOperations = await Migration(document, storeIndex, device);
                            else if (IsRollbackEvent(deviceEvent.statusEvent.eventType, deviceEvent.statusEvent.eventStatus))
                                patchOperations = await Rollback(document, storeIndex, device);
                        }

                        if (patchOperations.Count == 0)
                            return null;

                        ItemResponse<Deployment> updated = await _targetContainer.PatchItemAsync<Deployment>(document.id, new PartitionKey(document.market), patchOperations, patchItemRequestOptions);

                        return updated.Resource;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error : {deviceEvent.deploymentId}, {deviceEvent.storeId}, {deviceEvent.deviceId}");
                throw ex;
            }
        }

        /// <summary>
        /// Identify a Migration event.
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        private static bool IsMigrationEvent(string eventType, string status)
        {
            return eventType == StoreEventTypes.PackageApplication.ToString()
                && (status == StoreEventStatuses.Started.ToString()
                    || status == StoreEventStatuses.Succeeded.ToString()
                    || status == StoreEventStatuses.Failed.ToString()
                    || status == StoreEventStatuses.Complete.ToString()) ? true : false;
        }

        /// <summary>
        /// Identify a Rollback event.
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        private static bool IsRollbackEvent(string eventType, string status)
        {
            return eventType == StoreEventTypes.PackageApplication.ToString()
                && (status == StoreEventStatuses.RollbackStarted.ToString()
                    || status == StoreEventStatuses.RollbackSucceeded.ToString()
                    || status == StoreEventStatuses.RollbackFailed.ToString()) ? true : false;
        }

        /// <summary>
        /// Patch operations for a Migration.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="storeIndex"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static async Task<List<PatchOperation>> Migration(Deployment document, int storeIndex, DeviceStatus device)
        {
            var patchOperations = new List<PatchOperation>();

            if (document.stores[storeIndex].IsStoreUpdatePending())
            {
                if (document.stores[storeIndex].devicesStatus == null)
                    document.stores[storeIndex].devicesStatus = new List<DeviceStatus>();

                // Add the Device event to the Deployment document.
                document.stores[storeIndex].devicesStatus.Add(device);

                if (document.stores[storeIndex].IsFirstDeviceEvent())
                {
                    document.stores[storeIndex].status = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DeploymentInProgress.ToString());

                    // Check if the Device Stats is initialized. If not, then 3rd attempt to initialize.
                    if (document.stores[storeIndex].deviceStats == null)
                    {
                        patchOperations.Add(PatchOperation.Add<DeviceStats>("/stores/" + storeIndex + "/deviceStats", await GetDeviceStats(document, document.stores[storeIndex].storeId, storeIndex)));
                    }

                    patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));
                    patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", document.stores[storeIndex].status));

                    var devicesList = new List<DeviceStatus>();
                    devicesList.Add(device);

                    // Add the incoming device event.
                    patchOperations.Add(PatchOperation.Add<List<DeviceStatus>>("/stores/" + storeIndex + "/devicesStatus", devicesList));
                }
                else
                {
                    // Add the incoming device event.
                    patchOperations.Add(PatchOperation.Add<DeviceStatus>("/stores/" + storeIndex + "/devicesStatus/-", device));
                }

                // If this is the last device event, store status = Completed/Failed.
                if (document.stores[storeIndex].IsLastDeviceEvent())
                {
                    // If all devices are Succeeded, store status = Completed.
                    document.stores[storeIndex].status = document.stores[storeIndex].IsAllDeviceStatusSuccess()
                        ? DeploymentDetailStatus.Completed.ToString()
                        : DeploymentDetailStatus.Failed.ToString();

                    // Update Store status.
                    patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));
                    patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", document.stores[storeIndex].status));

                    // Date completed for the store.
                    patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/dateCompleted", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffK", CultureInfo.InvariantCulture)));
                }

                // If all stores are Completed, deployment status = Completed.
                if (document.IsDeploymentCompleted())
                {
                    patchOperations.Add(PatchOperation.Replace<string>("/status", DeploymentStatus.Completed.ToString()));
                }
            }
            else
            {
                // Add the incoming device event - this is after the Store status = Completed.
                patchOperations.Add(PatchOperation.Add<DeviceStatus>("/stores/" + storeIndex + "/devicesStatus/-", device));
            }

            return patchOperations;
        }

        /// <summary>
        /// Patch operations for a Rollback.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="storeIndex"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static async Task<List<PatchOperation>> Rollback(Deployment document, int storeIndex, DeviceStatus device)
        {
            var patchOperations = new List<PatchOperation>();

            // Add the Device event to the Deployment document.
            document.stores[storeIndex].devicesStatus.Add(device);

            if (document.stores[storeIndex].IsLastDeviceRollbackEvent())
            {
                // If all devices are Succeeded, store status = Completed.
                document.stores[storeIndex].status = document.stores[storeIndex].IsAllDeviceRollbackStatusSuccess()
                    ? DeploymentDetailStatus.Completed.ToString()
                    : DeploymentDetailStatus.Failed.ToString();

                // Update Store status.
                patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));
                patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", document.stores[storeIndex].status));
            }
            // Add the incoming device event.
            patchOperations.Add(PatchOperation.Add<DeviceStatus>("/stores/" + storeIndex + "/devicesStatus/-", device));

            return patchOperations;
        }

        /// <summary>
        /// Update the SmartUpdate Cancel events in the Deployment document.
        /// </summary>
        /// <param name="deviceEvent"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<Deployment> SaveCancelEvent(DeviceEvent deviceEvent, ILogger log)
        {
            try
            {
                string sqlQuery = "SELECT * FROM c WHERE c.market = '" + deviceEvent.market + "' AND c.deploymentId = '" + deviceEvent.deploymentId + "'";
                var iterator = _targetContainer.GetItemQueryIterator<Deployment>(new QueryDefinition(sqlQuery));

                // Should have max 1 document...
                while (iterator.HasMoreResults)
                {
                    foreach (var document in await iterator.ReadNextAsync())
                    {
                        // Get the index for updating the device event.
                        int storeIndex = document.stores.FindIndex(s => s.storeId == deviceEvent.storeId);

                        PatchItemRequestOptions patchItemRequestOptions = new PatchItemRequestOptions
                        {
                            FilterPredicate = "from c join s in c.stores where s.storeId = '" + deviceEvent.storeId + "'"
                        };

                        List<PatchOperation> patchOperations = new List<PatchOperation>();
                        // Incoming device event.
                        var device = (DeviceStatus)deviceEvent;

                        if (document.stores[storeIndex].devicesStatus == null)
                            document.stores[storeIndex].devicesStatus = new List<DeviceStatus>();

                        // Add the Device event to the Deployment document.
                        document.stores[storeIndex].devicesStatus.Add(device);

                        if (document.stores[storeIndex].IsFirstDeviceEvent())
                        {
                            // Just checking if the Device Stats is initialized.
                            if (document.stores[storeIndex].deviceStats == null)
                            {
                                document.stores[storeIndex].deviceStats = await GetDeviceStats(document, deviceEvent.storeId, storeIndex);

                                patchOperations.Add(PatchOperation.Add<DeviceStats>("/stores/" + storeIndex + "/deviceStats", document.stores[storeIndex].deviceStats));
                            }

                            var devicesList = new List<DeviceStatus>();
                            devicesList.Add(device);

                            // Add the incoming device event.
                            patchOperations.Add(PatchOperation.Add<List<DeviceStatus>>("/stores/" + storeIndex + "/devicesStatus", devicesList));
                        }
                        else
                        {
                            // Add the incoming device event.
                            patchOperations.Add(PatchOperation.Add<DeviceStatus>("/stores/" + storeIndex + "/devicesStatus/-", device));
                        }

                        // If this is the last device event, check if store status = Canceled.
                        if (document.stores[storeIndex].IsLastDeviceEvent() && document.stores[storeIndex].status != DeploymentDetailStatus.Canceled.ToString())
                        {
                            document.stores[storeIndex].status = DeploymentDetailStatus.Canceled.ToString();

                            patchOperations.Add(PatchOperation.Replace<string>("/detailedStatus", document.GetStatus()));
                            patchOperations.Add(PatchOperation.Replace<string>("/stores/" + storeIndex + "/status", document.stores[storeIndex].status));

                            // Update the Deployment status, if required.
                            if (document.status != DeploymentStatus.Canceled.ToString() && document.stores.All(s => s.status == DeploymentDetailStatus.Canceled.ToString()))
                            {
                                patchOperations.Add(PatchOperation.Replace<string>("/status", DeploymentStatus.Canceled.ToString()));
                            }
                        }

                        if (patchOperations.Count == 0)
                            return null;

                        ItemResponse<Deployment> updated = await _targetContainer.PatchItemAsync<Deployment>(document.id, new PartitionKey(document.market), patchOperations, patchItemRequestOptions);

                        return updated.Resource;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in SaveCancelEvent: {deviceEvent.deploymentId}, {deviceEvent.storeId}, {deviceEvent.deviceId}");
                throw ex;
            }
        }

        /// <summary>
        /// Query DCS for deviceIds from RAM for a store.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="storeId"></param>
        /// <returns></returns>
        private static async Task<List<Device>> GetStoreDevicesFromRAM(string market, string storeId)
        {
            try
            {
                if (httpClient.BaseAddress == null)
                {
                    httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("DCS_Endpoint"));
                }
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestUri = Environment.GetEnvironmentVariable("DCS_StoreDevices_Uri").Replace("{market}", market).Replace("{storeId}", storeId);
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    string devicesData = await response.Content.ReadAsStringAsync();
                    var storeDevices = JsonConvert.DeserializeObject<RAMDeviceList>(devicesData);

                    return storeDevices.devices;
                }
                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Build "deviceStats" for the Deployment document.
        /// </summary>
        /// <param name="deployment"></param>
        /// <param name="storeId"></param>
        /// <param name="storeIndex"></param>
        /// <returns></returns>
        private static async Task<DeviceStats> GetDeviceStats(Deployment deployment, string storeId, int storeIndex)
        {
            var devicesFromRAM = deployment.stores[storeIndex].ApplyDeviceFilterRules(await GetStoreDevicesFromRAM(deployment.market, storeId));

            if (devicesFromRAM == null)
            {
                return null;
            }

            return new DeviceStats
            {
                collectedOn = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffK", CultureInfo.InvariantCulture), // Datetime when the deviceStats was created.
                devices = string.Join(",", devicesFromRAM.Select(d => d.deviceName))
            };
        }
        #endregion
    }
}
