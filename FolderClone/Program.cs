using FileTools;
using FileTools.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FolderClone
{
    internal static class Program
    {
        #region fields and properties
        
        private const char _KeyValueSeparator = '=';

        private static readonly FileAttributes[] _ValidAttribs = FolderCompareProcessor.GetSupportedAttributes().ToArray();
        private static readonly FileCompareAction[] _ValidActions = Enum.GetValues<FileCompareAction>();
        private static readonly Dictionary<string, int> _EventStats =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static string Source { get; set; }
        private static string Target { get; set; }
        private static TextWriter LogFile { get; set; }
        private static TextWriter Error { get; set; }
        private static TextWriter Display => Console.Out;
        private static TextReader Keyboard => Console.In;

        private static bool? Help { get; set; }
        private static bool? Clean { get; set; }
        private static bool? Prompt { get; set; }
        private static bool? Verbose { get; set; }

        private static readonly FolderCompareProcessor _Cloner =
            new FolderCompareProcessor(new FileCompareProcessor())
            {
                OnActivityResult = CountStats,
                OnException = OnException,
                OnIgnoreFile = OnIgnoreFile,
            };

        private static string Attributes
        {
            get
            {
                var attribList = _Cloner.AttributeFilters
                    .Where(x => x.Value.HasValue)
                    .Select(x => $"{(x.Value.Value ? "" : "!")}{x.Key}");

                return string.Join(",", attribList);
            }
        }
        
        #endregion

        internal static void Main(string[] args)
        {
            // default attributes to ignore:  
            // To ignore the attribute, use 
            // Example: /Attributes=?Sys,?Off,?Temp,?RP
            _Cloner.AttributeFilters[FileAttributes.System] = false;
            _Cloner.AttributeFilters[FileAttributes.Offline] = false;
            _Cloner.AttributeFilters[FileAttributes.Temporary] = false;
            _Cloner.AttributeFilters[FileAttributes.ReparsePoint] = false;

            LogFile = Console.Out;
            Error = Console.Error;

            // Configure from arguments / prompts.
            var keyValues = args.ParseKeyValue(_KeyValueSeparator);

            bool ok =
                SetSwitches(args) &&
                SetSource(keyValues) &&
                SetTarget(keyValues) &&
                SetAttributes(keyValues) &&
                SetActions(keyValues);

            if (ok && Prompt == true)
                PromptForInput();

            if (Help == true || Source.Equals(Target, StringComparison.CurrentCultureIgnoreCase))
            {
                ShowHelp();
                return;
            }

            if (Verbose == true)
                _Cloner.OnActivityResult = (a, b) =>
                {
                    CountStats(a, b);
                    DisplayVerboseActivities(a, b);
                };

            _Cloner.MatchTargetFilesAsync(Source, Target).GetAwaiter().GetResult();
            ShowStats();
            if (Clean == true)
            {
                CleanEmptyFolders(new DirectoryInfo(Source), new DirectoryInfo(Target));
            }
        }

        #region private methods

        private static void OnIgnoreFile(FileInfo file)
        {
            if (Verbose == true)
                LogFile.WriteLine($"Ignore: {file.FullName} ({file.Attributes})");
        }

        private static void OnException(FileCompareActivity activity, Exception ex)
        {
            Error.WriteLine($"Error processing {activity.Action}: {ex.Message}");
        }

        private static void CountStats(FileCompareActivity activity, bool result)
        {
            // Count events.
            var key = (result ? "" : "!") + activity.Action.ToString();
            if (_EventStats.ContainsKey(key))
                _EventStats[key] += 1;
            else
                _EventStats[key] = 1;
        }

        private static void DisplayVerboseActivities(FileCompareActivity activity, bool result)
        {
            switch (activity.Action)
            {
                case FileCompareAction.FindTarget:
                    LogFile.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.CompareLengths:
                    LogFile.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.CompareBytes:
                    LogFile.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.WriteTarget:
                    LogFile.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.WriteSource:
                    LogFile.WriteLine($"{activity.Action}: {activity.Source.FullName} ({result})");
                    break;

                case FileCompareAction.DeleteTarget:
                    LogFile.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.DeleteSource:
                    LogFile.WriteLine($"{activity.Action}: {activity.Source.FullName} ({result})");
                    break;

                default:
                    break;
            }
        }

        private static void ShowStats()
        {
            if (Verbose == false)
                return;

            if (_EventStats.Any())
            {
                LogFile.WriteLine("Event Counts:");
                foreach (var pair in _EventStats)
                {
                    LogFile.WriteLine($"\t {pair.Key} = {pair.Value}");
                }
            }
            else
            {
                LogFile.WriteLine("No Files Processed!");
            }
        }

        private static void ShowHelp()
        {
            Display.WriteLine($"Usage:");
            Display.Write($"FolderClone");
            Display.Write($" {nameof(Source)}{_KeyValueSeparator}sourceFolder");
            Display.Write($" {nameof(Target)}{_KeyValueSeparator}destinationFolder");
            Display.Write($" {nameof(Attributes)}{_KeyValueSeparator}A,!Sys,!Temp,!Offline,!RP ");
            Display.WriteLine($" [!]Action{_KeyValueSeparator}NextAction");

            Display.WriteLine($"{nameof(Attributes)}:");
            Display.WriteLine($" Optional comma separated list of attribute names, partial name, or Initials.");
            Display.WriteLine($" Allowed Attributes: {string.Join(",", _ValidAttribs)}");
            Display.WriteLine($" Included files must have Attribute set.");
            Display.WriteLine($" ! (Exclamation) prefix must have Attribute not set.");
            Display.WriteLine($" ? (Question) to Clear default flags:");
            Display.WriteLine();

            Display.WriteLine($"Action:");
            Display.WriteLine($"  Actions are any one of: ({string.Join(", ", Enum.GetNames<FileCompareAction>())})");
            Display.WriteLine($"  NextAction is the next action to take if Action returns true.");
            Display.WriteLine($"  [!] indicates an optional ! (exclamation)  prefix indicates the NextAction is used when Action returns false. (Don't use square brackets).");
            Display.WriteLine($"  Repeat [!]Action{_KeyValueSeparator}NextAction as needed.");

            Display.WriteLine();

            Display.WriteLine();
            Display.WriteLine($"Switches:  (Optional)");
            ShowSwitch(nameof(Help), "Display Help.");
            ShowSwitch(nameof(Prompt), "Prompt for Parameters.");
            ShowSwitch(nameof(_Cloner.Recurse), "Recurse through subdirectories.", 's');
            ShowSwitch(nameof(Clean), "Clean out empty folders.");
            ShowSwitch(nameof(Verbose), "Display Verbose output of files processed.");

            Display.WriteLine();
            Display.WriteLine("Example:");
            Display.Write($"Source{_KeyValueSeparator}{Path.GetFullPath("/Data")} ");
            Display.Write($"Target{_KeyValueSeparator}{Path.GetFullPath("/Backup")} ");
            Display.Write($"--{nameof(_Cloner.Recurse)} ");
            Display.Write($"!{nameof(FileCompareAction.FindTarget)}{_KeyValueSeparator}{nameof(FileCompareAction.WriteTarget)} ");
            Display.Write($"{nameof(FileCompareAction.FindTarget)}{_KeyValueSeparator}{nameof(FileCompareAction.CompareLengths)} ");
            Display.Write($"!{nameof(FileCompareAction.CompareLengths)}{_KeyValueSeparator}{nameof(FileCompareAction.WriteTarget)} ");
            Display.WriteLine();

            Display.WriteLine();

            static void ShowSwitch(string switchName, string description, char? extraOption = null)
            {
                var options = string.Join('|', CommandParser.GetSwitchOptions(switchName, extraOption));
                Display.WriteLine($"  [{options}]\t {description}");
            }
        }

        private static void CleanEmptyFolders(DirectoryInfo sourceFolder, DirectoryInfo targetFolder)
        {
            LogFile.Write($"Folder: {sourceFolder} cleaning empty folders: ");
            var count = sourceFolder.RemoveEmptyFolders();
            LogFile.WriteLine($"{count} folders removed.");

            LogFile.Write($"Folder: {targetFolder} cleaning empty folders: ");
            count = targetFolder.RemoveEmptyFolders();
            LogFile.WriteLine($"{count} folders removed.");
        }

        private static bool SetSwitches(string[] args)
        {
            if (args.HasSwitch(nameof(Help), '?'))
            {
                Help = true;
            }

            if (args.HasSwitch(nameof(Clean)))
            {
                Clean = true;
            }

            if (args.HasSwitch(nameof(Prompt)))
            {
                Prompt = true;
            }

            if (args.HasSwitch(nameof(Verbose)))
            {
                Verbose = true;
            }

            if (args.HasSwitch(nameof(_Cloner.Recurse), 's'))
                _Cloner.Recurse = true;

            return Help != true;
        }

        private static bool SetSource(IDictionary<string, string> keyValues)
        {
            // Input Source Folder:
            if (keyValues.ContainsKey(nameof(Source)))
            {
                Source = keyValues[nameof(Source)];
                keyValues.Remove(nameof(Source));
            }
            else if (Prompt == true)
            {
                Display.Write("Enter Source Path: ");
                Source = Keyboard.ReadLine();
            }
            if (string.IsNullOrEmpty(Source))
            {
                Error.WriteLine($"{nameof(Source)} is Required.");
                Help = true;
            }
            return Help != true;
        }

        private static bool SetTarget(IDictionary<string, string> keyValues)
        {
            // Input Target Folder:
            if (keyValues.ContainsKey(nameof(Target)))
            {
                Target = keyValues[nameof(Target)];
                keyValues.Remove(nameof(Target));
            }
            else if (Prompt == true)
            {
                Display.Write("Enter Target Path: ");
                Target = Keyboard.ReadLine();
            }
            if (string.IsNullOrWhiteSpace(Target))
            {
                Error.WriteLine($"{nameof(Target)} is Required.");
                Help = true;
            }
            return Help != true;
        }

        private static bool SetAttributes(IDictionary<string, string> keyValues)
        {
            var key = nameof(Attributes);
            if (keyValues.ContainsKey(key))
            {
                var values = keyValues[key];
                keyValues.Remove(key);

                foreach (var item in values.Split(','))
                {
                    bool? hasFlagSet = true;
                    var name = item.Trim();
                    if (name.StartsWith("!"))
                    {
                        name = name[1..];
                        hasFlagSet = false;
                    }
                    else if (name.StartsWith("?"))
                    {
                        name = name[1..];
                        hasFlagSet = null; // clear the flag.
                    }
                    var attribute = _ValidAttribs.FindFirst(name);
                    if (attribute != null)
                    {
                        _Cloner.AttributeFilters[attribute.Value] = hasFlagSet;
                    }
                    else
                    {
                        Error.WriteLine($"{name} does not match: {string.Join(", ", _ValidAttribs)}");
                        Help = true;
                    }
                }

            }

            if (!string.IsNullOrWhiteSpace(Attributes))
            {
                LogFile.WriteLine($"{nameof(Attributes)}{_KeyValueSeparator}{Attributes}");
            }

            return Help != true;
        }

        private static bool SetActions(IDictionary<string, string> keyValues)
        {
            foreach (var pair in keyValues)
            {
                string actionName = pair.Key?.Trim() ?? string.Empty;
                string nextActionName = pair.Value?.Trim() ?? string.Empty;

                bool isBang = false;
                if (actionName.StartsWith('!'))
                {
                    isBang = true;
                    actionName = actionName[1..];
                }

                var forAction = _ValidActions.FindFirst(actionName);
                var nextAction = _ValidActions.FindFirst(nextActionName);

                if (forAction == null || (nextAction == null && nextActionName.Length > 0))
                {
                    var or = nextActionName.Length > 0 ? " or " : "";
                    Error.WriteLine($"Unknown Action: {actionName}{or}{nextActionName}");
                    Help = true;
                    return false;
                }
                _Cloner.SetNextAction(forAction.Value, !isBang, nextAction);
            }
            return Help != true;
        }

        private static void PromptForInput()
        {
            Display.WriteLine($"{nameof(Source)}{_KeyValueSeparator}{Source}");
            Display.WriteLine($"{nameof(Target)}{_KeyValueSeparator}{Target}");

            if (PromptYesNo("Show Help Now"))
            {
                ShowHelp();
            }

            if (!_Cloner.Recurse.HasValue)
            {
                _Cloner.Recurse = PromptYesNo(nameof(_Cloner.Recurse));
            }

            if (!Clean.HasValue)
            {
                Clean = PromptYesNo(nameof(Clean));
            }

            if (!Verbose.HasValue)
            {
                Verbose = PromptYesNo(nameof(Verbose));
            }

            if(PromptYesNo("Setup Each Action"))
            {
                foreach (var action in _ValidActions)
                {
                    if (_Cloner.GetNextAction(action, true) == null || _Cloner.GetNextAction(action, false) == null)
                    {
                        var options = _ValidActions.Where(key => key != action).Select(key => key.ToString());
                        Display.WriteLine($"Options for {action} are: {string.Join(',', options)}");

                        PromptForAction(action, true);
                        PromptForAction(action, false);
                    }
                }
            }

            Display.WriteLine($"{nameof(Attributes)}{_KeyValueSeparator}{Attributes}");
            Display.WriteLine("Enter Additional Attributes (or leave blank to keep) and Press Enter: ");
            var newAttribs = Keyboard.ReadLine();
            if(!string.IsNullOrEmpty(newAttribs))
            {
                var dict = new Dictionary<string, string>();
                dict.Add(nameof(Attributes), newAttribs);
                SetAttributes(dict);
            }    

            // nested helper method(s)...
            static bool PromptYesNo(string prompt)
            {
                Display.Write($"{prompt} [Y|N]? ");
                bool? value = null;
                while (!value.HasValue)
                {
                    int readChar = Keyboard.Read();
                    char yn = Convert.ToChar(readChar);
                    value =
                        (yn == 'y' || yn == 'Y') ?
                        true :
                        (yn == 'n' || yn == 'n') ?
                        false :
                        null;
                }
                Display.WriteLine();
                return value.Value;
            }
        }

        private static void PromptForAction(FileCompareAction action, bool actionResult)
        {
            if (_Cloner.GetNextAction(action, actionResult).HasValue)
            {
                // already set... skip input.
                return;
            }

            var bang = actionResult ? "" : "!";
            Display.Write($"{bang}{action}{_KeyValueSeparator}");
            string actionName = Keyboard.ReadLine();
            var nextAction = _ValidActions.FindFirst(actionName);

            if (nextAction != null || string.IsNullOrWhiteSpace(actionName))
            {
                _Cloner.SetNextAction(action, actionResult, nextAction);
            }
            else
            {
                Error.WriteLine($"Invalid Action Name: {actionName}");
            }
        }

        #endregion
    }
}
