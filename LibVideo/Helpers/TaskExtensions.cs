using System;
using System.Threading.Tasks;

namespace LibVideo.Helpers
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Ensures unobserved exceptions inside fire-and-forget tasks are handled
        /// by injecting a catch block that logs them.
        /// </summary>
        public static async void SafeFireAndForget(this Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SafeFireAndForget task swallowed an exception");
            }
        }
    }
}
