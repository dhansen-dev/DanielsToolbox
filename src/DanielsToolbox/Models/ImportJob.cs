
using Microsoft.Xrm.Sdk;

using System;

namespace DanielsToolbox.Models
{
    public class ImportJob
    {
        private readonly Entity _entity;

        public ImportJob(Entity entity)
        {
            _entity = entity;
        }

        public Guid Id { get => _entity.GetAttributeValue<Guid>("importjobid"); }

        public double Progress { get => _entity.GetAttributeValue<double>("progress"); }

        [AttributeLogicalName("completedon")]
        public DateTime? CompletedOn { get => _entity.GetAttributeValue<DateTime?>("completedon"); }

        public bool IsCompleted()
            => CompletedOn.HasValue;
    }
}