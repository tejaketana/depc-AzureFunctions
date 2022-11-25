using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MCD.FN.ManageGit
{
    public class ValidateStoreObject
    {
        List<string> _errorList = new List<string>();
        public Boolean validate(Store store)
        {
            //Check if Series is found 
            if (string.IsNullOrWhiteSpace(store.series))
            {
                _errorList.Add("Series Not Found");

            }
            //Check Store Id found
            if (string.IsNullOrWhiteSpace(store.storeId))
            {
                _errorList.Add("StoreId Not Found");
            }
            //Store Id 5 digits
            if (!string.IsNullOrWhiteSpace(store.storeId) && !Regex.IsMatch(store.storeId, @"^\d{5}$"))
            {
                _errorList.Add("StoreId Format Incorrect. Needs to be 5 digits.");

            }

            //Check RTP Version found
            if (string.IsNullOrWhiteSpace(store.rtpVersion))
            {
                _errorList.Add("RTP Version Not Found");

            }
            //Check region
            if (string.IsNullOrWhiteSpace(store.region))
            {
                _errorList.Add("Region Not Found");

            }
            //Check Profile
            if (string.IsNullOrWhiteSpace(store.profile))
            {
                _errorList.Add("Profile Not Found");

            }
            //Check Market
            if (string.IsNullOrWhiteSpace(store.market))
            {
                _errorList.Add("Market Not Found");

            }
            //Check CreatedBy
            if (string.IsNullOrWhiteSpace(store.createdBy))
            {
                _errorList.Add("CreatedBy Not Found");

            }
            if (_errorList.Count > 0)
            {
                return false;
            }
            return true;
        }
        public List<string> ErrorList()
        {
            return _errorList;
        }
    }
}
