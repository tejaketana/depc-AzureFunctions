using System.Collections.Generic;

namespace DeploymentUpdates.Models
{
    public class Deployment
    {
        public string id { get; set; }
        public string deploymentId { get; set; }
        public string market { get; set; }
        public string currentWorkflowTemplate { get; set; }
        public string isRecordCreated { get; set; }
        public string workflowsCreated { get; set; }
        public List<DeploymentStore> stores { get; set; }

        public enum WorkflowTemplates
        {
            Initiation,
            DependencyCheck,
        }

        public enum WorkflowTriggered
        {
            NotTriggered = 0,
            FutureDeploymentTriggered = 1,
            CurrentDeploymentTriggered = 2,
        }
    }

    public class DeploymentStore
    {
        public int id { get; set; }
        public string storeId { get; set; }
        public string status { get; set; }
        public string dateCompleted { get; set; }
        public string workflowId { get; set; }
        public List<Components> components { get; set; }
        public List<DependencyCheckDetails> dependencyCheckDetails { get; set; }
        public DeviceStats deviceStats { get; set; }
        public List<DeviceStatus> devicesStatus { get; set; }
    }

    public class Components
    {
        public string software { get; set; }
        public string version { get; set; }
    }

    public class DependencyCheckDetails
    {
        public string dependencyCheckDate { get; set; }
        public List<DependencyCheckResults> dependencyCheckResults { get; set; }
    }

    public class DependencyCheckResults
    {
        public string deviceName { get; set; }
        public string deviceId { get; set; }
        public string deviceResult { get; set; }
        public string deviceDetails { get; set; }
        public string rules { get; set; }
    }

    public class DeviceStats
    {
        public string collectedOn { get; set; }
        public string devices { get; set; }
    }

    public class DeviceStatus
    {
        public string deviceId { get; set; }
        public string completedByUserId { get; set; }
        public string timestamp { get; set; }
        public StatusEvent statusEvent { get; set; }
        public Version versions { get; set; }
    }

    public class StatusEvent
    {
        public string eventType { get; set; }
        public string eventStatus { get; set; }
        public Reason reason { get; set; }
    }

    public class Reason
    {
        public int code { get; set; }
        public string description { get; set; }
    }

    public class Version
    {
        public string datVersion { get; set; }
        public int datUpdateType { get; set; }
        public string binaryVersion { get; set; }
        public int binUpdateType { get; set; }
        public string smartUpdateVersion { get; set; }
        public string npContainerVersion { get; set; }
        public string kioskBinaryVersion { get; set; }
    }
}