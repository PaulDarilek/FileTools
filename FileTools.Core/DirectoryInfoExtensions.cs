using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FileTools
{
    public static class DirectoryInfoExtensions
    {
        [DebuggerStepThrough()]
        public static int RemoveEmptyFolders(this DirectoryInfo directory, string pattern = "*", bool recurse = true)
        {
            int count = 0;
            Stack<string> stack = new Stack<string>();
            stack.Push(directory.FullName);
            foreach (var info in directory.EnumerateDirectories(pattern, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                stack.Push(info.FullName);
            }
            while (stack.Count > 0)
            {
                DirectoryInfo info = new DirectoryInfo(stack.Pop());
                if (!info.GetFileSystemInfos().Any())
                {
                    info.Delete();
                    count++;
                }
            }
            return count;
        }


    }
}
