using FileTools;
using System;
using System.IO;
using System.Linq;

namespace FolderClone
{
    internal class Program
    {
        internal static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 3 || args.Any(arg => arg.StartsWith("--Help", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"FolderClone sourceFolder destinationFolder --DeleteSource");
                Console.WriteLine($"\t Where:");
                Console.WriteLine($"\t   sourceFolder is the Source to copy From.");
                Console.WriteLine($"\t   destinationFolder is the target folder to Copy (if needed).");
                Console.WriteLine($"\t   --DeleteSource is optional parameter to remove source files after verifying bytes match in each file.");
                return;
            }

            string sourceFolder = args[0];
            string destFolder = args[1];
            bool isDelete = (args.Length > 2) && args[3].Equals("--DeleteSource", StringComparison.OrdinalIgnoreCase);

            var cloner = new FolderCloner(sourceFolder, destFolder)
            {
                CopyFilter = ShouldCopy,
                VerifyMatch = isDelete ? VerifyAndDeleteSource : NoVerify,
            };

            cloner.CloneAsync().GetAwaiter().GetResult();

            Console.WriteLine($"Copy From \"{sourceFolder}\" To \"{destFolder}\" = {cloner.CopyFilter} Files copied, {cloner.VerifyCount} files verified and deleted.");
        }

        private static bool ShouldCopy(FileInfo source, FileInfo destination)
        {
            if(!source.Exists)
            {
                throw new FileNotFoundException(source.FullName);
            }

            bool shouldCopy =
                destination.Exists == false ||
                source.Length != destination.Length ||
                source.LastWriteTimeUtc != destination.LastWriteTimeUtc;

            if (shouldCopy)
            {
                Console.WriteLine($"{source.FullName}\t{destination.FullName}");
                return true;
            }
            return false;
        }

        private static bool VerifyAndDeleteSource(FileInfo source, FileInfo destination)
        {
            var areEqual = source.FilesAreEqualAsync(destination).GetAwaiter().GetResult();
            if (areEqual)
            {
                Console.WriteLine($"Delete {source.FullName}");
                source.Delete();
            }
            else
            {
                Console.WriteLine($"Not a Match: {source.FullName} and {destination.FullName}");
            }
            return areEqual;
        }

        private static bool NoVerify(FileInfo source, FileInfo dest) => false;
    }
}
