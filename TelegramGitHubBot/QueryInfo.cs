using System;
using System.Collections.Generic;
using System.Text;

namespace TelegramGitHubBot
{
    public class QueryInfo
    {
        public QueryType Type { get; set; }
        public Exception Exception { get; internal set; }
        public string Owner { get; internal set; }
        public string Repository { get; internal set; }

        public QueryInfo(QueryType type = QueryType.Exception)
        {
            this.Type = type;
        }
    }
}
