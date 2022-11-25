using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TriggerRepositoryPreparation.Core;
using TriggerRepositoryPreparation.Models;
using TriggerRepositoryPreparation.Models.Output;

namespace TriggerRepositoryPreparation.Services
{
    public static class ConfigurationService
    {
        public static JObject initializeNewConfig { get; set; }

        #region Private Methods
        /// <summary>
        /// Mapping against the new format only.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="template"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static JObject MapNewFormat(JObject input, JObject template, ILogger log)
        {
            foreach (JProperty property in template.Properties())
            {
                string patternProperty = template[property.Name].Value<string>().Replace(" ", string.Empty);

                // Identify if this is a property match.
                if (patternProperty.StartsWith("{") || patternProperty.EndsWith("}"))
                {
                    patternProperty = patternProperty.Replace("{", string.Empty).Replace("}", string.Empty);

                    // Complex property match.
                    // For eg. "components.version"
                    if (patternProperty.IndexOf(".") >= 0)
                    {
                        string[] properties = patternProperty.Split(".");

                        template[property.Name] = HeirarchicalPropertyMatch(input, properties);
                    }
                    else
                    {
                        // Simple property match.
                        if (input[patternProperty] != null)
                        {
                            // Simple property match and replace.
                            template[property.Name] = input[patternProperty];
                        }
                    }
                }
                // Otherwise, Hard-coded values. So, do nothing.
            }

            return template;
        }

