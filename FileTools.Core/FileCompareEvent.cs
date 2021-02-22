using System.IO;
using System.Threading.Tasks;

namespace FileTools
{
    /// <summary>Signature of the Handlers used in Folder comparisons when matching files.</summary>
    /// <returns>An Event indicating a change in file system state.</returns>
    public delegate Task<FileCompareEvent> FileCompareDelegate(FileInfo source, FileInfo destination);


    /// <summary>Events raised by <see cref="FileCompareDelegate"/> handlers. Acts as a State of the file system</summary>
    public enum FileCompareEvent
    {
        /// <summary>Default value</summary>
        None,

        /// <summary>Target file Exists</summary>
        TargetFound,
        /// <summary>Target file does not Exist</summary>
        TargetNotFound,

        /// <summary>Target file was Written To (Source copied to Target)</summary>
        TargetWritten,
        /// <summary>Target file was Deleted</summary>
        TargetDeleted,

        /// <summary>Source file was Written To (Target copied to Source)</summary>
        SourceWritten,
        /// <summary>Source file was Deleted</summary>
        SourceDeleted,

        /// <summary>Source and Target file are same length</summary>
        LengthEqual,
        /// <summary>Source and Target file are different length</summary>
        LengthNotEqual,

        /// <summary>Binary Comparison shows Files are Equal</summary>
        BytesEqual,
        /// <summary>Binary Comparison shows Files are Different</summary>
        BytesNotEqual,

        ///// <summary>Hash Code Comparison shows Files have equal hash (Files probably the same)</summary>
        //HashEqual,
        ///// <summary>Hash Code Comparison shows Files have different hashes (Files are different)</summary>
        //HashNotEqual,
    }

}
