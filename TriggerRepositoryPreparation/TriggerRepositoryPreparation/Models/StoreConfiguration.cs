using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace TriggerRepositoryPreparation.Models
{
    /// <summary>
    /// This entity will be persisted in the "StoreConfigurations".
    /// Document will contain the Store configuration in the "storeversion" property.
    /// </summary>
    public class StoreConfiguration
    {
        public string storeId { get; set; }
        public JObject storeversion { get; set; }
    }
}