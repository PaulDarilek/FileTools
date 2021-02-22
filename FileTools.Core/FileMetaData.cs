using System;
using System.IO;

namespace FileTools
{
    class FileMetaData 
    {
        /// <summary>Does the File Exist.</summary>
        public bool Exists { get; }

        /// <summary>Computer (machine) where Drive or Share is found.</summary>
        public string MachineName { get; }

        /// <summary>Drive Letter or Share Path (Root)</summary>
        public string DrivePath { get; }

        /// <summary>Full Path of Directory the file is in.</summary>
        public string DirectoryPath{ get; }

        /// <summary>Size of file in bytes</summary>
        public long Length { get; }

        /// <summary>Name of File without Path</summary>
        public string Name { get; }

        /// <summary>Fully Qualified Path</summary>
        public string FullName { get; }

        /// <summary>File Extension (including leading period)</summary>
        public string Extension { get; }

        /// <summary>Is the File Read-Only.</summary>
        public bool IsReadOnly { get; }

        /// <summary>When file was last written to</summary>
        public DateTime? LastWriteTimeUTC { get; set; }

        /// <summary>When File was Created</summary>
        public DateTime? CreationTimeUTC { get; set; }


        public FileMetaData(string fileName, string machineName = null) : this(new FileInfo(fileName), machineName)
        {
            // chained constructor.
        }
            
        public FileMetaData(FileInfo info, string machineName = null)
        {
            Exists = info.Exists;
            if(Exists)
            {
                MachineName = machineName ?? System.Environment.MachineName;
                DrivePath = info.Directory.Root.FullName;
                DirectoryPath = info.DirectoryName[DrivePath.Length..];
                Length = info.Length;
                Name = info.Name;
                FullName = info.FullName;
                Extension = info.Extension;
                IsReadOnly = info.IsReadOnly;
                LastWriteTimeUTC = info.LastWriteTimeUtc;
                CreationTimeUTC = info.CreationTimeUtc;
            }
        }

    }
}
