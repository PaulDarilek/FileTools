using FileTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FolderClone
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var argList = args.ToList();

            if (argList.Count < 2 || argList.HasSwitch("-?", "--help"))
            {
                Console.WriteLine($"FolderClone sourceFolder destinationFolder --DeleteSource");
                Console.WriteLine($"\t Where:");
                Console.WriteLine($"\t   sourceFolder is the Source to copy From.");
                Console.WriteLine($"\t   destinationFolder is the target folder to Copy (if needed).");
                Console.WriteLine($"\t   -? or --help is optional parameter to display this help.");
                Console.WriteLine($"\t   -r or --recurse enables searching subdirectories");
                Console.WriteLine($"\t   -h or --hidden copies hidden files also");
                Console.WriteLine($"\t   --DeleteSource is optional parameter to remove source files after verifying bytes match in each file.");
                return;
            }

            string sourceFolder = argList.NextParm(true, nameof(sourceFolder));
            string destinationFolder = argList.NextParm(true, nameof(destinationFolder));
            bool hidden = argList.HasSwitch("-h", "--hidden");
            bool recurse = argList.HasSwitch("-r", "--recurse");
            bool deleteSource = argList.HasSwitch("--DeleteSource");

            var cloner = new FolderProcessor(sourceFolder)
            {
                VerifyMatchAsync = BinaryCompareAsync,
                OnVerifyFailedAsync = CopyFileAsync,
                OnVerifyPassedAsync = null,
                OnException = (source, dest, ex) => Console.Error.WriteLine($"Error {ex.Message}: {source.FullName} {dest.FullName}"),
            };

            if (deleteSource)
            {
                cloner.OnVerifyPassedAsync = DeleteMatchedSourceFile;
                cloner.OnVerifyFailedAsync = CopyVerifyAndDeleteAsync;
            }

            Func<FileInfo, bool> filePredicate = hidden ? null : FilterHiddenFile;
            cloner.MatchFilesAsync(destinationFolder, recurse: recurse, filterPredicate: filePredicate).GetAwaiter().GetResult();

            //Console.WriteLine($"Copy From \"{sourceFolder}\" To \"{destFolder}\" = {cloner.VerifyMatchAsync} Files copied, {cloner.VerifyCount} files verified and deleted.");
        }

        private static bool FilterHiddenFile(this FileInfo file) => !file.Attributes.HasFlag(FileAttributes.Hidden);

        private static bool HasSwitch(this List<string> argList, params string[] switches)
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

        private static string NextParm(this List<string> argList, bool required, string parameterName)
        {
            string arg = null;
            if(argList.Count > 0)
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

        private static async Task<bool> BinaryCompareAsync(FileInfo source, FileInfo destination)
        {
            Console.Write($"Compare: {source.FullName} to {destination.FullName} ");
            bool isMatch = await source.BinaryFileCompareAsync(destination);
            Console.WriteLine(isMatch ? "(Matched)" : "(Failed)");
            return isMatch;
        }

        private static async Task CopyFileAsync(FileInfo source, FileInfo destination)
        {
            Console.Write($"Copying File: {source.FullName} to {destination.FullName} ");
            await source.CopyToAsync(destination);
            Console.WriteLine(" (Done)");
        }

        private static async Task CopyVerifyAndDeleteAsync(FileInfo source, FileInfo destination)
        {
            await CopyFileAsync(source, destination);
            bool verified = await BinaryCompareAsync(source, destination);
            if(verified)
            {
                await DeleteMatchedSourceFile(source, destination);
            }
        }

        private static Task DeleteMatchedSourceFile(FileInfo source, FileInfo destination)
        {
            if(source.Exists && destination.Exists && source.Length == destination.Length)
            {
                Console.WriteLine($"Delete Match: {source.FullName}");
                source.Delete();
                var sourceFolder = source.Directory;
                if(sourceFolder != null && sourceFolder.Exists)
                {
                    if (!sourceFolder.EnumerateFileSystemInfos().Any())
                    {
                        sourceFolder.Delete();
                    }
                }
            }
            return Task.CompletedTask;
        }


    }
}
