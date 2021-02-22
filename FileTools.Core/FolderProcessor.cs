using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileTools
{
    public class FolderProcessor
    {
        private static readonly int MaxEventCount = Enum.GetValues(typeof(FileCompareEvent)).Length;

        /// <summary>Folder used as Source</summary>
        public DirectoryInfo SourceFolder { get; set; }

        public Dictionary<FileCompareEvent, FileCompareDelegate> Handlers { get; }

        public Action<FileCompareEvent, FileInfo, FileInfo> OnEventRaised { get; set; }

        public Action<FileCompareEvent, FileInfo, FileInfo, Exception> OnException { get; set; }

        /// <summary>If True will recurse through subdirectories</summary>
        public bool Recurse { get; set; }

        /// <summary>File Filter Predicate.  Returns True for files to Process.</summary>
        /// <remarks>Null implies no filter.</remarks>
        public Func<FileInfo, bool> FileFilter { get; set; }

        public FolderProcessor(string sourcePath)
        {
            SourceFolder = new DirectoryInfo(sourcePath);
            Handlers = new Dictionary<FileCompareEvent, FileCompareDelegate>(MaxEventCount);
        }

        public FolderProcessor WithHandler(FileCompareEvent eventType, FileCompareDelegate handler)
        {
            if(eventType != FileCompareEvent.None)
            {
                Handlers.Add(eventType, handler);
            }
            return this;
        }

        /// <summary>Process each file in folder, and set up a comparison to matching relative path in another folder</summary>
        /// <param name="destFolder">Folder to Compare or Copy to.</param>
        /// <param name="recurse">True to process subdirectories</param>
        /// <param name="filePattern">File pattern to match on. (Regular Expressions not valid)</param>
        /// <param name="FileFilter">Optional Filter for Source files</param>
        public async Task MatchFilesAsync(string destFolder, string filePattern = "*")
        {
            await MatchFilesAsync(new DirectoryInfo(destFolder), filePattern);
        }

        /// <summary>Process each file in folder, and set up a comparison to matching relative path in another folder</summary>
        /// <param name="targetFolder">Folder to Compare or Copy to.</param>
        /// <param name="Recurse">True to process subdirectories</param>
        /// <param name="filePattern">File pattern to match on. (Regular Expressions not valid)</param>
        /// <param name="FileFilter">Optional Filter for Source files</param>
        public async Task MatchFilesAsync(DirectoryInfo targetFolder, string filePattern = "*")
        {
            if (!SourceFolder.Exists)
            {
                throw new DirectoryNotFoundException(SourceFolder.FullName);
            }
            if (!targetFolder.Exists)
            {
                targetFolder.Create();
            }

            string sourcePrefix = FolderPath(SourceFolder);
            string destPrefix = FolderPath(targetFolder);

            SearchOption recurseOption =
                Recurse ?
                SearchOption.AllDirectories :
                SearchOption.TopDirectoryOnly;

            var files = SourceFolder.EnumerateFiles(filePattern ?? "*", recurseOption);
            foreach (var sourceFile in files)
            {
                if (FileFilter == null || FileFilter(sourceFile))
                {
                    string relativeFilePath = sourceFile.FullName[sourcePrefix.Length..];
                    FileInfo targetFile = new FileInfo(Path.Combine(destPrefix, relativeFilePath));
                    
                    FileCompareEvent currentEvent = FileCompareEvent.None;

                    try
                    {
                        // avoid loops by only processing an event once per file.
                        var eventsProcessed = new List<FileCompareEvent>(MaxEventCount);

                        // start with either target file found or not.                        
                        currentEvent = 
                            targetFile.Exists?
                            FileCompareEvent.TargetFound :
                            FileCompareEvent.TargetNotFound;

                        while (currentEvent != FileCompareEvent.None)
                        {
                            // Pre-Event may output trace info (console, etc).
                            OnEventRaised?.Invoke(currentEvent, sourceFile, targetFile);

                            // Execute the processor.
                            if (Handlers.ContainsKey(currentEvent) && !eventsProcessed.Contains(currentEvent))
                            {
                                eventsProcessed.Add(currentEvent);
                                var handler = Handlers[currentEvent];
                                currentEvent = await handler.Invoke(sourceFile, targetFile);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        OnException?.Invoke(currentEvent, sourceFile, targetFile, ex);
                    }
                }
            }
        }

        public static Dictionary<string, FileCompareDelegate> GetHandlerDictionary()
        {
            FileCompareDelegate[] actions = { CompareFileLength, BinaryCompare, CopySourceToTarget, CopyTargetToSource, DeleteSource, DeleteTarget, };
            var pairs = from item in actions select new KeyValuePair<string, FileCompareDelegate>(item.Method.Name, item);
            return new Dictionary<string, FileCompareDelegate>(pairs, StringComparer.OrdinalIgnoreCase);
        }

        public static Task<FileCompareEvent> CompareFileLength(FileInfo source, FileInfo target)
            => Task.FromResult(
                source.Length == target.Length ?
                FileCompareEvent.LengthEqual :
                FileCompareEvent.LengthNotEqual);

        public static async Task<FileCompareEvent> BinaryCompare(FileInfo source, FileInfo target)
        {
            bool isMatch = await source.BinaryFileCompareAsync(target);
            return isMatch ? FileCompareEvent.BytesEqual : FileCompareEvent.BytesNotEqual;
        }

        /// <summary>Delete the Source file</summary>
        /// <returns><see cref="FileCompareEvent.SourceDeleted"/></returns>
        public static Task<FileCompareEvent> DeleteSource(FileInfo source, FileInfo target)
        {
            Debug.Assert(target.Exists);
            source.Delete();
            return Task.FromResult(FileCompareEvent.SourceDeleted);
        }

        /// <summary>Delete the Target file</summary>
        /// <returns><see cref="FileCompareEvent.TargetDeleted"/></returns>
        public static Task<FileCompareEvent> DeleteTarget(FileInfo source, FileInfo target)
        {
            Debug.Assert(source.Exists);
            target.Delete();
            return Task.FromResult(FileCompareEvent.TargetDeleted);
        }

        /// <summary>Copy File Aysncronously.</summary>
        /// <param name="source">File to copy From.</param>
        /// <param name="target">File to copy To.</param>
        /// <returns><see cref="FileCompareEvent.TargetWritten"/></returns>
        public static async Task<FileCompareEvent> CopySourceToTarget(FileInfo source, FileInfo target)
        {
            await source.CopyToAsync(target);
            return FileCompareEvent.TargetWritten;
        }

        /// <summary>Copy Target File back to Source.</summary>
        /// <param name="source">File to copy To.</param>
        /// <param name="target">File to copy From.</param>
        /// <returns><see cref="FileCompareEvent.SourceWritten"/></returns>
        public static async Task<FileCompareEvent> CopyTargetToSource(FileInfo source, FileInfo target)
        {
            await target.CopyToAsync(source);
            return FileCompareEvent.SourceWritten;
        }

        private static string FolderPath(DirectoryInfo info)
            =>
            info.FullName.EndsWith(Path.DirectorySeparatorChar) ? info.FullName :
            info.FullName.EndsWith(Path.AltDirectorySeparatorChar) ? info.FullName :
            info.FullName + Path.DirectorySeparatorChar;
    }

}
