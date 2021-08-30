using DanielsToolbox.Models;
using DanielsToolbox.Models.CommandLine.XRMFramework;

using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

using static DanielsToolbox.Models.PluginStepImage;

namespace DanielsToolbox.Managers
{
    public class PluginManager
    {

        static PluginManager()
        {
            var fullPath = Path.GetFullPath("LIB\\Microsoft.Xrm.Sdk.dll");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
        

        public PluginManager()
        {
        
        }

        public static async Task RegisterPluginsInCRM(RegisterPluginsCommandLine commandLine, ServiceClient client)
        {
            var pluginAssembly = new PluginAssembly(commandLine.PluginAssemblyPath.FullName);

            if (commandLine.SyncPluginSteps)
            {
                await SyncRemotePlugins(client, pluginAssembly);
            }

            await RegisterPluginAssembly(client, commandLine, pluginAssembly);
        }

        public static async Task<Guid> RegisterPluginAssembly(ServiceClient client, RegisterPluginsCommandLine commandLine, PluginAssembly pluginAssembly)
        {
            var o = client;

            Guid pluginAssemblyId;

            var existingPluginAssemblyId = await GetExistingEntityId(o, "pluginassembly", new ConditionExpression("name", ConditionOperator.Equal, pluginAssembly.Name));

            var entity = CreatePluginAssembly(pluginAssembly.Data, pluginAssembly.Name, pluginAssembly.Version);

            if (existingPluginAssemblyId.HasValue)
            {
                Console.WriteLine("Updating plugin assembly " + pluginAssembly.Name);

                pluginAssemblyId = existingPluginAssemblyId.Value;
                entity.Id = pluginAssemblyId;
                o.Update(entity);
            }
            else
            {
                Console.WriteLine("Creating plugin assembly " + pluginAssembly.Name);
                pluginAssemblyId = o.Create(entity);

                await AddSolutionComponent(o, pluginAssemblyId, 91, commandLine.SolutionName);
            }

            Console.WriteLine("Done with plugin assembly");

            if (commandLine.UpdateOnlyPluginAssembly == false)
            {
                foreach (var plugin in pluginAssembly.Plugins)
                {
                    Console.WriteLine("Registering plugin " + plugin.TypeName);

                    await RegisterPluginSteps(client, commandLine.SolutionName, pluginAssemblyId, plugin);
                }
            }
            else
            {
            }

            return pluginAssemblyId;
        }

        private static async Task SyncRemotePlugins(ServiceClient client, PluginAssembly localPluginAssembly)
        {
            var assemblyQuery = new QueryExpression("pluginassembly");
            assemblyQuery.Criteria.AddCondition("name", ConditionOperator.Equal, localPluginAssembly.Name);

            var pluginTypes = assemblyQuery.AddLink("plugintype", "pluginassemblyid", "pluginassemblyid", JoinOperator.Inner);
            pluginTypes.EntityAlias = "pluginType";
            pluginTypes.Columns.AllColumns = true;

            var steps = pluginTypes.AddLink("sdkmessageprocessingstep", "plugintypeid", "eventhandler", JoinOperator.LeftOuter);
            steps.EntityAlias = "step";
            steps.Columns.AllColumns = true;

            var plugins = await client.RetrieveMultipleAsync(assemblyQuery);

            foreach (var remotePluginType in plugins.Entities.ToLookup(e => GetAliasedValue<Guid>(e, "pluginType.plugintypeid")))
            {
                var localPluginType = localPluginAssembly.Plugins.SingleOrDefault(localPlugin => localPlugin.ExtensionId == remotePluginType.Key);

                if (localPluginType != null)
                {
                    var remotePluginSteps = remotePluginType.ToDictionary(t => GetAliasedValue<Guid>(t, "step.sdkmessageprocessingstepid"));

                    var localPluginStepIds = localPluginType.PluginSteps.Select(localPluginStep => localPluginStep.Id);
                    var remotePluginStepIds = remotePluginSteps.Select(remotePluginStep => remotePluginStep.Key);

                    var stepDiff = remotePluginStepIds.Except(localPluginStepIds);

                    if (stepDiff.Any())
                    {
                        foreach (var removedStep in stepDiff)
                        {
                            if (GetAliasedValue<BooleanManagedProperty>(remotePluginSteps[removedStep], "step.iscustomizable").Value)
                            {
                                Console.WriteLine("Will remove step " + removedStep);
                            }
                        }
                    } 
                    else
                    {
                        foreach(var remotePluginStep in remotePluginSteps)
                        {
                            var remoteImageQuery = new QueryExpression("sdkmessageprocessingstepimage")
                            {
                                ColumnSet = new ColumnSet(true)
                            };

                            remoteImageQuery.Criteria.AddCondition("sdkmessageprocessingstepid", ConditionOperator.Equal, remotePluginStep.Key);

                            var remoteImageEntities = (await client.RetrieveMultipleAsync(remoteImageQuery))?.Entities;

                            var remoteImages = remoteImageEntities.Select(image => image.GetAttributeValue<OptionSetValue>("imagetype").Value);
                            var localImages = localPluginType.PluginSteps.Single(localPlugin => localPlugin.Id == remotePluginStep.Key)?.EntityImages.Select(localEntityImages => (int)localEntityImages.EntityImageType);

                            var imageDiffs = remoteImages.Except(localImages);

                            foreach(var imageDiff in imageDiffs)
                            {
                                var remoteImageIdToDelete = remoteImageEntities.Single(remoteImage => remoteImage.GetAttributeValue<OptionSetValue>("imagetype").Value == imageDiff);

                                Console.WriteLine("Deleting image " + remoteImageIdToDelete);
                            }
                        }
                    }
                } 
                else
                {
                    Console.WriteLine("Will remove plugin type " + remotePluginType.Key);
                }
            }
        }

        public static async Task RegisterPluginSteps(ServiceClient client, string solutionName, Guid pluginAssemblyId, Plugin plugin)
        {           

            var lengthOfDescription = plugin.ExtensionDescription?.Length > 256 ? 256 : (plugin.ExtensionDescription?.Length ?? 0);

            var pluginTypeEntity = new Entity("plugintype")
            {
                Attributes = new AttributeCollection
                        {
                            { "plugintypeid", plugin.ExtensionId },
                            { "typename", plugin.FullName },
                            { "name", plugin.FullName },
                            { "friendlyname", plugin.TypeName },
                            { "description", plugin.ExtensionDescription?.Substring(0, lengthOfDescription) },
                            { "pluginassemblyid", new EntityReference("pluginassembly", pluginAssemblyId) }
                        }
            };

            var existingPluginTypeId = await GetExistingEntityId(client, "plugintype", new ConditionExpression("typename", ConditionOperator.Equal, plugin.FullName));
            Guid pluginTypeId;
            if (existingPluginTypeId.HasValue)
            {
                pluginTypeId = existingPluginTypeId.Value;
                pluginTypeEntity.Id = pluginTypeId;
                await client.UpdateAsync(pluginTypeEntity);
            }
            else
            {
                pluginTypeId = await client.CreateAsync(pluginTypeEntity);
            }

            foreach (var step in plugin.PluginSteps)
            {
                Console.WriteLine("Registering step " + step.Name);
                Console.WriteLine(step.Description);
                Console.WriteLine(step.Message ?? "" + " " + step.TriggerOnEntity ?? "");
                Console.WriteLine(step.Name);
                Console.WriteLine(string.Join(" ", step.FilteringAttributes ?? Array.Empty<object>()));
                Console.WriteLine("");

                var messageQuery = new QueryExpression("sdkmessage")
                {
                    ColumnSet = new ColumnSet(true)
                };

                messageQuery.Criteria.AddCondition("name", ConditionOperator.Equal, step.Message);

                var messages = (await client.RetrieveMultipleAsync(messageQuery)).Entities;

                var message = messages[0];

                var messageFilterQuery = new QueryExpression("sdkmessagefilter")
                {
                    ColumnSet = new ColumnSet(false)
                };

                Entity messageFilter = null;

                if (!string.IsNullOrEmpty(step.TriggerOnEntity))
                {
                    messageFilterQuery.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, step.TriggerOnEntity);
                    messageFilterQuery.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, message?.Id);
                    var messageFilterResult = await client.RetrieveMultipleAsync(messageFilterQuery);

                    messageFilter = messageFilterResult.Entities[0];
                }

                var stepToCreate = new Entity("sdkmessageprocessingstep")
                {
                    Attributes = new AttributeCollection
                            {
                                {"sdkmessageprocessingstepid", step.Id },
                                { "filteringattributes", step.FilteringAttributes != null ? string.Join(",", step.FilteringAttributes) : "" },
                                { "mode", new OptionSetValue(step.Mode) },
                                { "name", step.Name },
                                { "description", step.Description },
                                { "rank", step.Rank },
                                { "stage", new OptionSetValue(step.Stage)},
                                { "supporteddeployment", new OptionSetValue(step.SupportedDeployment) },
                                { "plugintypeid", new EntityReference("plugintype", pluginTypeId) },
                                { "sdkmessageid",message.ToEntityReference() },
                                { "sdkmessagefilterid",  messageFilter?.ToEntityReference() }
                            }
                };

                Guid stepId;

                var existingStepId = await GetExistingEntityId(client, "sdkmessageprocessingstep", new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, step.Id));

