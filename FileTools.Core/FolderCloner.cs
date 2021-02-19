using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileTools
{
    public class FolderCloner
    {
        public const int DefaultBufferSize = 4096;
        public string RootPathSource { get; }
        public string RootPathDestination { get; }

        public Func<FileInfo, FileInfo, bool> CopyFilter { get; set; }
        public Func<FileInfo, FileInfo, bool> VerifyMatch { get; set; }
        public int CopyCount { get; private set; }
        public int VerifyCount { get; private set; }


        public FolderCloner(string sourcePath, string destinationPath)
        {
            RootPathSource = sourcePath;
            RootPathDestination = destinationPath;
            CopyFilter = (source, dest) => source.Exists && (!dest.Exists || source.Length != dest.Length || source.LastWriteTimeUtc != dest.CreationTimeUtc;
            VerifyMatch = (source, dest) => source.Exists && dest.Exists && source.Length == dest.Length && source.LastWriteTimeUtc == dest.LastWriteTimeUtc;
        }

        public async Task CloneAsync()
        {
            CopyCount = 0;
            VerifyCount = 0;

            var sourceFolder = new DirectoryInfo(RootPathSource);
            if(!sourceFolder.Exists)
            {
                throw new DirectoryNotFoundException(RootPathSource);
            }
            var destFolder = new DirectoryInfo(RootPathDestination);
            if (!destFolder.Exists)
            {
                destFolder.Create();
            }
            
            string sourcePrefix = sourceFolder.FullName;
            string destPrefix = destFolder.FullName;

            var files = sourceFolder.EnumerateFiles("*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string relativeFilePath = file.FullName.Substring(sourcePrefix.Length + 1);
                FileInfo destInfo = new FileInfo(Path.Combine(destPrefix, relativeFilePath));
                try
                {
                    // Copy if needed.
                    if (CopyFilter(file, destInfo))
                    {
                        var folder = new DirectoryInfo(destInfo.DirectoryName);
                        if (!folder.Exists)
                        {
                            folder.Create();
                        }

                        await file.CopyFileAsync(destInfo.FullName);
                        
                        destInfo.Refresh();
                        if(destInfo.Exists)
                        {
                            CopyCount++;
                        }
                    }
                    
                    // Verify
                    if(VerifyMatch(file, destInfo))
                    {
                        VerifyCount++;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine($"{file.FullName} to {destInfo.FullName} = {ex.Message}");
                }

            }
        }


    }

}
