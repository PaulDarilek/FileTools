using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FileTools
{
    public static class FileInfoExtensions
    {
        public const int MinimumBufferSize = 256;
        public const int MaximumBufferSize = 1024 * 1024;
        public const int DefaultBufferSize = 4096 * 2;
        public static bool CopyCreationTimeUtc { get; set; } = true;
        public static bool ClearSourceArchiveBit { get; set; } = true;

        [DebuggerStepThrough()]
        public static bool CopyTo(this FileInfo sourceInfo, FileInfo destInfo)
            => sourceInfo.CopyToAsync(destInfo, DefaultBufferSize).GetAwaiter().GetResult();

        [DebuggerStepThrough()]
        public static async Task<bool> CopyToAsync(this FileInfo sourceInfo, FileInfo destInfo)
        {
            await sourceInfo.CopyToAsync(destInfo, DefaultBufferSize);
            destInfo.Refresh();
            return destInfo.Exists && destInfo.Length == sourceInfo.Length;
        }

        [DebuggerStepThrough()]
        public static async Task<bool> CopyToAsync(this FileInfo sourceInfo, FileInfo destInfo, int bufferSize)
        {
            if (!sourceInfo.Exists)
            {
                throw new FileNotFoundException(sourceInfo.FullName);
            }

            // Eliminate DirectoryNotFoundException by creating folder if it is missing.
            var folder = new DirectoryInfo(destInfo.DirectoryName);
            if (!folder.Exists)
            {
                folder.Create();
            }

            // switch allows copying source CreationTimeUtc if set to True.
            // if not set, the target file's CreationTimeUtc will be kept if the file exists.
            // otherwise it was created now.
            var creationTime =
                CopyCreationTimeUtc ?
                sourceInfo.CreationTimeUtc :
                destInfo.Exists ?
                destInfo.CreationTimeUtc :
                DateTime.UtcNow;

            if (bufferSize < MinimumBufferSize || bufferSize > MaximumBufferSize)
            {
                bufferSize = DefaultBufferSize;
            }

            using (FileStream sourceStream = File.Open(sourceInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using FileStream destinationStream = File.Create(destInfo.FullName, bufferSize, FileOptions.Asynchronous);
                await sourceStream.CopyToAsync(destinationStream);
            }

            // set filesystem dates and attributes.
            destInfo.Refresh();
            destInfo.CreationTimeUtc = creationTime;
            destInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
            destInfo.Attributes = sourceInfo.Attributes;
            //default:
            destInfo.LastAccessTime = DateTime.UtcNow;

            if(ClearSourceArchiveBit && sourceInfo.Attributes.HasFlag(FileAttributes.Archive))
            {
                // be a good backup tool.
                sourceInfo.Attributes &= ~FileAttributes.Archive;
            }

            return destInfo.Exists && destInfo.Length == sourceInfo.Length;
        }

        /// <summary>Compares two files byte-for-byte and returns True if they match.</summary>
        [DebuggerStepThrough()]
        public static async Task<bool> CompareBytes(this FileInfo sourceInfo, FileInfo destInfo)
            => await CompareBytesAsync(sourceInfo, destInfo, DefaultBufferSize);

        /// <summary>Compares two files byte-for-byte and returns True if they match.</summary>
        [DebuggerStepThrough()]
        public static async Task<bool> CompareBytesAsync(this FileInfo sourceInfo, FileInfo destInfo, int bufferSize)
        {
            if (!sourceInfo.Exists || !destInfo.Exists || sourceInfo.Length != destInfo.Length)
            {
                // One of the files are missing or File Sizes do not match.
                return await Task.FromResult(false);
            }
            long fileLength = sourceInfo.Length;

            if (bufferSize < MinimumBufferSize || bufferSize > MaximumBufferSize)
            {
                bufferSize = DefaultBufferSize;
            }
            byte[] sourceData = new byte[bufferSize];
            byte[] destData = new byte[bufferSize];
            long offset = 0;

            using FileStream sourceStream = File.Open(sourceInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destinationStream = File.Open(destInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            while (offset < fileLength)
            {
                // start both tasks to read.
                var sTask = sourceStream.ReadAsync(sourceData, 0, bufferSize);
                var dTask = destinationStream.ReadAsync(destData, 0, bufferSize);

                // wait on both tasks.
                int sBytes = await sTask;
                int dBytes = await dTask;

                // compare bytes read.
                int count = Math.Min(sBytes, dBytes);
                if (count == 0)
                {
                    // one or both files are at End of File.
                    break;
                }
                else
                {
                    if(sBytes != dBytes)
                    {
                        sourceStream.Seek(offset + count, SeekOrigin.Begin);
                        destinationStream.Seek(offset + count, SeekOrigin.Begin);
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    if (sourceData[i] != destData[i])
                    {
                        return false;
                    }
                }
                offset += count;
            }

            // all the bytes read and compared total the file length
            bool success = (offset == fileLength);
            return success;
        }

    }
}
