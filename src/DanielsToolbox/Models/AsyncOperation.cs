
using Microsoft.Xrm.Sdk;

using System;

namespace DanielsToolbox.Models
{
    public class AsyncOperation
    {
        private readonly Entity _entity;

        public AsyncOperation(Entity entity)
        {
            _entity = entity;
        }

        public enum AsyncOperationStatusCode
        {
            WaitingForResources = 0,
            Waiting = 10,
            InProgress = 20,
            Pausing = 21,
            Cancelling = 22,
            Succeeded = 30,
            Failed = 31,
            Canceled = 32
        }

        public TimeSpan ExecutionTimeSpan { get => TimeSpan.FromMinutes(_entity.GetAttributeValue<double>("executiontimespan")); }
        public string FriendlyMessage { get => _entity.GetAttributeValue<string>("friendlymessage"); }
        public Guid Id { get => _entity.GetAttributeValue<Guid>("asyncoperationid"); }

        public AsyncOperationStatusCode StatusCode { get => (AsyncOperationStatusCode)_entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value; }

        public bool HasStarted()
            => !(StatusCode == AsyncOperationStatusCode.WaitingForResources || StatusCode == AsyncOperationStatusCode.Waiting);

        public bool IsCompleted()
            => StatusCode == AsyncOperationStatusCode.Succeeded || StatusCode == AsyncOperationStatusCode.Failed || StatusCode == AsyncOperationStatusCode.Canceled;
    }
}