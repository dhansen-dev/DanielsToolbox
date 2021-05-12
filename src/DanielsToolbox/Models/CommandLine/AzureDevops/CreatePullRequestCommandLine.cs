
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DanielsToolbox.Models.CommandLine.AzureDevops
{
    public class CreatePullRequestCommandLine
    {
        public string MergeSourceBranchName { get; init; }
        public string MergeTargetBranchName { get; init; }
        public Guid? OrganizationId { get; init; }
        public string OrganizationName { get; init; }
        public string PersonalAccessToken { get; init; }
        public string PullRequestTitle { get; init; }
        public Guid RepositoryId { get; init; }
        public Guid[] Reviewers { get; init; }
        public bool UseLegacyUrl { get; init; }
        public static Command Create()
        {
            var command = new Command("create-pull-request", "Creates a pull request")
            {
                new Argument<string>("personal-access-token", "Personal access token"),
                new Argument<string>("organization-name", "Name of the organization in dev ops"),
                new Argument<Guid>("repository-id", "Id of the repository"),
                new Argument<string>("merge-source-branch-name", "Name of branch to create pull request from"),
                new Argument<string>("merge-target-branch-name", "Name of branch to merge pull request into"),
                new Argument<string>("pull-request-title", "Title of pull request"),
                new Option<Guid[]>("--reviewers", getDefaultValue: () => Array.Empty<Guid>(), "Comma separated list of reviewers"),
                new Option<bool>("--use-legacy-url"),
                new Option<Guid>("--organization-id")

            };

            command.Handler = CommandHandler.Create<CreatePullRequestCommandLine>(async (a) => await a.CreatePullRequest());

            return command;

        }

        public async Task CreatePullRequest()
        {
            var reviewers = string.Empty;

            if (Reviewers.Any())
            {
                var reviewerJsonList = Reviewers.Select(reviewerId => $"{{\"id\": \"{reviewerId}\",  \"isFlagged\": true, \"isRequired\": true}}");

                reviewers = string.Join(",", reviewerJsonList);
            }

            try
            {

                using (HttpClient client = new())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"pat:{PersonalAccessToken}")));

                    var json = $"{{\"title\": \"{PullRequestTitle}\", \"sourceRefName\": \"refs/heads/{MergeSourceBranchName}\", \"targetRefName\": \"refs/heads/{MergeTargetBranchName}\", \"reviewers\": [{reviewers}], \"completionOptions\": {{\"deleteSourceBranch\": true}} }}";

                    var requestUrl = UseLegacyUrl
                        ? $"https://{OrganizationName}.visualstudio.com/{OrganizationId}/_apis/git/repositories/{RepositoryId}/pullRequests?api-version=6.0"
                        : $"https://dev.azure.com/{OrganizationName}/_apis/git/repositories/{RepositoryId}/pullRequests?api-version=6.0";

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(requestUrl),
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    using (HttpResponseMessage response = await client.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                        }

                        string responseBody = await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}