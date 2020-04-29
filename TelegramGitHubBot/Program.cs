using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.InlineQueryResults;

namespace TelegramGitHubBot
{
    class Program
    {
        static ITelegramBotClient botClient;
        static ProductHeaderValue product = new ProductHeaderValue("GH_TelegramBot");
        static GitHubClient github = new GitHubClient(product);

        static void Main(string[] args)
        {
            if (!File.Exists(".token"))
            {
                Console.WriteLine("Put your Telegram bot token in .token file");
                return;
            } 

            if (args.Contains("--oauth-gen"))
            {
                if (!File.Exists(".github_clientid"))
                {
                    Console.WriteLine("Put your client id into .github_clientid");
                    return;
                }

                var request = new OauthLoginRequest(new FileTokenProvider(".github_clientid").Get())
                {
                    Scopes = { "read:user" }
                };
                var oauthLoginUrl = github.Oauth.GetGitHubLoginUrl(request);
                Console.WriteLine(oauthLoginUrl);
                return;
            }

            if (!File.Exists(".github_token") && File.Exists(".github_clientid") 
                && File.Exists(".github_clientsecret") && File.Exists(".github_authcode"))
            {
                Console.WriteLine("Fetching token from GitHub...");

                var clientid = new FileTokenProvider(".github_clientid").Get();
                var clientsecret = new FileTokenProvider(".github_clientsecret").Get();
                var code = new FileTokenProvider(".github_authcode").Get();
                var token = github.Oauth.CreateAccessToken(new OauthTokenRequest(clientid, clientsecret, code)).GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    Console.WriteLine("Fatal error: can't fetch token");
                    Console.WriteLine("Try to get a new oauth code using --oauth-gen");
                    return;
                }

                using (var w = new StreamWriter(".github_token"))
                    w.Write(token.AccessToken);

                Console.WriteLine("Done. Saved to .github_token");
            }

            if (!args.Contains("--no-github") && File.Exists(".github_token"))
            {
                github.Credentials = new Credentials(new FileTokenProvider(".github_token").Get());
                Console.WriteLine("Using private GitHub account.");
            }
            else
            {
                Console.WriteLine("Using limited guest GitHub account.");
                Console.WriteLine("Put your GitHub Ouath code into .github_authcode");
                Console.WriteLine("Use --oauth-gen argument to generate OAuth link.");
            }


            botClient = new TelegramBotClient(new FileTokenProvider(".token").Get());
            var me = botClient.GetMeAsync().Result;
            Console.WriteLine($"Working with {me.FirstName}.");

            botClient.OnInlineQuery += BotClient_OnInlineQuery;
            botClient.OnMessage += BotClient_OnMessage;
            botClient.StartReceiving();

            Thread.Sleep(-1);
        }

