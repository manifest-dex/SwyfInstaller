using System;
using System.IO;

namespace Swyf.CustomAI
{
    public static class BackendConfig
    {
        public static bool Read(string path)
        {
            if (!File.Exists(path)) return false;
            bool? disabled = null;
            foreach (var raw in File.ReadAllText(path).TrimStart('\uFEFF').Split('\n'))
            {
                var line = raw.Split('#')[0].Trim();
                if (line.Length == 0) continue;
                var pair = line.Split('=');
                if (pair.Length != 2 || pair[0].Trim() != "disable_kolkata_api" || disabled.HasValue)
                    throw new FormatException("Expected one disable_kolkata_api = true or false setting.");
                var value = pair[1].Trim();
                if (value != "true" && value != "false")
                    throw new FormatException("disable_kolkata_api must be true or false.");
                disabled = value == "true";
            }
            return disabled ?? false;
        }
    }
}
