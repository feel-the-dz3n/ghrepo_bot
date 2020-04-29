using System;
using System.Collections.Generic;
using System.Text;

namespace TelegramGitHubBot
{
    public class Limits
    {
        public static int CleanSearchLimit = 6;
        public static int HybridSearchLimit => CleanSearchLimit / 2;
    }
}
