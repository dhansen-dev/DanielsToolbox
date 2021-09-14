
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

using System;

namespace DanielsToolbox.Models
{
    [EntityLogicalNameAttribute("asyncoperation")]
    public class AsyncOperation : Entity
    {
        public AsyncOperation() : base("asyncoperation")
        {
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

        public enum AsyncOperationStateCode
        {
            Ready = 0,
            InProgess = 1,
            Locked = 2,
            Completed = 3
        };

        public TimeSpan ExecutionTimeSpan { get => TimeSpan.FromMinutes(GetAttributeValue<double>("executiontimespan")); }
        public string FriendlyMessage { get => GetAttributeValue<string>("friendlymessage"); }        

        public AsyncOperationStatusCode StatusCode { get => (AsyncOperationStatusCode)GetAttributeValue<OptionSetValue>("statuscode")?.Value; }
        public AsyncOperationStateCode StateCode { get => (AsyncOperationStateCode)GetAttributeValue<OptionSetValue>("statecode")?.Value; }

        public bool HasStarted()
            => !(StatusCode == AsyncOperationStatusCode.WaitingForResources || StatusCode == AsyncOperationStatusCode.Waiting);

        public bool IsCompleted()
            => StatusCode == AsyncOperationStatusCode.Succeeded || StatusCode == AsyncOperationStatusCode.Failed || StatusCode == AsyncOperationStatusCode.Canceled;
    }
}