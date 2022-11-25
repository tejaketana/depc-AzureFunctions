using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace TriggerRepositoryPreparation.Models
{
    public class ConfigurationTemplate
    {
        public int templateId { get; set; }
        public string rtpType { get; set; }
        public string location { get; set; }
        public JObject initializeNewStore { get; set; }
        public Section backwardCompatibilty { get; set; }
        public List<Component> components { get; set; }
    }

    public class Section
    {
        public JObject components { get; set; }
        public JObject mapping { get; set; }
    }

    public class Component
    {
        public string component { get; set; }
        public string configFile { get; set; }
        public string location { get; set; }
        public JObject mapping { get; set; }
    }
}
