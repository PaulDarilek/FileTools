using FileTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FolderClone
{
    internal static class Program
    {
        const char KeyValueSeparator = '=';

        private static readonly string[] _ActionNames = Enum.GetNames<FileCompareAction>();
        private static readonly Dictionary<string, int> _EventStats =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static string Source { get; set; }
        private static string Target { get; set; }

        internal static void Main(string[] args)
        {
            var cloner = new FolderCompareProcessor(new FileCompareProcessor())
            {
                OnActivityResult = DisplayEvent,
                OnException = (activity, ex) => Console.Error.WriteLine($"Error processing {activity.Action}: {ex.Message}"),
            };

            if (!ConfigureCloner(cloner, args, out bool clean))
            {
                ShowHelp();
                return;
            }

            cloner.MatchTargetFilesAsync(Source, Target).GetAwaiter().GetResult();
            ShowStats();
            if (clean)
            {
                CleanEmptyFolders(new DirectoryInfo(Source), new DirectoryInfo(Target));
            }
        }

        private static void DisplayEvent(FileCompareActivity activity, bool result)
        {
            // Count events.
            var key = (result ? "" : "!") + activity.Action.ToString();
            if (_EventStats.ContainsKey(key))
                _EventStats[key] += 1;
            else
                _EventStats[key] = 1;

            switch (activity.Action)
            {
                case FileCompareAction.FindTarget:
                    Console.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.CompareLengths:
                    Console.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.CompareBytes:
                    Console.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.WriteTarget:
                    Console.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.WriteSource:
                    Console.WriteLine($"{activity.Action}: {activity.Source.FullName} ({result})");
                    break;

                case FileCompareAction.DeleteTarget:
                    Console.WriteLine($"{activity.Action}: {activity.Target.FullName} ({result})");
                    break;

                case FileCompareAction.DeleteSource:
                    Console.WriteLine($"{activity.Action}: {activity.Source.FullName} ({result})");
                    break;

                default:
                    break;
            }
        }

        private static void ShowStats()
        {
            if (_EventStats.Any())
            {
                Console.WriteLine("Event Counts:");
                foreach (var pair in _EventStats)
                {
                    Console.WriteLine($"\t {pair.Key} = {pair.Value}");
                }
            }
            else
            {
                Console.WriteLine("No Files Processed!");
            }
        }

        private static void CleanEmptyFolders(DirectoryInfo sourceFolder, DirectoryInfo targetFolder)
        {
            Console.Write($"Folder: {sourceFolder} cleaning empty folders: ");
            var count = sourceFolder.RemoveEmptyFolders();
            Console.WriteLine($"{count} folders removed.");

            Console.Write($"Folder: {targetFolder} cleaning empty folders: ");
            count = targetFolder.RemoveEmptyFolders();
            Console.WriteLine($"{count} folders removed.");
        }

        private static void ShowHelp()
        {
            Console.WriteLine($"Usage:");
            Console.Write($"FolderClone");
            Console.Write($" {nameof(Source)}{KeyValueSeparator}sourceFolder");
            Console.Write($" {nameof(Target)}{KeyValueSeparator}destinationFolder");
            Console.WriteLine($" [!]Action{KeyValueSeparator}NextAction");

            Console.WriteLine($"Where:");
            Console.WriteLine($"  Action is one of ({string.Join(", ", _ActionNames)})");
            Console.WriteLine($"  NextAction is the next action to take if Action returns true.");
            Console.WriteLine($"  [!] indicates an optional ! (exclamation)  prefix indicates the NextAction is used when Action returns false. (Don't use square brackets).");
            Console.WriteLine($"  Repeat [!]Action{KeyValueSeparator}NextAction as needed.");

            Console.WriteLine();
            Console.WriteLine($"Switches:  (Optional)");
            Console.WriteLine($"  [-?|-h|--help]\t Display Help.");
            Console.WriteLine($"  [-i|--interactive]\t InterActive (Prompt for Options).");
            Console.WriteLine($"  [-r|-s|--recurse]\t Recurse Subdirectories");
            Console.WriteLine($"  [-a|--archive]\t Only Files with Archive bit set");
            Console.WriteLine($"  [-c|--clean]\t Remove Empty Folders from Source and Target.");

            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.Write($"Source{KeyValueSeparator}{Path.GetFullPath("/Data")} ");
            Console.Write($"Target{KeyValueSeparator}{Path.GetFullPath("/Backup")} ");
            Console.Write($"--recurse ");
            Console.Write($"!{nameof(FileCompareAction.FindTarget)}{KeyValueSeparator}{nameof(FileCompareAction.WriteTarget)} ");
            Console.Write($"{nameof(FileCompareAction.FindTarget)}{KeyValueSeparator}{nameof(FileCompareAction.CompareLengths)} ");
            Console.Write($"!{nameof(FileCompareAction.CompareLengths)}{KeyValueSeparator}{nameof(FileCompareAction.WriteTarget)} ");
            Console.WriteLine();
        }

        /// <summary>Parse and configure the FolderProcessor.</summary>
        /// <returns>true if ready to go. False if Help should be displayed.</returns>
        private static bool ConfigureCloner(FolderCompareProcessor cloner, string[] args, out bool clean)
        {
            clean = args.HasSwitch("-c", "--clean");

            if (args.HasSwitch("-h", "-?", "--help"))
            {
                // Returns False to Show Help or Wrong Input.
                return false;
            }

            cloner.Recurse = args.HasSwitch("-r", "--recurse");
            cloner.ArchiveAttributeOnly = args.HasSwitch("-a", "--archive");

            bool isInteractive = args.HasSwitch("-i", "--interactive");

            var keyValues = args.ParseKeyValue(KeyValueSeparator);

            if (keyValues.ContainsKey(nameof(Source)))
            {
                Source = keyValues[nameof(Source)];
                keyValues.Remove(nameof(Source));
            }
            else if (isInteractive)
            {
                Console.Write("Enter Source Path:");
                Source = Console.ReadLine();
            }
            if (string.IsNullOrEmpty(Source))
            {
                Console.WriteLine($"{nameof(Source)} is Required.");
                return false;
            }

            if (keyValues.ContainsKey(nameof(Target)))
            {
                Target = keyValues[nameof(Target)];
                keyValues.Remove(nameof(Target));
            }
            else if (isInteractive)
            {
                Console.Write("Enter Target Path:");
                Target = Console.ReadLine();
            }
            if (string.IsNullOrWhiteSpace(Target))
            {
                Console.WriteLine($"{nameof(Target)} is Required.");
                return false;
            }

            foreach (var pair in keyValues)
            {
                string actionName = pair.Key;
                bool isBang = actionName?.StartsWith('!') ?? false;
                if (isBang)
                    actionName = actionName[1..];

                if (!TryParseAction(actionName, out FileCompareAction? forAction))
                    return false;
                if (forAction.HasValue)
                {
                    // next action when true (before colon)
                    actionName = pair.Value?.Trim() ?? string.Empty;

                    if (!TryParseAction(actionName, out FileCompareAction? nextAction))
                        return false;

                    cloner.SetNextAction(forAction.Value, !isBang, nextAction);
                }
            }

            if (isInteractive)
            {
                Console.WriteLine($"{nameof(Source)}{KeyValueSeparator}{Source}");
                Console.WriteLine($"{nameof(Target)}{KeyValueSeparator}{Target}");

                if (!cloner.Recurse)
                {
                    Console.Write($"Use --recurse [Y|N]? ");
                    char yn = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    cloner.Recurse = yn == 'y' || yn == 'Y';
                }

                if (!cloner.ArchiveAttributeOnly)
                {
                    Console.Write($"Use --archive [Y|N]? ");
                    char yn = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    cloner.ArchiveAttributeOnly = yn == 'y' || yn == 'Y';
                }

                if(!clean)
                {
                    Console.Write($"Use --clean [Y|N]? ");
                    char yn = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    clean = yn == 'y' || yn == 'Y';
                }

                var allActions = Enum.GetValues<FileCompareAction>();

                foreach (var action in allActions)
                {
                    Prompt(cloner, action, true);
                    Prompt(cloner, action, false);
                }
            }

            return true;

            static void Prompt(FolderCompareProcessor cloner, FileCompareAction action, bool actionResult)
            {
                var options = _ActionNames.Where(key => !key.Equals(action.ToString(), StringComparison.CurrentCultureIgnoreCase));
                if (actionResult)
                {
                    Console.WriteLine($"Options for {action} are: {string.Join(',', options)}");
                }
                if( cloner.GetNextAction(action, actionResult).HasValue)
                {
                    // existing value... skip!
                    return;
                }

                var bang = actionResult ? "" : "!";
                Console.Write($"{bang}{action}{KeyValueSeparator}");
                string actionName = Console.ReadLine();
                if (TryParseAction(actionName, out FileCompareAction? nextAction))
                {
                    cloner.SetNextAction(action, actionResult, nextAction);
                }
            }

            static bool TryParseAction(string actionName, out FileCompareAction? action)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    action = null;
                    return true;
                }

                if (Enum.TryParse<FileCompareAction>(actionName, out FileCompareAction parsed))
                {
                    action = parsed;
                    return true;
                }

                Console.WriteLine($"Invalid Action Name: {actionName}");
                action = null;
                return false;
            }
        }

    }
}
