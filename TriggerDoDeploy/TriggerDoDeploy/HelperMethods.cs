using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace TriggerDoDeploy
{
    public class HelperMethods
    {
        public static HttpMethod GetHttpMethod(string method)
        {
            switch (method.ToUpper())
            {
                case "GET":
                    return HttpMethod.Get;
                case "POST":
                    return HttpMethod.Post;
                case "PATCH":
                    return HttpMethod.Patch;
                case "PUT":
                    return HttpMethod.Put;
                case "HEAD":
                    return HttpMethod.Head;
                default:
                    return HttpMethod.Post;
            }
        }
    }
}
