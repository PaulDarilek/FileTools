using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileTools
{
    /// <summary>Signature of the Handlers used in Folder comparisons when matching files.</summary>
    /// <returns>An Event indicating a change in file system state.</returns>
    public delegate Task<bool> FileCompareHandler(FileCompareActivity compareActivity);

    public class FolderCompareProcessor
    {
        private static readonly int MaxEventCount = Enum.GetValues(typeof(FileCompareAction)).Length;

        /// <summary>File Compare Result recieved. (Display/Log)</summary>
        public Action<FileCompareActivity, bool> OnActivityResult { get; set; }

        public Action<FileCompareActivity, Exception> OnException { get; set; }
        public Action<FileInfo> OnIgnoreFile { get; set; }

        /// <summary>If True will recurse through subdirectories</summary>
        public bool? Recurse { get; set; }

        /// <summary>File Filter Predicate.  Returns True for files to Process.</summary>
        /// <remarks>Null implies no filter.</remarks>
        public Func<FileInfo, bool> FileFilter { get; set; }

        public Dictionary<FileAttributes, bool?> AttributeFilters { get; }

        public int ActionCount => _NextActionWhenTrue.Count + _NextActionWhenFalse.Count;

        public FileCompareAction? GetNextAction(FileCompareAction current, bool currentResult)
        {
            var dict = currentResult ? _NextActionWhenTrue : _NextActionWhenFalse;
            FileCompareAction? value = dict.ContainsKey(current) ? dict[current] : null;
            return value;
        }
        public void SetNextAction(FileCompareAction current, bool currentResult, FileCompareAction? next)
        {
            var dict = currentResult ? _NextActionWhenTrue : _NextActionWhenFalse;
            if(next.HasValue)
            {
                dict[current] = next.Value;
            }
            else
            {
                dict.Remove(current);
            }
        }

        private IFileCompareProcessor Processor { get; }
        private readonly Dictionary<FileCompareAction, FileCompareAction> _NextActionWhenTrue = new Dictionary<FileCompareAction, FileCompareAction>(MaxEventCount);
        private readonly Dictionary<FileCompareAction, FileCompareAction> _NextActionWhenFalse = new Dictionary<FileCompareAction, FileCompareAction>(MaxEventCount);

        /// <summary>Constructor</summary>
        public FolderCompareProcessor(FileCompareProcessor compareProcessor)
        {
            Processor = compareProcessor;
            AttributeFilters = new Dictionary<FileAttributes, bool?>(GetSupportedAttributes().Select(attrib => new KeyValuePair<FileAttributes, bool?>(attrib, (bool?)null)));
        }

        /// <summary>Process each file in folder, and set up a comparison to matching relative path in another folder</summary>
        /// <param name="destFolder">Folder to Compare or Copy to.</param>
        /// <param name="recurse">True to process subdirectories</param>
        /// <param name="filePattern">File pattern to match on. (Regular Expressions not valid)</param>
        /// <param name="FileFilter">Optional Filter for Source files</param>
        public async Task MatchTargetFilesAsync(string sourceFolder, string destFolder, string filePattern = "*")
        {
            await MatchTargetFilesAsync(new DirectoryInfo(sourceFolder), new DirectoryInfo(destFolder), filePattern);
        }

        /// <summary>Process each file in folder, and set up a comparison to matching relative path in another folder</summary>
        /// <param name="targetFolder">Folder to Compare or Copy to.</param>
        /// <param name="Recurse">True to process subdirectories</param>
        /// <param name="filePattern">File pattern to match on. (Regular Expressions not valid)</param>
        /// <param name="FileFilter">Optional Filter for Source files</param>
        public async Task MatchTargetFilesAsync(DirectoryInfo sourceFolder, DirectoryInfo targetFolder, string filePattern = "*")
        {
            if (!sourceFolder.Exists)
            {
                throw new DirectoryNotFoundException(sourceFolder.FullName);
            }
            if (!targetFolder.Exists)
            {
                targetFolder.Create();
            }
            if (sourceFolder.FullName.Equals(targetFolder.FullName, StringComparison.CurrentCultureIgnoreCase))
                throw new ArgumentException("Source and Target paths must be different");

            string sourcePrefix = FolderPath(sourceFolder);
            string destPrefix = FolderPath(targetFolder);

            SearchOption recurseOption =
                Recurse == true ?
                SearchOption.AllDirectories :
                SearchOption.TopDirectoryOnly;

            var attribFilter = AttributeFilters.Where(x => x.Value.HasValue).ToArray();

            var files = sourceFolder.EnumerateFiles(filePattern ?? "*", recurseOption);
            foreach (var sourceFile in files)
            {
                // option to filter to only files with Archive Attribute Set.
                bool includeFile = 
                    attribFilter.All(item => item.Value.Value == sourceFile.Attributes.HasFlag(item.Key)) &&
                    (FileFilter == null || FileFilter(sourceFile));

                if (includeFile)
                {
                    string relativeFilePath = sourceFile.FullName[sourcePrefix.Length..];
                    FileInfo targetFile = new FileInfo(Path.Combine(destPrefix, relativeFilePath));

                    // initial Result;
                    FileCompareActivity activity = new FileCompareActivity(FileCompareAction.FindTarget, sourceFile, targetFile);

                    try
                    {
                        // while current activity has not already been done for this file.
                        while (activity.GetResult() == null)
                        {
                            // Execute the Method.
                            var actionResult = await Processor.ProcessActivityAsync(activity);

                            // update result of the Method.
                            activity.SetResult(actionResult);

                            // Pre-Event may output trace info (console, etc).
                            OnActivityResult?.Invoke(activity, actionResult);

                            // Figure out next action based on Result.
                            FileCompareAction? next = GetNextAction(activity.Action, actionResult);

                            if (next.HasValue)
                            {
                                activity.Action = next.Value;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        OnException?.Invoke(activity, ex);
                    }
                }
                else
                {
                    OnIgnoreFile?.Invoke(sourceFile);
                }
            }

            [DebuggerStepThrough()]
            static string FolderPath(DirectoryInfo info) =>
                info.FullName.EndsWith(Path.DirectorySeparatorChar) ? info.FullName :
                info.FullName.EndsWith(Path.AltDirectorySeparatorChar) ? info.FullName :
                info.FullName + Path.DirectorySeparatorChar;
        }


        public static IEnumerable<FileAttributes> GetSupportedAttributes()
        {
            yield return FileAttributes.Archive;
            yield return FileAttributes.ReadOnly;
            yield return FileAttributes.Hidden;
            yield return FileAttributes.Compressed;
            yield return FileAttributes.NotContentIndexed;
            yield return FileAttributes.Encrypted;
            yield return FileAttributes.Offline;
            yield return FileAttributes.Temporary;
            yield return FileAttributes.ReparsePoint;    // RoboCopy does not allow in /xa: or /ia: 
            yield return FileAttributes.System;

            // yield return FileAttributes.Directory;       // We are scanning files or folders specifically.
            // yield return FileAttributes.Normal;          // Not important to check.
            // yield return FileAttributes.SparseFile;      // Not important to check.
            // yield return FileAttributes.IntegrityStream; // Ignore ReFS feature.
            // yield return FileAttributes.NoScrubData;     // Ignore ReFS feature.
            // yield return FileAttributes.Device;          // Device is not valid.
        }

    }

}
