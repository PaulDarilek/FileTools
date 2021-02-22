using FileTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FolderClone
{
    internal static class Program
    {
        private static readonly Dictionary<string, FileCompareDelegate> _AllHandlers = FolderProcessor.GetHandlerDictionary();
        private static readonly Dictionary<string, int> _EventStats =
            new Dictionary<string, int>(
                Enum.GetNames(typeof(FileCompareEvent))
                .Where(name => name != nameof(FileCompareEvent.None))
                .Select(name => new KeyValuePair<string, int>(name, 0)),
                StringComparer.OrdinalIgnoreCase);

        internal static void Main(string[] args)
        {
            var cloner = ConfigureCloner(args, out string destinationFolder, out bool clean);

            cloner.MatchFilesAsync(destinationFolder).GetAwaiter().GetResult();
            ShowStats();
            if(clean)
            {
                CleanEmptyFolders(cloner.SourceFolder, new DirectoryInfo(destinationFolder));
            }
        }

        private static void ShowStats()
        {
            if (_EventStats.Where(kv => kv.Value != 0).Any())
            {
                Console.WriteLine("Event Counts:");
                foreach (var pair in _EventStats.Where(kv => kv.Value != 0))
                {
                    Console.WriteLine($"\t {pair.Key} = {pair.Value}");
                }
            }
        }

        private static void CleanEmptyFolders(DirectoryInfo sourceFolder, DirectoryInfo targetFolder)
        {
            if (_EventStats[nameof(FileCompareEvent.SourceDeleted)] > 0)
            {
                Console.Write($"Folder: {sourceFolder} cleaning empty folders: ");
                var count = sourceFolder.RemoveEmptyFolders();
                Console.WriteLine($"{count} folders removed.");
            }

            if (_EventStats[nameof(FileCompareEvent.TargetDeleted)] > 0)
            {
                Console.Write($"Folder: {targetFolder} cleaning empty folders: ");
                var count = targetFolder.RemoveEmptyFolders();
                Console.WriteLine($"{count} folders removed.");
            }
        }

        private static FolderProcessor ConfigureCloner(string[] args, out string destinationFolder, out bool clean)
        {
            var argList = args.ToList();

            if (argList.Count < 2 || HasSwitch("-?", "--help"))
            {
                ShowHelp();
                destinationFolder = null;
                clean = false;
                return null;
            }

            string sourceFolder = NextParm(true, nameof(sourceFolder));
            destinationFolder = NextParm(true, nameof(destinationFolder));
            bool recurse = HasSwitch("-r", "--recurse");
            bool skipHidden = HasSwitch("-h", "--hidden");
            clean = HasSwitch("-c", "--clean");

            var cloner = new FolderProcessor(sourceFolder)
            {
                OnEventRaised = DisplayEvent,
                OnException = (evt, source, dest, ex) => Console.Error.WriteLine($"Error processing {evt}: {ex.Message}, {source.FullName} {dest.FullName}"),
                Recurse = recurse,
                FileFilter = skipHidden ? FilterHiddenFile : null,
            };

            for (int i = 0; i < argList.Count; i++)
            {
                var arg = argList[i];
                bool isValid = !string.IsNullOrWhiteSpace(arg) && arg.StartsWith("--") && arg.Contains("=");
                if (isValid)
                {
                    var index = arg.IndexOf('=');
                    string eventName = arg[2..index].Trim();
                    string handlerName = arg[(index + 1)..].Trim();
                    isValid = _AllHandlers.ContainsKey(handlerName) && _EventStats.ContainsKey(eventName);
                    if (isValid)
                    {
                        FileCompareEvent eventKey = (FileCompareEvent)Enum.Parse(typeof(FileCompareEvent), eventName);
                        cloner.WithHandler(eventKey, _AllHandlers[handlerName]);
                    }
                }
                if (!isValid)
                {
                    Console.Error.WriteLine($"Error, Unknown Command: {arg}");
                    ShowHelp();
                }
            }

            if(cloner.Handlers.Count == 0)
            {
                Console.WriteLine($"No Handlers... Here is a recommendation:");
                Console.WriteLine($"\t--{nameof(FileCompareEvent.TargetNotFound)}={nameof(FolderProcessor.CopySourceToTarget)}");
                Console.Write("Add recommended handler? (Press Y or N)");
                var key = Console.ReadKey();
                Console.WriteLine();
                if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                    cloner.WithHandler(FileCompareEvent.TargetNotFound, FolderProcessor.CopySourceToTarget);
            }

            return cloner;

            // test for a switch being set (false if not set).
            bool HasSwitch(params string[] switches)
            {
                bool found = false;
                foreach (var switchPattern in switches)
                {
                    if (argList.Count == 0)
                        return found;

                    var value = argList.FirstOrDefault(arg => arg.Equals(switchPattern, StringComparison.OrdinalIgnoreCase));
                    if (value != null)
                    {
                        found = true;
                        argList.Remove(value);
                    }
                }
                return found;
            }

            // Get next parameter, then remove from the list.
            string NextParm(bool required, string parameterName)
            {
                string arg = null;
                if (argList.Count > 0)
                {
                    arg = argList[0];
                    argList.RemoveAt(0);
                }
                else
                {
                    if (required)
                        throw new ArgumentNullException(parameterName);
                }
                return arg;
            }

        }

        private static void ShowHelp()
        {
            Console.WriteLine($"FolderClone sourceFolder destinationFolder --DeleteSource");
            Console.WriteLine($"\t Where:");
            Console.WriteLine($"\t   sourceFolder is the Source to copy From.");
            Console.WriteLine($"\t   destinationFolder is the target folder to Copy (if needed).");
            Console.WriteLine($"\t   -? or --help is optional parameter to display this help.");
            Console.WriteLine($"\t   -r or --recurse enables searching subdirectories.");
            Console.WriteLine($"\t   -h or --hidden filters out hidden files from being processed.");
            Console.WriteLine($"\t   --EventName=HandlerName");
            Console.WriteLine($"\t   EventNames: ({string.Join("|", _EventStats.Keys)})");
            Console.WriteLine($"\t   HandlerNames: ({string.Join("|", _AllHandlers.Keys)})");

            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine($"\t--{nameof(FileCompareEvent.TargetNotFound)}={nameof(FolderProcessor.CopySourceToTarget)}");
            Console.WriteLine($"\t--{nameof(FileCompareEvent.TargetFound)}={nameof(FolderProcessor.CompareFileLength)}");
            Console.WriteLine($"\t--{nameof(FileCompareEvent.LengthNotEqual)}={nameof(FolderProcessor.CopySourceToTarget)}");

            System.Environment.Exit(-1);
        }

        private static void DisplayEvent(FileCompareEvent fileEvent, FileInfo source, FileInfo target)
        {
            // Count events.
            _EventStats[fileEvent.ToString()] += 1;

            switch (fileEvent)
            {
                case FileCompareEvent.TargetFound:
                    Console.WriteLine($"{fileEvent}: {target.FullName} ({target.Length} bytes)");
                    break;
                
                case FileCompareEvent.TargetNotFound:
                    Console.WriteLine($"{fileEvent}: {target.FullName}\n\t(Source: {source.FullName} {source.Length} bytes)");
                    break;
                
                case FileCompareEvent.TargetWritten:
                    Console.WriteLine($"\t{fileEvent}: {target.Name} ({target.Length} bytes)");
                    break;
                
                case FileCompareEvent.TargetDeleted:
                    Console.WriteLine($"\t{fileEvent}: {target.Name}");
                    break;

                case FileCompareEvent.SourceWritten:
                    Console.WriteLine($"\t{fileEvent}: {source.Name} ({source.Length} bytes)");
                    break;
                case FileCompareEvent.SourceDeleted:
                    Console.WriteLine($"\t{fileEvent}: {source.FullName}");
                    break;
                
                case FileCompareEvent.LengthEqual:
                case FileCompareEvent.LengthNotEqual:
                    Console.WriteLine($"\t{fileEvent}: {source.Name} ({source.Length} {target.Length} bytes)");
                    break;
                
                case FileCompareEvent.BytesEqual:
                case FileCompareEvent.BytesNotEqual:
                    Console.WriteLine($"\t{fileEvent}: {source.Name} ({source.Length} {target.Length} bytes)");
                    break;
                
                default:
                    Console.WriteLine($"\t{fileEvent}:{source.FullName}\n\t{target.FullName}");
                    break;
            }
        }

        private static bool FilterHiddenFile(this FileInfo file) => !file.Attributes.HasFlag(FileAttributes.Hidden);
    }
}
