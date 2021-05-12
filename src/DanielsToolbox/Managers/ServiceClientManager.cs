
using Microsoft.PowerPlatform.Dataverse.Client;

using System;

namespace DanielsToolbox.Managers
{
    public class ServiceClientManager
    {
        public static ServiceClient CreateClient(Uri instanceUrl, string clientId, string clientSecret, int? maxConnectionTimeout, Guid? userToImpersonate)
        {
            Console.WriteLine("Connecting to DataVerse");

            if (maxConnectionTimeout.HasValue)
            {
                Console.WriteLine("Setting " + nameof(ServiceClient.MaxConnectionTimeout) + " to " + maxConnectionTimeout + " minutes.");

                ServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(maxConnectionTimeout.Value);
            }

            var client = new ServiceClient(instanceUrl, clientId, clientSecret, true);

            if (!client.IsReady)
            {
                throw client.LastException;
            }

            Console.WriteLine("Connected to " + client.ConnectedOrgFriendlyName);

            if (userToImpersonate.HasValue)
            {
                client.CallerId = userToImpersonate.Value;

                Console.WriteLine("Impersonating user with id " + userToImpersonate);
            }

            return client;
        }
    }
}
