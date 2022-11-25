using System;
using System.Collections.Generic;
using System.Text;

namespace TriggerDoDeploy.Models
{
    /// <summary>
    /// Payload for DoDeploy Logic App.
    /// </summary>
    public class DoDeployRequest
    {
        public string id { get; set; }
        public string deploymentId { get; set; }
        public List<Component> Components { get; set; }
        public string schedule { get; set; }
        public string market { get; set; }
        public string marketstoreid { get; set; }
        public string storeId { get; set; }
        public string series { get; set; }
        public string createdBy { get; set; }
        public string description { get; set; }
        public string profile { get; set; }
        public string region { get; set; }
        public string rtpType { get; set; }
        public string rtpVersion { get; set; }
        public string scheduledDate { get; set; }
        public string securityData { get; set; }
        public string storeSubnet { get; set; }
        public string workflowTemplate { get; set; }
        public string workflowName { get; set; }
    }
    public class Component
    {
        public string name { get; set; }
        public string version { get; set; }
    }
}
