using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrepareGithubRepository.Models
{
    public class StoreDeploymentDocument
    {
        public string id { get; set; }
        public List<Component> components { get; set; }
        public string market { get; set; }
        public string storeId { get; set; }
        public string deploymentId { get; set; }
        public string effectiveDateTime { get; set; }
        public string applyDate { get; set; }
        public string createdBy { get; set; }
        public string createdByUserId { get; set; }
        public string rtpType { get; set; }
        public string rtpVersion { get; set; }
        public string workflowTemplate { get; set; }
        public string workflowName { get; set; }
        public string isCanceled { get; set; }
        public string rollback { get; set; }
        public List<string> errors { get; set; }
        public List<string> cancelErrors { get; set; }
        public List<JObject> storeConfigs { get; set; }

        public bool IsUpdated()
        {
            return isCanceled == "true" || rollback == "true" ? true : false;
        }
    }

    public class Component
    {
        public string name { get; set; }
        public string version { get; set; }
    }

    public class StoreVersionJsonConfig
    {
        public string location { get; set; }
        public string configFile { get; set; }
        public JObject storeVersion { get; set; }
    }

    public class LocalContainerVersionJsonConfig
    {
        public string location { get; set; }
        public string configFile { get; set; }
        public JObject localContainerVersion { get; set; }
    }

    public class StorePackages
    {
        public string version { get; set; }
        public string deploymentId { get; set; }
        public string effectiveDate { get; set; }
        public string downloadDateTime { get; set; }
        public string packageType { get; set; }
        public string target { get; set; }
    }
}
