using System;
using System.Threading.Tasks;

namespace FileTools
{
    public interface IFileCompareProcessor
    {
        Task<bool> ProcessActivityAsync(FileCompareActivity activity);
    }

    public class FileCompareProcessor : IFileCompareProcessor
    {
        /// <summary>Process the Activity</summary>
        public async Task<bool> ProcessActivityAsync(FileCompareActivity activity)
        {
            Func<FileCompareActivity, Task<bool>> method = null;
            switch (activity.Action)
            {
                case FileCompareAction.FindTarget:
                    method = CheckTargetExists;
                    break;

                case FileCompareAction.CompareLengths:
                    method = CompareLengths;
                    break;

                case FileCompareAction.CompareBytes:
                    method = CompareBytes;
                    break;

                case FileCompareAction.WriteTarget:
                    method = WriteTarget;
                    break;

                case FileCompareAction.WriteSource:
                    method = WriteSource;
                    break;

                case FileCompareAction.DeleteTarget:
                    method = DeleteTarget;
                    break;

                case FileCompareAction.DeleteSource:
                    method = DeleteSource ;
                    break;
            }

            return await method(activity);
        }

        protected async virtual Task<bool> CheckTargetExists(FileCompareActivity activity) 
            => await Task.FromResult(activity.Target.Exists);

        protected async virtual Task<bool> CompareLengths(FileCompareActivity activity)
            => await Task.FromResult(activity.Source.Exists && activity.Target.Exists && activity.Source.Length == activity.Target.Length);

        protected async virtual Task<bool> CompareBytes(FileCompareActivity activity)
            => await activity.Source.CompareBytes(activity.Target);
        
        protected async virtual Task<bool> WriteTarget(FileCompareActivity activity)
        {
            await activity.Source.CopyToAsync(activity.Target);
            activity.Target.Refresh();
            return await CompareLengths(activity);
        }
        protected async virtual Task<bool> WriteSource(FileCompareActivity activity)
        {
            await activity.Target.CopyToAsync(activity.Source);
            activity.Source.Refresh();
            return await CompareLengths(activity);
        }

        protected async virtual Task<bool> DeleteTarget(FileCompareActivity activity)
        {
            activity.Target.Delete();
            activity.Target.Refresh();
            return await Task.FromResult(!activity.Target.Exists);
        }

        protected async virtual Task<bool> DeleteSource(FileCompareActivity activity)
        {
            activity.Source.Delete();
            activity.Source.Refresh();
            return await Task.FromResult(!activity.Source.Exists);
        }

    }


}