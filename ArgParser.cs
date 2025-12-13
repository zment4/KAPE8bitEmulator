using System;
using System.Collections.Generic;

namespace KAPE8bitEmulator
{
    public class ArgParseResult
    {
        public Dictionary<string, string> Switches { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> Positionals { get; } = new List<string>();
        public string FileName { get; set; }
    }

    public static class ArgParser
    {
        // Parse args supporting --flag, --key value, --run <file> and plain positional filename anywhere
        public static ArgParseResult Parse(string[] args)
        {
            var res = new ArgParseResult();

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (string.IsNullOrEmpty(a)) continue;

                if (a.StartsWith("--"))
                {
                    var key = a.Substring(2);
                    // If next token looks like a value (not starting with --), consume it
                    string val = "true";
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        val = args[i + 1];
                        i++; // consume
                    }

                    res.Switches[key] = val;
                }
                else
                {
                    // positional
                    res.Positionals.Add(a);
                }
            }

            // Determine filename precedence: --run (explicit) > first positional
            if (res.Switches.TryGetValue("run", out var runVal) && !string.Equals(runVal, "true", StringComparison.OrdinalIgnoreCase))
            {
                res.FileName = runVal;
            }
            else if (res.Positionals.Count > 0)
            {
                res.FileName = res.Positionals[0];
            }

            return res;
        }
    }
}
