using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileTools
{

    /// <summary>Action To Take When Comparing Two Files</summary>
    public enum FileCompareAction
    {
        /// <summary>Find Target (Check Existance)</summary>
        /// <remarks>This occurs as the starting Action.  Others are triggered off of it.</remarks>
        FindTarget = 0,

        /// <summary>Compare File.Length of Source and Target</summary>
        /// <remarks>Usually after true is returned by <see cref="FindTarget"/> or <see cref="WriteTarget"/> or <see cref="WriteSource"/></remarks>
        CompareLengths,

        /// <summary>Compare each Byte of Source and Target</summary>
        /// <remarks>Usually after true is returned by <see cref="FindTarget"/> or <see cref="WriteTarget"/> or <see cref="WriteSource"/></remarks>
        CompareBytes,

        /// <summary>Copy Source to Target File.</summary>
        /// <remarks>Usually after false is returned by <see cref="FindTarget"/> or <see cref="CompareLengths"/>or <see cref="CompareBytes"/></remarks>
        WriteTarget,

        /// <summary>Copy Target to Source File.</summary>
        /// <remarks>Usually after false is returned by <see cref="CompareLengths"/> or <see cref="CompareBytes"/></remarks>
        WriteSource,

        /// <summary>Delete Target File.</summary>
        /// <remarks>Usually after true is returned by <see cref="CompareLengths"/> or <see cref="CompareBytes"/></remarks>
        DeleteTarget,

        /// <summary>Delete Source File.</summary>
        /// <remarks>Usually after true is returned by <see cref="CompareLengths"/> or <see cref="CompareBytes"/></remarks>
        DeleteSource,
    }

    public class FileCompareActivity
    {
        public FileCompareAction Action { get; set; }
        public FileInfo Source { get; }
        public FileInfo Target { get; }
        
        private readonly Dictionary<FileCompareAction, bool> _Results = new Dictionary<FileCompareAction, bool>();

        public FileCompareActivity(FileCompareAction action, FileInfo source, FileInfo target)
        {
            Action = action;
            Source = source;
            Target = target;
        }

        /// <summary>Get Each Action Executed and the Result </summary>
        public IEnumerable<KeyValuePair<FileCompareAction, bool>> GetResults() => _Results.ToArray();

        /// <summary>Get Result of a specific action</summary>
        public bool? GetResult() => _Results.ContainsKey(Action) ? _Results[Action] : (bool?)null;

        public void SetResult(bool actionResult) => _Results.Add(Action, actionResult);
    }


}
