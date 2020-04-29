using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TelegramGitHubBot
{
    public class FileTokenProvider : ITokenProvider
    {
        public string FileName { get; set; }

        public string Get()
        {
            using (var s = new StreamReader(FileName))
                return s.ReadToEnd();
        }

        public FileTokenProvider(string fileName)
        {
            FileName = fileName;
        }
    }
}
