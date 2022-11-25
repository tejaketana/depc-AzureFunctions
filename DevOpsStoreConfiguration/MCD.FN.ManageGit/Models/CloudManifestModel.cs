using System.Collections.Generic;

namespace MCD.FN.ManageGit.Models
{
    public class CloudManifestModel
    {
        /// <summary>
        /// Package version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Package type.
        /// </summary>
        public string PackageType { get; set; }

        /// <summary>
        /// Product version.
        /// </summary>
        public string ProductVersion { get; set; }

        /// <summary>
        /// Collection of <see cref="PackageContent"/> that makes up the content of the package.
        /// </summary>
        public IEnumerable<ManifestFile> Contents;        
    }
}