                if (existingStepId.HasValue)
                {
                    stepId = existingStepId.Value;
                    stepToCreate.Id = stepId;
                    await client.UpdateAsync(stepToCreate);
                }
                else
                {
                    stepId = await client.CreateAsync(stepToCreate);
                    await AddSolutionComponent(client, stepId, 92, solutionName);
                }

                await RegisterPluginImages(client, step, stepId);
            }
        }

        public static async Task AddSolutionComponent(ServiceClient client, Guid componentId, int componentType, string solutionName)
        {
            if(solutionName == null)
            {
                await Task.CompletedTask;
            }

            await client.ExecuteAsync(new OrganizationRequest("await AddSolutionComponent")
            {
                Parameters = new ParameterCollection
                {
                    { "ComponentId", componentId },
                    { "ComponentType", componentType },
                    { "SolutionUniqueName", solutionName },
                    { "AddRequiredComponents", false },
                    { "DoNotIncludeSubcomponents", false }
                }
            });
        }

        public static Entity CreatePluginAssembly(byte[] assembly, string assemblyName, Version version)
        {
            return new Entity("pluginassembly")
            {
                Attributes = new AttributeCollection
                    {
                        { "version", version.ToString() },
                        { "name", assemblyName },
                        { "isolationmode", new OptionSetValue(2) },
                        { "content", Convert.ToBase64String(assembly) }
                    }
            };
        }

        public static async Task<Guid?> GetExistingEntityId(ServiceClient client, string entityLogicalName, params ConditionExpression[] conditions)
        {
            var queryExpression = new QueryExpression(entityLogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo
                {
                    ReturnTotalRecordCount = true
                }
            };
            foreach (var condition in conditions)
            {
                queryExpression.Criteria.AddCondition(condition);
            }

            var result = await client.RetrieveMultipleAsync(queryExpression);

            if (result.Entities.Count() > 1)
            {
                throw new Exception("Cannot update if there are multiple matches");
            }
            else if (result.Entities.Count() == 0)
            {
                return default;
            }

            return result.Entities[0].Id;
        }

        public static async Task RegisterPluginImages(ServiceClient client, PluginStep step, Guid stepId)
        {
            foreach (var entityImage in step.EntityImages)
            {
                Console.WriteLine("Registering plugin images");

                if (entityImage.EntityImageType == ImageType.PreImage)
                {
                    Guid preEntityImageId;

                    var preImage = new Entity("sdkmessageprocessingstepimage")
                    {
                        Attributes = new AttributeCollection
                            {
                                { "attributes", string.Join(",", entityImage.PreEntityImageAttributes)  },
                                { "description", "Pre entity image" },
                                { "entityalias", "preEntityImage" },
                                { "imagetype", new OptionSetValue(0) },
                                { "messagepropertyname", PluginStep.MessagePropertyNameLookUp[step.Message] },
                                { "name", "Pre entity image" },
                                { "sdkmessageprocessingstepid", new EntityReference("sdkmessageprocessingstep", stepId) }
                            }
                    };

                    var existingPreImageId = await GetExistingEntityId(client, "sdkmessageprocessingstepimage",
                        new ConditionExpression("entityalias", ConditionOperator.Equal, "preEntityImage"),
                        new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId)
                        );

                    if (existingPreImageId.HasValue)
                    {
                        preEntityImageId = existingPreImageId.Value;
                        preImage.Id = preEntityImageId;
                        client.Update(preImage);
                    }
                    else
                    {
                        preEntityImageId = client.Create(preImage);
                    }
                }

                if (entityImage.EntityImageType == ImageType.PostImage)
                {
                    Guid postEntityImageId;

                    var postImage = new Entity("sdkmessageprocessingstepimage")
                    {
                        Attributes = new AttributeCollection
                            {
                                { "attributes", string.Join(",", entityImage.PostEntityImageAttributes)  },
                                { "description", "Post entity image" },
                                { "entityalias", "postEntityImage" },
                                { "imagetype", new OptionSetValue(1) },
                                { "messagepropertyname", PluginStep.MessagePropertyNameLookUp[step.Message] },
                                { "name", "Post entity image" },
                                { "sdkmessageprocessingstepid", new EntityReference("sdkmessageprocessingstep", stepId) }
                            }
                    };

                    var existingPostImageId = await GetExistingEntityId(client, "sdkmessageprocessingstepimage",
                        new ConditionExpression("entityalias", ConditionOperator.Equal, "postEntityImage"),
                        new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId)
                        );

                    if (existingPostImageId.HasValue)
                    {
                        postEntityImageId = existingPostImageId.Value;
                        postImage.Id = postEntityImageId;
                        client.Update(postImage);
                    }
                    else
                    {
                        postEntityImageId = client.Create(postImage);
                    }
                }

                
            }


        }

        private static TType GetAliasedValue<TType>(Entity e, string attributeLogicalName)
                => e.TryGetAttributeValue<AliasedValue>(attributeLogicalName, out var value) ? (TType)value.Value : default;
    }
}
