using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileTools
{
    class FileInfoCopy 
    {
        /// <summary>Does the File Exist.</summary>
        public bool Exists { get; }

        /// <summary>Full Path of Directory the file is in.</summary>
        public string DirectoryName { get; }

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

        public FileInfoCopy(string fileName)
        {
            var info = new System.IO.FileInfo(fileName);
            Exists = info.Exists;
            if(Exists)
            {
                DirectoryName = info.DirectoryName;
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
