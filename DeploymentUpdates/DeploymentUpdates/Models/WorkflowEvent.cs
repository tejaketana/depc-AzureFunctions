using System;
using System.Collections.Generic;
using System.Text;

namespace DeploymentUpdates.Models
{
    /// <summary>
    /// Event sent by the Workflow service.
    /// </summary>
    public class WorkflowEvent
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
        public WorkflowEventDependencyCheckDetails dependencyCheckResultDetails { get; set; }
    }

    public class Component
    {
        public string software { get; set; }
        public string version { get; set; }
    }

    public class WorkflowEventDependencyCheckDetails
    {
        public string dependencyCheckDate { get; set; }
        public List<WorkflowEventDependencyCheckResults> dependencyCheckResults { get; set; }
    }

    public class WorkflowEventDependencyCheckResults
    {
        public string deviceName { get; set; }
        public string deviceResult { get; set; }
        public string deviceDetails { get; set; }
    }
}
