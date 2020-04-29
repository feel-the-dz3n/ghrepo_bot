using Octokit;
using System;
using System.Collections.Generic;
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
            Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

            botClient.OnMessage += BotClient_OnMessage;
            botClient.OnInlineQuery += BotClient_OnInlineQuery;
            botClient.OnInlineResultChosen += BotClient_OnInlineResultChosen;
            botClient.StartReceiving();

            Thread.Sleep(-1);
        }

        private static void BotClient_OnInlineResultChosen(object sender, ChosenInlineResultEventArgs e)
        {
        }

        private static void BotClient_OnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            var results = new List<InlineQueryResultBase>();

            try
            {
                var q = QueryParser.Parse(e.InlineQuery.Query);
                if (q.Type == QueryType.Error) throw q.Exception;
                else if (q.Type == QueryType.NotFound) throw new Exception("🔴 Not found");
                else if (q.Type == QueryType.Wrong) throw new Exception("🔴 Something is wrong");
                else if (q.Type == QueryType.Repository)
                {
                    var repo = github.Repository.Get(q.Owner, q.Repository).GetAwaiter().GetResult();

                    var text = new StringBuilder();
                    text.AppendLine($"📄 {q.Repository} by {q.Owner}");
                    if (!string.IsNullOrWhiteSpace(repo.Description)) text.AppendLine(repo.Description);
                    text.AppendLine();
                    if (repo.License != null && !string.IsNullOrWhiteSpace(repo.License.Name))
                        text.AppendLine("📃 License: " + repo.License.Name);
                    text.AppendLine("⭐️ Stars: " + repo.StargazersCount);
                    text.AppendLine("👥 Forks: " + repo.ForksCount);
                    text.AppendLine();
                    text.AppendLine(repo.HtmlUrl);

                    results.Add(new InlineQueryResultArticle(
                        "0", $"📄 {q.Repository} by {q.Owner}",
                        new InputTextMessageContent(text.ToString())));
                }
                else if (q.Type == QueryType.Owner)
                {
                    var owner = github.User.Get(q.Owner).GetAwaiter().GetResult();

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
                    if(owner.PrivateGists != null) 
                        text.AppendLine(" - Private gists: " + owner.PrivateGists);
                    text.AppendLine();

                    text.AppendLine(owner.HtmlUrl);

                    results.Add(new InlineQueryResultArticle(
                        "0", $"👨‍💻 {owner.Login}{(string.IsNullOrWhiteSpace(owner.Name) ? "" : " | " + owner.Name)}",
                        new InputTextMessageContent(text.ToString())));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                var result = new InlineQueryResultArticle(
                   "0", ex.Message, new InputTextMessageContent(ex.ToString()));

                results.Add(result);
            }

            botClient.AnswerInlineQueryAsync(e.InlineQuery.Id, results);
        }

        private static void BotClient_OnMessage(object sender, MessageEventArgs e)
        {
        }
    }
}
