using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileTools
{
    public class FolderProcessor
    {
        public Func<FileInfo, FileInfo, Task<bool>> VerifyMatchAsync { get; set; }
        public Func<FileInfo, FileInfo, Task> OnVerifyPassedAsync { get; set; }
        public Func<FileInfo, FileInfo, Task> OnVerifyFailedAsync { get; set; }

        public Action<FileInfo, FileInfo, Exception>  OnException { get; set; }

        private DirectoryInfo SourceFolder { get; set; }

        public FolderProcessor(string sourcePath)
        {
            SourceFolder = new DirectoryInfo(sourcePath);
        }

        /// <summary>Process each file in folder, and set up a comparison to matching relative path in another folder</summary>
        /// <param name="destFolder">Folder to Compare or Copy to.</param>
        /// <param name="recurse">True to process subdirectories</param>
        /// <param name="filePattern">File pattern to match on. (Regular Expressions not valid)</param>
        /// <param name="filterPredicate">Optional Filter for Source files</param>
        public async Task MatchFilesAsync(string destFolder, bool recurse = true, string filePattern = "*", Func<FileInfo, bool> filterPredicate = null)
        {
            await MatchFilesAsync(new DirectoryInfo(destFolder), recurse, filePattern, filterPredicate);
        }

        /// <summary>Process each file in folder, and set up a comparison to matching relative path in another folder</summary>
        /// <param name="destFolder">Folder to Compare or Copy to.</param>
        /// <param name="recurse">True to process subdirectories</param>
        /// <param name="filePattern">File pattern to match on. (Regular Expressions not valid)</param>
        /// <param name="filterPredicate">Optional Filter for Source files</param>
        public async Task MatchFilesAsync(DirectoryInfo destFolder, bool recurse = true, string filePattern = "*", Func<FileInfo, bool> filterPredicate = null)
        {
            if (!SourceFolder.Exists)
            {
                throw new DirectoryNotFoundException(SourceFolder.FullName);
            }
            if (!destFolder.Exists)
            {
                destFolder.Create();
            }

            string sourcePrefix = FolderPath(SourceFolder);
            string destPrefix = FolderPath(destFolder);

            VerifyMatchAsync ??= CompareFileLengthAsync;
            OnVerifyPassedAsync ??= NoActionAsync;
            OnVerifyFailedAsync ??= NoActionAsync;

            SearchOption recurseOption =
                recurse ?
                SearchOption.AllDirectories :
                SearchOption.TopDirectoryOnly;

            var files = SourceFolder.EnumerateFiles(filePattern ?? "*", recurseOption);
            foreach (var file in files)
            {
                if (filterPredicate == null || filterPredicate(file))
                {
                    string relativeFilePath = file.FullName.Substring(sourcePrefix.Length);
                    FileInfo destInfo = new FileInfo(Path.Combine(destPrefix, relativeFilePath));
                    try
                    {
                        // Verify Match
                        bool verified = await VerifyMatchAsync(file, destInfo);
                        if (verified)
                        {
                            // Files Matched.
                            await OnVerifyPassedAsync(file, destInfo);
                        }
                        else
                        {
                            // Files Did Not Match
                            await OnVerifyFailedAsync(file, destInfo);
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        OnException?.Invoke(file, destInfo, ex);
                    }
                }
            }
        }

        public int RemoveEmptyFolders(string pattern = "*", bool recurse = true)
        {
            int count = 0;
            Stack<string> stack = new Stack<string>();
            stack.Push(SourceFolder.FullName);
            foreach (var info in SourceFolder.EnumerateDirectories(pattern, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                stack.Push(info.FullName);
            }
            while(stack.Count > 0)
            {
                DirectoryInfo info = new DirectoryInfo(stack.Pop());
                if(! info.GetFileSystemInfos().Any())
                {
                    info.Delete();
                    count++;
                }
            }
            return count;
        }

        /// <summary>Do Nothing Asyncronously</summary>
        private static Task NoActionAsync(FileInfo source, FileInfo dest) 
            => Task.CompletedTask;

        private static Task<bool> CompareFileLengthAsync(FileInfo source, FileInfo dest)
            => Task.FromResult(source.Exists && dest.Exists && source.Length != dest.Length);

        private static string FolderPath(DirectoryInfo info)
            =>
            info.FullName.EndsWith(Path.DirectorySeparatorChar) ? info.FullName :
            info.FullName.EndsWith(Path.AltDirectorySeparatorChar) ? info.FullName :
            info.FullName + Path.DirectorySeparatorChar;

    }

}
