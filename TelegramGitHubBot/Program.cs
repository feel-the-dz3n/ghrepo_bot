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
            botClient = new TelegramBotClient(new FileTokenProvider(".token").Get());

            var me = botClient.GetMeAsync().Result;
            Console.WriteLine($"Working with {me.FirstName}.");

            botClient.OnInlineQuery += BotClient_OnInlineQuery;
            botClient.StartReceiving();

            Thread.Sleep(-1);
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
                else if (q.Type == QueryType.Owner)
                {
                    var owner = github.User.Get(q.Owner).GetAwaiter().GetResult();
                    results.Add(InlineFromUser(owner));
                }
                else if (q.Type == QueryType.SearchRepo)
                {
                    var repos = github.Search.SearchRepo(new SearchRepositoriesRequest(q.Repository)).GetAwaiter().GetResult();

                    if (repos.Items.Count <= 0) throw new Exception("No repos found");

                    for (int i = 0; i < repos.Items.Count && i < 5; i++)
                        results.Add(InlineFromRepo(github.Repository.Get(repos.Items[i].Id).GetAwaiter().GetResult()));
                }
                else if (q.Type == QueryType.SearchUser)
                {
                    var users = github.Search.SearchUsers(new SearchUsersRequest(q.Owner)).GetAwaiter().GetResult();

                    if (users.Items.Count <= 0) throw new Exception("No users found");

                    for (int i = 0; i < users.Items.Count && i < 5; i++)
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
