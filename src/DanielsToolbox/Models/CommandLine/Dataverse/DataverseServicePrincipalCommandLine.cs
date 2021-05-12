using DanielsToolbox.Managers;

using Microsoft.PowerPlatform.Dataverse.Client;

using System;
using System.Collections.Generic;
using System.CommandLine;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class DataverseServicePrincipalCommandLine
    {
        public Uri InstanceUri { get; init; }
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }

        public int? ConnectionTimeout { get; init; }

        public Guid? ImpersonateUser { get; init; }

        public ServiceClient Connect()
            => ServiceClientManager.CreateClient(InstanceUri, ClientId, ClientSecret, ConnectionTimeout, ImpersonateUser);

        public static IEnumerable<Symbol> Arguments()
                        => new Symbol[] {
                            new Argument<Uri>("instance-uri", "Url to instance to export from"),
                            new Argument<string>("client-id", "Client id"),
                            new Argument<string>("client-secret", "Client secret"),
                            new Option<int?>("--connection-timeout", () => 15, "Connection timeout in minutes"),
                            new Option<Guid?>("--impersonate-user", "User id to impersonate")
        };
    }

}