        private static void BotClient_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text == "/info")
            {
                var text = new StringBuilder();

#if DEBUG
                text.AppendLine("Debug build");
#endif
                text.AppendLine("System: " + Environment.OSVersion.VersionString);
                text.AppendLine("Clean search limit: " + Limits.CleanSearchLimit);
                text.AppendLine("Hybrid search limit: " + Limits.HybridSearchLimit);
                text.AppendLine("GitHub auth: " + github.Credentials.AuthenticationType);

                botClient.SendTextMessageAsync(e.Message.Chat, text.ToString());
            }
        }

        static InlineQueryResultArticle InlineFromRepo(Repository repo)
        {
            var text = new StringBuilder();
            text.AppendLine($"📄 {repo.Name} by {repo.Owner.Login}");
            if (!string.IsNullOrWhiteSpace(repo.Description)) text.AppendLine(repo.Description);
            text.AppendLine();
            if (repo.License != null && !string.IsNullOrWhiteSpace(repo.License.Name))
                text.AppendLine("📃 License: " + repo.License.Name);
            text.AppendLine("⭐️ Stars: " + repo.StargazersCount);
            text.AppendLine("👥 Forks: " + repo.ForksCount);
            text.AppendLine();
            text.AppendLine(repo.HtmlUrl);

            return new InlineQueryResultArticle(
                $"{repo.Owner.Login}/{repo.Name}", $"📄 {repo.Name} by {repo.Owner.Login}",
                new InputTextMessageContent(text.ToString()));
        }

        static InlineQueryResultArticle InlineFromUser(User owner)
        {
            var text = new StringBuilder();

            text.Append($"👨‍💻 {owner.Login}");

            if (!string.IsNullOrWhiteSpace(owner.Name)) text.AppendLine(" | " + owner.Name);
            else text.AppendLine();

            if (!string.IsNullOrWhiteSpace(owner.Bio))
            {
                text.AppendLine(owner.Bio);
                text.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(owner.Email))
                text.AppendLine("E-mail: " + owner.Email);

            if (!string.IsNullOrWhiteSpace(owner.Location))
                text.AppendLine("Location: " + owner.Location);

            text.AppendLine();
            if (owner.PublicRepos > 0)
                text.AppendLine(" - Public repos: " + owner.PublicRepos);
            if (owner.OwnedPrivateRepos > 0)
                text.AppendLine(" - Private repos: " + owner.OwnedPrivateRepos);
            if (owner.PublicGists > 0)
                text.AppendLine(" - Public gists: " + owner.PublicGists);
            if (owner.PrivateGists != null)
                text.AppendLine(" - Private gists: " + owner.PrivateGists);
            text.AppendLine();

            text.AppendLine(owner.HtmlUrl);

            return new InlineQueryResultArticle(
                owner.Login, $"👨‍💻 {owner.Login}{(string.IsNullOrWhiteSpace(owner.Name) ? "" : " | " + owner.Name)}",
                new InputTextMessageContent(text.ToString()));
        }

        private static void BotClient_OnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            var results = new List<InlineQueryResultBase>();

            try
            {
                var q = QueryParser.Parse(e.InlineQuery.Query);
                if (q.Type == QueryType.Exception) throw q.Exception;
                else if (q.Type == QueryType.Repository)
                {
                    var repo = github.Repository.Get(q.Owner, q.Repository).GetAwaiter().GetResult();
                    results.Add(InlineFromRepo(repo));
                }
                else if (q.Type == QueryType.SearchOrOwner)
                {
                    try
                    {
                        var owner = github.User.Get(q.Owner).GetAwaiter().GetResult();
                        results.Add(InlineFromUser(owner));
                    }
                    catch (NotFoundException)
                    {
                        var repos = github.Search.SearchRepo(new SearchRepositoriesRequest(q.Owner)).GetAwaiter().GetResult();
                        for (int i = 0; i < repos.Items.Count && i < Limits.HybridSearchLimit; i++)
                            results.Add(InlineFromRepo(github.Repository.Get(repos.Items[i].Id).GetAwaiter().GetResult()));

                        var users = github.Search.SearchUsers(new SearchUsersRequest(q.Owner)).GetAwaiter().GetResult();
                        for (int i = 0; i < users.Items.Count && i < Limits.HybridSearchLimit; i++)
                            results.Add(InlineFromUser(github.User.Get(users.Items[i].Login).GetAwaiter().GetResult()));
                    }

                    if (results.Count <= 0)
                        throw new Exception("Nothing found.");
                }
                else if (q.Type == QueryType.SearchRepo)
                {
                    var repos = github.Search.SearchRepo(new SearchRepositoriesRequest(q.Repository)).GetAwaiter().GetResult();

                    if (repos.Items.Count <= 0) throw new Exception("No repos found");

                    for (int i = 0; i < repos.Items.Count && i < Limits.CleanSearchLimit; i++)
                        results.Add(InlineFromRepo(github.Repository.Get(repos.Items[i].Id).GetAwaiter().GetResult()));
                }
                else if (q.Type == QueryType.SearchUser)
                {
                    var users = github.Search.SearchUsers(new SearchUsersRequest(q.Owner)).GetAwaiter().GetResult();

                    if (users.Items.Count <= 0) throw new Exception("No users found");

                    for (int i = 0; i < users.Items.Count && i < Limits.CleanSearchLimit; i++)
                        results.Add(InlineFromUser(github.User.Get(users.Items[i].Login).GetAwaiter().GetResult()));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                var result = new InlineQueryResultArticle(
                   "0", "🔴 " + ex.Message, new InputTextMessageContent(ex.ToString()));

                results.Add(result);
            }

            botClient.AnswerInlineQueryAsync(e.InlineQuery.Id, results);
        }
    }
}
