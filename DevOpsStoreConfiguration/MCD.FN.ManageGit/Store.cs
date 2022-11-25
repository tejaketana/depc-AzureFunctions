using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MCD.FN.ManageGit
{
    public class Store
    {
        public string id { get; set; }
        public string series { get; set; }
        public string storeId { get; set; }
        public string deploymentId { get; set; }
        public string rtpType { get; set; }
        public string rtpVersion { get; set; }
        public string region { get; set; }
        public string profile { get; set; }
        public string market { get; set; }
        public string storeSubnet { get; set; }
        public string securityData { get; set; }
        public string createdBy { get; set; }
        public Component[] Components { get; set; }
        //public string _Processed { get; set; }
        public string description { get; set; }
        public string scheduledDate { get; set; }
        public string workflowTemplate { get; set; }
        public string workflowName { get; set; }
    }

    public class StoreResults
    {
        public string errorList { get; set; }
        public string RCTBranchFriendlyURL { get; internal set; }
        public string RTPBranchFriendlyURL { get; internal set; }
        public string RCTBranchRemoteUrl { get; internal set; }
        public string RTPBranchRemoteUrl { get; internal set; }
        public string RCTStateBranchFriendlyURL { get; internal set; }
        public string RCTStateBranchRemoteURL { get; internal set; }
    }

    public class Component
    {
        public string name { get; set; }
        public string version { get; set; }
        public string target { get; set; }

        public string previousVersion { get; set; }

        public string bundleMarket { get; set; }
    }

    public class AppSettingsDoc
    {
        [JsonProperty(PropertyName = "Id")]
        public string Id;
        public AppComponent[] Components;
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
    public class AppComponent
    {
        public string Name { get; set; }
        public List<Location> Locations { get; set; }
    }

    public class Location
    {
        public string Pattern { get; set; }
        public string IndividualLocation { get; set; }
    }
}
