using System;
using System.Collections.Generic;
using System.Text;

namespace TriggerDoDeploy.Models
{
    /// <summary>
    /// Event/Message from the EventHub read by this Azure Funtion.
    /// </summary>
    public class TriggerMessage
    {
        public List<ComponentInput> components { get; set; }
        public string message { get; set; }
        public string messageOut { get; set; }
        public string name { get; set; }
        public string structuredForType { get; set; }
        public string trigger { get; set; }
        public string type { get; set; }
        public string verbosity { get; set; }
        public string workflowSystem { get; set; }
        public string status { get; set; }
    }

    public class ComponentInput
    {
        public string software { get; set; }
        public string deploymentModel { get; set; }
        public string version { get; set; }
    }

}
