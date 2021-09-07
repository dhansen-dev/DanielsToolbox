using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using static DanielsToolbox.Models.PluginStepImage;

namespace DanielsToolbox.Models
{
    public class PluginStep
    {
        public PluginStep(object o, Type pluginType)
        {
            var propertyType = o.GetType();

            Id = Guid.Parse(propertyType.GetProperty(nameof(Id)).GetValue(o).ToString());
            Name = (string)propertyType.GetProperty(nameof(Name)).GetValue(o);
            Rank = (int)propertyType.GetProperty(nameof(Rank)).GetValue(o);
            AsyncAutoDelete = (AsyncAutoDeleteEnum)propertyType.GetProperty(nameof(AsyncAutoDelete)).GetValue(o);
            Description = (string)propertyType.GetProperty(nameof(Description)).GetValue(o);
            EntityImages = GetEntityImages(propertyType, o);
            FilteringAttributes = (string[])propertyType.GetProperty(nameof(FilteringAttributes)).GetValue(o);
            Stage = (ExecutionStage)propertyType.GetProperty(nameof(Stage)).GetValue(o);
            SupportedDeployment = (SupportedDeploymentEnum)propertyType.GetProperty(nameof(SupportedDeployment)).GetValue(o);
            TriggerOnEntity = (string)propertyType.GetProperty(nameof(TriggerOnEntity)).GetValue(o);
            Message = (string)propertyType.GetProperty(nameof(Message)).GetValue(o);
            Mode = (ExecutionMode)propertyType.GetProperty(nameof(Mode)).GetValue(o);
            PluginType = pluginType;
        }

        public static Dictionary<string, string> MessagePropertyNameLookUp { get; } = new Dictionary<string, string>
        {
            { "Assign", "Target" },
            { "Create", "id" },
            { "Delete", "Target" },
            { "DeliverIncoming", "Target" },
            { "DeliverPromote", "Target" },
            { "Route", "Target" },
            { "Send", "emailId" },
            { "SetStateDynamicEntity", "entityMoniker" },
            { "Update", "Target" }
        };

        public AsyncAutoDeleteEnum AsyncAutoDelete { get; }

        public string Description { get; }

        public List<PluginStepImage> EntityImages { get; }

        public string[] FilteringAttributes { get; }

        public Guid Id { get; }

        public string Name { get; }

        public int Rank { get; }

        public ExecutionStage Stage { get; }

        public SupportedDeploymentEnum SupportedDeployment { get; }

        public string TriggerOnEntity { get; }
        public string Message { get; }
        public ExecutionMode Mode { get; }

        public Type PluginType { get; }

        private List<PluginStepImage> GetEntityImages(Type t, object o)
        {
            var list = t.GetProperty("EntityImages").GetValue(o);

            if (list == null)
            {
                return null;
            }

            var images = new List<PluginStepImage>();

            var listCount = (int)list.GetType().GetProperty("Count").GetValue(list);

            var prop1 = list.GetType().GetProperty("Item");

            for (var i = 0; i < listCount; i++)
            {
                var entityImageType = prop1.GetValue(list, new object[] { i });

                var image = new PluginStepImage((ImageType)entityImageType, (string[])t.GetProperty("PreEntityImageAttributes").GetValue(o), (string[])t.GetProperty("PostEntityImageAttributes").GetValue(o));

                images.Add(image);
            }

            return images;
        }

        public enum ExecutionMode
        {
            Synchronous = 0,
            Asynchronous = 1
        };

        public enum ExecutionStage
        {
            PreValidation = 10,
            PreOperation = 20,
            MainOperation = 30,
            PostOperation = 40
        }

        public enum AsyncAutoDeleteEnum
        {
            Yes = 1,
            No = 0
        }

        public enum SupportedDeploymentEnum
        {
            ServerOnly = 0,
            MicrosoftDynamics365ClientForOutlookOnly = 1,
            Both = 2
        }
    }

    public class PluginStepImage
    {
        public PluginStepImage(ImageType entityImageType, string[] preEntityImageAttributes, string[] postEntityImageAttributes)
        {
            EntityImageType = entityImageType;
            PreEntityImageAttributes = preEntityImageAttributes;
            PostEntityImageAttributes = postEntityImageAttributes;
        }

        public enum ImageType
        {
            PreImage = 0,
            PostImage = 1,
            Both = 2
        }

        public ImageType EntityImageType { get; }
        public string[] PostEntityImageAttributes { get; }
        public string[] PreEntityImageAttributes { get; }
    }

    public class PluginAssembly
    {
        private readonly string pluginAssemblyPath;


        static PluginAssembly()
        {
            var fullPath = Path.GetFullPath("LIB\\Microsoft.Xrm.Sdk.dll");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }

        public PluginAssembly(string pluginAssemblyPath)
        {            
            this.pluginAssemblyPath = pluginAssemblyPath;

            var assemblyData = File.ReadAllBytes(pluginAssemblyPath);

            var assembly = Assembly.Load(assemblyData);

            var exportedTypes = assembly.ExportedTypes;

            var pluginTypes = assembly.GetExportedTypes().Where(t => t.BaseType?.Name == "PluginBase");

            Name = assembly.GetName().Name;
            Version = assembly.GetName().Version;
            Data = assemblyData;

            foreach (var pluginType in pluginTypes)
            {
                var pluginObj = Activator.CreateInstance(pluginType);

                var steps = pluginObj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

                var list = steps[0].GetValue(pluginObj);

                var listCount = (int)list.GetType().GetProperty("Count").GetValue(list);

                var prop1 = list.GetType().GetProperty("Item");

                var plugin = new Plugin(pluginType, pluginObj);

                for (var i = 0; i < listCount; i++)
                {
                    var propVal = prop1.GetValue(list, new object[] { i });

                    var pluginStep = new PluginStep(propVal, pluginType);

                    plugin.PluginSteps.Add(pluginStep);
                }

                Plugins.Add(plugin);
            }

            foreach (var customAPI in exportedTypes.Where(t => t.BaseType?.Name == "CustomAPI"))
            {
                var customAPIObject = Activator.CreateInstance(customAPI);
                Plugins.Add(new Plugin(customAPI, customAPIObject));
            }
        }

        public byte[] Data { get; }
        public string Name { get; }
        public List<Plugin> Plugins { get; } = new List<Plugin>();
        public Version Version { get; }
    }

    public class Plugin
    {
        public Plugin(Type pluginType, object o)
        {
            ExtensionId = Guid.Parse(pluginType.GetProperty(nameof(ExtensionId)).GetValue(o).ToString());
            ExtensionDescription = (string)pluginType.GetProperty(nameof(ExtensionDescription)).GetValue(o);
            FullName = pluginType.FullName;
            TypeName = pluginType.Name;
        }

        public Guid ExtensionId { get; }

        public string ExtensionDescription { get; }
        public string FullName { get; }
        public string TypeName { get; }
        public List<PluginStep> PluginSteps { get; } = new List<PluginStep>();
    }
}
