using System.Collections.Generic;

namespace PersistEvents.Models
{
    /// <summary>
    /// Data model for devices from DCS/RAM.
    /// </summary>
    public class RAMDeviceList
    {
        public string storeId { get; set; }
        public List<Device> devices { get; set; }
    }

    public class Device
    {
        public string deviceType { get; set; }
        public string deviceName { get; set; }
    }
}
