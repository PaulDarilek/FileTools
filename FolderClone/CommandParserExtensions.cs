using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FolderClone
{
    internal static class CommandParserExtensions
    {
        [DebuggerStepThrough]
        public static IDictionary<string, string> ParseKeyValue(this IEnumerable<string> args, char separator, bool ignoreCase = true)
        {
            var dict = new Dictionary<string, string>(args.Count(), ignoreCase ? StringComparer.CurrentCulture : StringComparer.CurrentCultureIgnoreCase);
            foreach (var arg in args)
            {
                if(arg.Contains("="))
                {
                    var index = arg.IndexOf(separator);
                    string key = arg[..index]?.Trim() ?? string.Empty;
                    string value = arg[(index + 1)..]?.Trim() ?? string.Empty;
                    dict.Add(key, value);
                }
            }
            return dict;
        }

        [DebuggerStepThrough]
        public static bool HasSwitch(this IEnumerable<string> args, params string[] switches)
        {
            bool found = args.Any() && switches.Any(value => !string.IsNullOrWhiteSpace(value) && args.Contains(value, StringComparer.CurrentCultureIgnoreCase));
            return found;
        }

        [DebuggerStepThrough]
        public static bool HasSwitch(this IEnumerable<string> args, StringComparer comparer, params string[] switches)
        {
            bool found = args.Any() && switches.Any(value => !string.IsNullOrWhiteSpace(value) && args.Contains(value, comparer));
            return found;
        }


    }
}
