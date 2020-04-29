using System;
using System.Collections.Generic;
using System.Text;

namespace TelegramGitHubBot
{
    public class QueryParser
    {
        public static QueryInfo Parse(string input)
        {
            var result = new QueryInfo();

            try
            {
                if (string.IsNullOrWhiteSpace(input))
                    throw new Exception("Enter login or owner/repository!");

                if (input.StartsWith("search"))
                {
                    if (input.StartsWith("search user"))
                    {
                        result.Type = QueryType.SearchUser;
                        result.Owner = input.Substring("search user".Length).Trim();
                    }
                    else if (input.StartsWith("search repo"))
                    {
                        result.Type = QueryType.SearchRepo;
                        result.Repository = input.Substring("search repo".Length).Trim();
                    }
                    else throw new Exception("Use: search repo | search user");
                }
                else
                {
                    if (input.Contains("/"))
                    {
                        var qArgs = input.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                        result.Type = QueryType.Repository;
                        result.Owner = qArgs[0].Trim();
                        result.Repository = qArgs[1].Trim();
                    }
                    else // probably owner
                    {
                        result.Type = QueryType.SearchOrOwner;
                        result.Owner = input.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Type = QueryType.Exception;
                result.Exception = ex;
            }

            return result;
        }
    }
}
