using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FileTools.Parsing
{

    [DebuggerStepThrough()]
    public static class CommandParser
    {
        public static IDictionary<string, string> ParseKeyValue(this IEnumerable<string> args, char separator, bool ignoreCase = true)
        {
            var dict = new Dictionary<string, string>(args.Count(), ignoreCase ? StringComparer.CurrentCulture : StringComparer.CurrentCultureIgnoreCase);
            foreach (var arg in args)
            {
                if (arg.Contains("="))
                {
                    var index = arg.IndexOf(separator);
                    string key = arg[..index]?.Trim() ?? string.Empty;
                    string value = arg[(index + 1)..]?.Trim() ?? string.Empty;
                    dict.Add(key, value);
                }
            }
            return dict;
        }

        public static IEnumerable<string> GetSwitchOptions(string switchName, char? extra = null)
        {
            yield return "-" + char.ToLower(switchName[0]);
            if (extra.HasValue)
                yield return "-" + extra;
            yield return "--" + switchName;
        }

        [DebuggerStepThrough()]
        public static bool HasSwitch(this IEnumerable<string> args, string switchName, char? extra = null)
        {
            var switches = GetSwitchOptions(switchName, extra);
            bool found = args.Any() && switches.Any(value => !string.IsNullOrWhiteSpace(value) && args.Contains(value, StringComparer.CurrentCultureIgnoreCase));
            return found;
        }

        public static bool HasSwitch(this IEnumerable<string> args, StringComparer comparer, params string[] switches)
        {
            bool found = args.Any() && switches.Any(value => !string.IsNullOrWhiteSpace(value) && args.Contains(value, comparer));
            return found;
        }

        /// <summary>Return first Enum that is a match by by full Name, or partial name, or Initials of the Capitalized letters.</summary>
        /// <returns>First match, or Null if nothing matches</returns>
        /// <example>
        ///   enum MyEnum{ReadOnly, Hidden, System, ReparsePoint}
        ///   var list = Enum.GetNames<MyEnum>();
        ///   list.FirstMatch("ReadOnly") would return MyEnum.ReadOnly as a full match.
        ///   list.FirstMatch("H") would return MyEnum.Hidden as a match starting with 'H'.
        ///   list.FirstMatch("RP") would return MyEnum.ReparsePoint as a match of the capitalized initials.
        ///   list.FirstMatch("R") would return math the starting 'R' of MyEnum.ReadOnly and MyEnum.ReparsePoint and return the first one found.
        ///   list.FirstMatch("XP") would not match on full Name, start of Name, nor the capitalized initials and would return null.
        /// </example>
        public static TEnum? FindFirst<TEnum>(this IEnumerable<TEnum> options, string name) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            return
                options.FindFirstByName(name) ??
                options.FindFirstByNameStartsWith(name) ??
                options.FindFirstByInitials(name);
        }

        /// <summary>Find first enum name that matches a string (case insensitive).</summary>
        public static TEnum? FindFirstByName<TEnum>(this IEnumerable<TEnum> options, string name) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            foreach (var item in options)
            {
                if (item.ToString().Equals(name, StringComparison.CurrentCultureIgnoreCase))
                    return item;
            }
            return null;
        }

        /// <summary>Find first enum name that matches the start of a string (case insensitive).</summary>
        public static TEnum? FindFirstByNameStartsWith<TEnum>(this IEnumerable<TEnum> options, string name) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            foreach (var item in options)
            {
                if (item.ToString().StartsWith(name, StringComparison.CurrentCultureIgnoreCase))
                    return item;
            }
            return null;
        }

        public static TEnum? FindFirstByInitials<TEnum>(this IEnumerable<TEnum> options, string name) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            name = name.ToUpper();
            foreach (var item in options)
            {
                if (GetCapitalLetters(item.ToString()).Equals(name))
                    return item;
            }
            return null;
        }

        public static string GetCapitalLetters(string value) => FilteredChars(value, char.IsUpper);

        public static string FilteredChars(string value, Func<char, bool> filter)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return new string(value.Where(c => filter(c)).ToArray());
        }

    }
}
