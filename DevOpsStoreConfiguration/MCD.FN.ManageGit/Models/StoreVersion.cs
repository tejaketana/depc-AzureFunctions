using System.Collections.Generic;

namespace MCD.FN.ManageGit.Models
{
    /// <summary>
    /// Represents the file with the versions to download
    /// </summary>
    public class StoreVersion
    {
        /// <summary>
        /// This is used only to mix the bits in the Hash generation process.
        /// </summary>
        private const int ArbitraryPrimeNumber = 397;

        /// <summary>
        /// Contains the version of the NPDAT package
        /// </summary>
        public string NPDAT { get; set; }

        /// <summary>
        /// Contains the version of the NPBIN package
        /// </summary>
        public string NPBIN { get; set; }

        /// <summary>
        /// Contains the version of the NPBIN package for Kiosk
        /// </summary>
        public string CSOBIN { get; set; }

        /// <summary>
        /// Contains the version of the NPCONTAINER package
        /// </summary>
        public string NPCONTAINER { get; set; }

        /// <summary>
        /// Contains the version of the SMARTUPDATECONTAINER package
        /// </summary>
        public string SMARTUPDATECONTAINER { get; set; }

        /// <summary>
        /// Contains the current deployment id.
        /// </summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public List<StorePackage> storePackages { get; set; }

    /// <summary>
    /// Compare current object with <paramref name="obj"/>
    /// </summary>
    /// <param name="obj">The object that will be compared.</param>
    /// <returns>True if equals. False otherwise.</returns>
    public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((StoreVersion)obj);
        }

        /// <summary>
        /// Compare current object with <paramref name="other"/>
        /// </summary>
        /// <param name="other"></param>
        /// <returns>True if equals. False otherwise.</returns>
        protected bool Equals(StoreVersion other)
        {
            return string.Equals(NPDAT, other.NPDAT) && 
                   string.Equals(NPBIN, other.NPBIN) && 
                   string.Equals(NPCONTAINER, other.NPCONTAINER) && 
                   string.Equals(SMARTUPDATECONTAINER, other.SMARTUPDATECONTAINER);
        }

        /// <summary>
        /// Gets the hash based on properties
        /// </summary>
        /// <returns>Hash based on properties</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (NPDAT != null ? NPDAT.GetHashCode() : 0);
                hashCode = (hashCode * ArbitraryPrimeNumber) ^ (NPBIN != null ? NPBIN.GetHashCode() : 0);
                hashCode = (hashCode * ArbitraryPrimeNumber) ^ (NPCONTAINER != null ? NPCONTAINER.GetHashCode() : 0);
                hashCode = (hashCode * ArbitraryPrimeNumber) ^ (SMARTUPDATECONTAINER != null ? SMARTUPDATECONTAINER.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class StorePackage
    
    {
        /// <summary>
        /// This will be the version of the package that is getting applied
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Deployment Id, that will be passed from the deployment console
        /// </summary>
        public string DeploymentId { get; set; }

        /// <summary>
        /// Effective Date when the package needs to be applied
        /// </summary>
        public string EffectiveDate { get; set; }

        /// <summary>
        /// Package Type
        /// </summary>
        public string PackageType { get; set; }
        
        /// <summary>
        /// DownloadDate Time Stamp
        /// </summary>
        public string DownloadDateTime { get; set; }

        /// <summary>
        /// Target
        /// </summary>
        public string Target { get; set; }
    }

    public class StoreDocument
    {
        public string StoreId { get; set; }
        public StoreVersion StoreVersion { get; set; }
        public string Id { get; set; }
    }
}