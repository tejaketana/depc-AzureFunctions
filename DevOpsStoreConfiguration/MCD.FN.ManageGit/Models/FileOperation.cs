using System.ComponentModel.DataAnnotations;

namespace MCD.FN.ManageGit.Models
{
    public enum FileOperation
    {
        /// <summary>
        /// Represents a Invalid fileOperation type.
        /// </summary>
        [Display(Name = "NONE")]
        NONE = 0,

        /// <summary>
        /// Represents a Add fileOperation.
        /// </summary>
        [Display(Name = "ADD")]
        ADD = 1,

        /// <summary>
        /// Represent a Deletion fileOperation.
        /// </summary>
        [Display(Name = "DEL")]
        DEL = 2
    }
}