        /// <summary>
        /// Mapping against the old format for backward compatibility.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="template"></param>
        /// <param name="componentMatch"></param>
        /// <param name="componentVersion"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static bool MapOldFormat(JObject input, JObject template, string componentMatch, string componentVersion, ILogger log)
        {
            foreach (JProperty property in template.Properties())
            {
                string patternProperty = template[property.Name].Value<string>().Replace(" ", string.Empty);

                if (property.Name == componentMatch)
                {
                    template[property.Name] = componentVersion;
                }
                else
                {
                    if (patternProperty.StartsWith("{") || patternProperty.EndsWith("}"))
                    {
                        patternProperty = patternProperty.Replace("{", string.Empty).Replace("}", string.Empty);

                        // Simple property match.
                        if (input[patternProperty] == null)
                        {
                            template[property.Name] = "";
                        }
                        else
                        {
                            // Simple property match and replace.
                            template[property.Name] = input[patternProperty];
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Recursive function used by MapNewFormat() for mapping.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="properties"></param>
        /// <param name="startAt"></param>
        /// <returns></returns>
        private static JValue HeirarchicalPropertyMatch(JObject input, string[] properties, int startAt = 0)
        {
            JValue foundValue = null;

            for (int i = startAt; i < properties.Count(); i++)
            {
                if (input[properties[i]] != null)
                {
                    switch (input[properties[i]].Type)
                    {
                        case JTokenType.Object:
                            break;
                        case JTokenType.Array:
                            foreach (var item in input[properties[i]].Value<JArray>())
                            {
                                foundValue = HeirarchicalPropertyMatch(item.ToObject<JObject>(), properties, ++i);
                            }
                            break;
                        default:
                            foundValue = input[properties[i]].Value<JValue>();
                            break;
                    }

                    if (foundValue != null)
                        return foundValue;
                }
            }

            return null;
        }
        #endregion

        /// <summary>
        /// Uses the incoming DoDeployPending event to build a Deployment document for the "StoreConfigurationLogs" container.
        /// </summary>
        /// <param name="inputDocument"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> MapInputComponentsToTemplate(StoreConfigurationDocument inputDocument, ILogger log)
        {
            bool mappingDone = false;

            // Get the Mapping template from "StoreConfigurationSettings".
            Stream mappingTemplate = await DBFunctions.GetDocument(Environment.GetEnvironmentVariable("StoreConfigurationSettings_DatabaseId"),
                        Environment.GetEnvironmentVariable("StoreConfigurationSettings_CollectionId"), $"SELECT TOP 1 * FROM c WHERE c.rtpType = '{inputDocument.rtpType}' ORDER BY c.templateId DESC", log);

            if (mappingTemplate == null)
            {
                log.LogError("No Templates available to create Store configuration");
            }
            else
            {
                StreamReader reader = new StreamReader(mappingTemplate);
                string templateDocument = await reader.ReadToEndAsync();
                var template = JObject.Parse(templateDocument).Property("Documents").Value.First();

                ConfigurationTemplate configTemplate = JsonConvert.DeserializeObject<ConfigurationTemplate>(template.ToString());

                // Init.
                inputDocument.storeConfigs = new JArray();
                JObject storeConfigs = new JObject();

                bool oldFormatMapped = false;

                initializeNewConfig = new JObject(configTemplate.initializeNewStore);

                // For each component in the Deployment, lookup the template for a mapping definition.
                inputDocument.components.ForEach(c =>
                {
                    if (configTemplate.components.Any(tc => tc.component == c.name))
                    {
                        // Creating a copy of the input containing only the Component being processed.
                        var inputWithOneComponent = JsonConvert.DeserializeObject<StoreConfigurationDocument>(JObject.FromObject(inputDocument).ToString());
                        inputWithOneComponent.components = inputWithOneComponent.components.Where(r => r.name == c.name).ToList();

                        var newFormatOutput = MapNewFormat(JObject.FromObject(inputWithOneComponent), configTemplate.components.FirstOrDefault(tc => tc.component == c.name).mapping, log);

                        if (newFormatOutput != null)
                        {
                            var currentComponent = configTemplate.components.Where(x => x.component == c.name).FirstOrDefault();

                            if (storeConfigs.Property(currentComponent.configFile) == null)
                            {
                                storeConfigs.Add(currentComponent.configFile, new JObject(
                                                    new JProperty("storePackages", new JArray(newFormatOutput))
                                                    ));
                            }
                            else
                            {
                                JObject tempStoreConfigObject = JObject.Parse(storeConfigs.Property(currentComponent.configFile).Value.ToString());
                                JArray tempStoreConfigs = JArray.Parse(tempStoreConfigObject.Property("storePackages").Value.ToString());
                                tempStoreConfigs.Add(newFormatOutput);
                                tempStoreConfigObject.Property("storePackages").Replace(new JProperty("storePackages", new JArray(tempStoreConfigs)));
                                storeConfigs.Property(currentComponent.configFile).Value = tempStoreConfigObject;
                            }

                            log.LogInformation($"Mapping for component '{c.name}' completed, output is {newFormatOutput}");
                        }
                        else
                        {
                            log.LogError($"Mapping for component '{c.name}' failed");
                        }

                        // Check if this property exists, in case it is removed in future.
                        // This is being maintained for backward compatibility for those Stores 
                        // which are still using the old Store configuration format.
                        if (configTemplate.backwardCompatibilty != null)
                        {
                            // Check if the Component matches the list in "backwardcompatibilty".
                            if (configTemplate.backwardCompatibilty.components[c.name] != null)
                            {
                                // Pass the matched Component name and version as parameters.
                                if (MapOldFormat(JObject.FromObject(inputWithOneComponent), configTemplate.backwardCompatibilty.mapping,
                                    configTemplate.backwardCompatibilty.components[c.name].Value<string>(),
                                    inputWithOneComponent.components.Where(m => m.name == c.name).SingleOrDefault().version, log))
                                {
                                    oldFormatMapped = true;
                                    log.LogInformation($"Mapping of component '{c.name}' in the old format completed");
                                }
                                else
                                {
                                    log.LogError($"Mapping for component '{c.name}' in the old format failed");
                                }
                            }
                        }
                        inputWithOneComponent = null;
                    }
                    else
                    {
                        string errorMessage = $"Mapping required for component '{c.name}' in {Environment.GetEnvironmentVariable("StoreConfigurationSettings_CollectionId")} container";

                        inputDocument.errors.Add(errorMessage);
                        log.LogError(errorMessage);
                    }
                });

                // Add the mappings to "storeConfigs".
                if (storeConfigs.Count > 0)
                {
                    if (oldFormatMapped)
                    {
                        // Add old format to "storeversion".
                        foreach (var property in configTemplate.backwardCompatibilty.mapping.Properties())
                        {
                            storeConfigs.Property("storeversion").Value.Last.AddBeforeSelf(new JProperty(property.Name, configTemplate.backwardCompatibilty.mapping[property.Name].Value<string>()));
                        }
                    }

                    // Update "storeConfigs".
                    foreach (var currProperty in storeConfigs.Properties())
                    {
                        JObject storeConfig = new JObject();
                        storeConfig.Add("location", configTemplate.components.FirstOrDefault(c => c.configFile == currProperty.Name).location);
                        storeConfig.Add("configFile", configTemplate.components.FirstOrDefault(c => c.configFile == currProperty.Name).configFile);
                        storeConfig.Add(currProperty.Name, currProperty.Value);

                        inputDocument.storeConfigs.Add(storeConfig);
                    }
                    mappingDone = true;
                }

                log.LogInformation($"Mapping for document completed: {JsonConvert.SerializeObject(inputDocument)}");
            }

            return mappingDone;
        }

        /// <summary>
        /// Persists the Deployment document into the "StoreConfigurationLogs" container.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> LogInputEvent(StoreConfigurationDocument document, ILogger log)
        {
            var result = await DBFunctions.AddDocument(Environment.GetEnvironmentVariable("StoreConfigurationLogs_DatabaseId"),
                Environment.GetEnvironmentVariable("StoreConfigurationLogs_CollectionId"), document.deploymentId, JsonConvert.SerializeObject(document), log);

            if (result.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// This method will update the "StoreConfigurationLogs" container for a deploymentId+storeId and update
        /// the document based on the value of the parameter "status".
        /// If status = "CancelDeploymentPending", the "isCanceled" flag is set to true.
        /// If status = "RollbackRequestPending", the "rollback" flag is set to true.
        /// </summary>
        /// <param name="inputDocument"></param>
        /// <param name="status"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> UpdateDeployment(StoreConfigurationDocument document, string status, ILogger log)
        {
            string filter = "from c where c.deploymentId = '" + document.deploymentId + "' and c.storeId = '" + document.storeId + "'";

            log.LogInformation($"Updating document '{filter}' for status '{status}'");

            // Get the document being updated from the StoreConfigurationLogs DB container.
            Stream documentStream = await DBFunctions.GetDocument(Environment.GetEnvironmentVariable("StoreConfigurationLogs_DatabaseId"),
                        Environment.GetEnvironmentVariable("StoreConfigurationLogs_CollectionId"), $"SELECT * FROM c WHERE c.deploymentId = '{document.deploymentId}' AND c.storeId = '{document.storeId}'", log);

            StreamReader reader = new StreamReader(documentStream);
            var tempDocument = JObject.Parse(await reader.ReadToEndAsync()).Property("Documents").Value.First();
            var updateDocument = JsonConvert.DeserializeObject<StoreConfigurationDocument>(tempDocument.ToString());

            var patchOperations = new List<PatchOperation>();
            string path = Environment.GetEnvironmentVariable("PathToUpdateTagsInDeployment");
            string updateProperty = document.UpdatePropertyPath(status);
            string updatePropertyValue = document.UpdatePropertyValue(status);
            string configFileName = string.Empty;

            // Update the "/isCanceled" or "/rollback" property.
            patchOperations.Add(PatchOperation.Add<string>(updateProperty, updatePropertyValue));

            for (int storeConfigsIndex = 0; storeConfigsIndex < updateDocument.storeConfigs.Count; storeConfigsIndex++)
            {
                configFileName = updateDocument.storeConfigs[storeConfigsIndex].Value<string>("configFile");
                for (int storePackagesIndex = 0; storePackagesIndex < updateDocument.storeConfigs.Count; storePackagesIndex++)
                {
                    patchOperations.Add(PatchOperation.Add<string>(string.Format(path, storeConfigsIndex, configFileName, storePackagesIndex, updateProperty), updatePropertyValue));

                    log.LogInformation($"Updating path '{string.Format(path, storeConfigsIndex, configFileName, storePackagesIndex, updateProperty)}' with '{updatePropertyValue}'");
                }
            }

            var result = await DBFunctions.UpdateDocument(Environment.GetEnvironmentVariable("StoreConfigurationLogs_DatabaseId"),
                Environment.GetEnvironmentVariable("StoreConfigurationLogs_CollectionId"), filter, patchOperations, document.id, document.deploymentId, log);

            if (result.IsSuccessStatusCode)
                return true;
            else
                return false;
        }
    }
}
