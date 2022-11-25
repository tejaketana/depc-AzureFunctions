using System;

namespace MCD.FN.ManageGit.Models
{
    /// <summary>
    /// Defines the contents of a package.
    /// </summary>
    [Serializable]
    public class ManifestFile
    {
        public string Path { get; set; }

        public string Version { get; set; }

        public string Operation { get; set; }

        public string Target { get; set; }
    }
}
