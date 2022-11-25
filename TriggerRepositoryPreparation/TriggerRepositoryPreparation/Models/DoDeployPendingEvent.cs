using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TriggerRepositoryPreparation.Core;

namespace TriggerRepositoryPreparation.Models.Input
{
    /// <summary>
    /// Event/Message from the EventHub read by this Azure Funtion.
    /// </summary>
    public class DoDeployPendingEvent
    {
        public List<Component> components { get; set; }
        public string message { get; set; }
        public string messageOut { get; set; }
        public string name { get; set; }
        public string structuredForType { get; set; }
        public string trigger { get; set; }
        public string type { get; set; }
        public string verbosity { get; set; }
        public string workflowSystem { get; set; }
        public string status { get; set; }

        public async Task<bool> IsValid(ILogger log)
        {

            bool valid = true;
            string[] messageArray = message.Split(",");

            if (status == GlobalConstants.FilterValues.DoDeployPending.ToString())
            {
                if (messageArray.FirstOrDefault(v => v.Contains("id")) == null || messageArray.FirstOrDefault(v => v.Contains("market")) == null
                    || messageArray.FirstOrDefault(v => v.Contains("deploymentId")) == null || messageArray.FirstOrDefault(v => v.Contains("storeId")) == null
                    || messageArray.FirstOrDefault(v => v.Contains("applyDate")) == null || messageArray.FirstOrDefault(v => v.Contains("effectiveDate")) == null
                    || messageArray.FirstOrDefault(v => v.Contains("timeZoneOffsetInMins")) == null
                    || messageArray.FirstOrDefault(v => v.Contains("deploymentModel")) == null
                    || components.Select(x => x.version) == null)
                {
                    messageOut = "'id', 'market', 'deploymentId', 'storeId', 'applyDate', 'effectiveDate', 'timeZoneOffsetInMins', 'deploymentModel', 'components/containers' are required";
                    log.LogError("DoDeployPendingEvent validation: " + messageOut);
                    valid = false;
                }

                // Add check for rtpVersion, if deploymentModel = "SC".
                if (valid
                    && messageArray.FirstOrDefault(d => d.Contains("deploymentModel")).Split(":")[1] == "SC"
                    && string.IsNullOrEmpty(messageArray.FirstOrDefault(d => d.Contains("rtpVersion")).Split(":")[1]))
                {
                    messageOut = "'rtpVersion' is required";
                    log.LogError("DoDeployPendingEvent validation: " + messageOut);
                    valid = false;
                }
            }
            else if (status == GlobalConstants.FilterValues.CancelDeploymentPending.ToString())
            {
                if (messageArray.FirstOrDefault(v => v.Contains("market")) == null
                    || messageArray.FirstOrDefault(v => v.Contains("deploymentId")) == null 
                    || messageArray.FirstOrDefault(v => v.Contains("storeId")) == null
                    || messageArray.FirstOrDefault(v => v.Contains("deploymentModel")) == null)
                {
                    messageOut = "'market', 'deploymentId', 'storeId', 'deploymentModel' are required";
                    log.LogError("CancelDeploymentPendingEvent validation: " + messageOut);
                    valid = false;
                }
            }

            if (!valid)
            {
                status = Environment.GetEnvironmentVariable("FailedResponse");
                await Helper.SendEvent(this, log);
            }
            return valid;
        }

        public bool IsActionEvent()
        {
            switch (status)
            {
                case "DoDeployPending":
                    return false;
                case "CancelDeploymentPending":
                    return true;
                case "RollbackRequestPending":
                    return true;
                default:
                    return false;
            }
        }
    }

    public class Component
    {
        public string software { get; set; }
        public string deploymentModel { get; set; }
        public string version { get; set; }
    }
}
