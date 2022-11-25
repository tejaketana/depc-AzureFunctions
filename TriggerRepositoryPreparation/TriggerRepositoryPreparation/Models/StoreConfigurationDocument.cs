using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using TriggerRepositoryPreparation.Core;
using TriggerRepositoryPreparation.Models.Input;

namespace TriggerRepositoryPreparation.Models.Output
{
    /// <summary>
    /// This entity will be persisted in the "StoreConfigurationsLog" container.
    /// Document will contain data from the DoDeployPending event and the mapped output for storeversion.
    /// </summary>
    public class StoreConfigurationDocument
    {
        public string id { get; set; }
        public List<Component> components { get; set; }
        public string market { get; set; }
        public string storeId { get; set; }
        public string deploymentId { get; set; }
        public string effectiveDateTime { get; set; }
        public string applyDate { get; set; }
        public int timeZoneOffsetInMins { get; set; }
        public string createdBy { get; set; }
        public string createdByUserId { get; set; }
        public string rtpType { get; set; }
        public string rtpVersion { get; set; }
        public string workflowTemplate { get; set; }
        public string workflowName { get; set; }
        public string isCanceled { get; set; } = "false";
        public string rollback { get; set; } = "false";
        public List<string> errors { get; set; }
        public JArray storeConfigs { get; set; }

        public bool MapInputToDataModel(DoDeployPendingEvent inputEvent, ILogger log)
        {
            try
            {
                string[] messageArray = inputEvent.message.Split(",");

                // "id" is the WorkFlow "id".
                id = messageArray.FirstOrDefault(v => v.Contains("id")).Split(":")[1].Trim();
                market = messageArray.FirstOrDefault(v => v.Contains("market")).Split(":")[1].Trim();
                storeId = messageArray.FirstOrDefault(v => v.Contains("storeId")).Split(":")[1].Trim();
                deploymentId = messageArray.FirstOrDefault(v => v.Contains("deploymentId")).Split(":")[1].Trim();
                rtpType = messageArray.FirstOrDefault(v => v.Contains("deploymentModel")).Split(":")[1].Trim();

                if (inputEvent.status == GlobalConstants.FilterValues.DoDeployPending.ToString())
                {
                    components = new List<Component>();
                    foreach (var component in inputEvent.components)
                    {
                        components.Add(new Component
                        {
                            name = component.software,
                            version = component.version
                        });
                    }

                    // User's time zone offset in minuites.
                    timeZoneOffsetInMins = Convert.ToInt32(messageArray.FirstOrDefault(v => v.Contains("timeZoneOffsetInMins")).Split(":")[1].Trim());
                    effectiveDateTime = messageArray.FirstOrDefault(v => v.Contains("effectiveDateTime")).Split(":", 2)[1].Trim();
                    applyDate = messageArray.FirstOrDefault(v => v.Contains("applyDate")).Split(":", 2)[1].Trim();
                    // Reformat the Apply date & Download date for SmartUpdate.
                    // 2021-03-31T14:03:28Z
                    effectiveDateTime = effectiveDateTime.Replace("-", "").Replace("T", "").Replace(":", "").Substring(0, 8).PadRight(14, '0');
                    // 2021-06-19T05:59:23.0000000Z
                    // Use the Time zone offset in minuites to convert the Apply datetime to user's local date.
                    applyDate = DateTime.Parse(applyDate, null, System.Globalization.DateTimeStyles.RoundtripKind).AddMinutes(timeZoneOffsetInMins).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffZ");
                    applyDate = applyDate.Replace("-", "").Replace("T", "").Replace(":", "").Substring(0, 8).PadRight(14, '0');

                    createdBy = messageArray.FirstOrDefault(v => v.Contains("createdBy")).Split(":")[1].Trim();
                    createdByUserId = messageArray.FirstOrDefault(v => v.Contains("createdByUserId")).Split(":")[1].Trim();
                    rtpVersion = messageArray.FirstOrDefault(v => v.Contains("rtpVersion")).Split(":")[1].Trim();
                    workflowTemplate = messageArray.FirstOrDefault(v => v.Contains("workflowTemplate")).Split(":")[1].Trim();
                    workflowName = messageArray.FirstOrDefault(v => v.Contains("workflowName")).Split(":")[1].Trim();
                }

                // Missing Component mappings will be added to the "errors" list;
                errors = new List<string>();

                return true;
            }
            catch (Exception ex)
            {
                log.LogError($"TriggerRepositoryPreparation: StoreConfigurationDocument.TransformForInputEntity() - error: {ex}");
                throw ex;
            }
        }

        public string UpdatePropertyPath(string status)
        {
            switch (status)
            {
                case "CancelDeploymentPending":
                    return "/isCanceled";
                case "RollbackRequestPending":
                    return "/rollback";
                default:
                    return string.Empty;
            }
        }

        public string UpdatePropertyValue(string status)
        {
            switch (status)
            {
                case "CancelDeploymentPending":
                    return "true";
                case "RollbackRequestPending":
                    return "true";
                default:
                    return string.Empty;
            }
        }
    }

    public class Component
    {
        public string name { get; set; }
        public string version { get; set; }
    }
}
