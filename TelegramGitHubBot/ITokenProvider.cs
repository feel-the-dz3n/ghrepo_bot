using System;
using System.Collections.Generic;
using System.Text;

namespace TelegramGitHubBot
{
    public interface ITokenProvider
    {
        string Get();
    }
}
