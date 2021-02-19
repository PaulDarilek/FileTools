using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileTools
{
    public static class FileExtensions
    {
        public const int DefaultBufferSize = 4096 * 2;

        public static async Task<bool> CopyFileAsync(this FileInfo sourceInfo, string destination, int bufferSize = DefaultBufferSize)
        {
            if(!sourceInfo.Exists)
            {
                throw new FileNotFoundException(sourceInfo.FullName);
            }

            var destInfo = new FileInfo(destination);
            bufferSize = bufferSize > 0 ? bufferSize : DefaultBufferSize;

            using (FileStream sourceStream = File.Open(sourceInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using FileStream destinationStream = File.Create(destination, bufferSize, FileOptions.Asynchronous);
                await sourceStream.CopyToAsync(destinationStream);
            }

            // clone Create and Last Update time.
            destInfo.Refresh();
            destInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
            destInfo.LastAccessTimeUtc = sourceInfo.LastAccessTimeUtc;
            //* Skip: destInfo.Attributes = sourceInfo.Attributes;

            return sourceInfo.Exists && destInfo.Exists && sourceInfo.Length == destInfo.Length;
        }

        public static async Task<bool> FilesAreEqualAsync(this FileInfo sourceInfo, FileInfo destInfo, int bufferSize = DefaultBufferSize)
        {
            if (!sourceInfo.Exists || !destInfo.Exists || sourceInfo.Length != destInfo.Length)
            {
                // One of the files are missing or File Sizes do not match.
                return await Task.FromResult(false);
            }
            long fileLength = sourceInfo.Length;

            bufferSize = bufferSize > 0 ? bufferSize : DefaultBufferSize;
            byte[] sourceData = new byte[bufferSize];
            byte[] destData = new byte[bufferSize];
            long offset = 0;

            using FileStream sourceStream = File.Open(sourceInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destinationStream = File.Open(destInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            while (offset < fileLength)
            {
                // start both tasks to read.
                var sTask = sourceStream.ReadAsync(sourceData, (int)offset, bufferSize);
                var dTask = destinationStream.ReadAsync(destData, (int)offset, bufferSize);

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
            return offset == fileLength;

        }


    }
}
