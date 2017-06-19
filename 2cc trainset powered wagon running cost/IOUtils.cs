using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Threading.Tasks;

namespace _2cc_trainset_powered_wagon_running_cost {
    internal static class IOUtils {
        public static string AppDir {
            get {
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (assemblyLocation == null)
                    return null;
                var fileInfo = new FileInfo(assemblyLocation);
                return fileInfo.DirectoryName;
            }
        }

        public static bool TryCreateDirectory(string path, int timeout = 10000, int retryInterval = 50) {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path == string.Empty || string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be an empty string or consist of only whitespace characters.", nameof(path));
            if (path.IndexOfAny(Path.GetInvalidPathChars()) > -1)
                throw new ArgumentException("Path contains invalid characters.", nameof(path));
            if (timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Must be a positive value.");
            if (retryInterval < 0 || retryInterval > timeout)
                throw new ArgumentOutOfRangeException(nameof(retryInterval), "Must be a positive value and should be less than the timeout value.");

            if (File.Exists(path))
                throw new IOException("The directory specified by path is a file.");

            var sw = Stopwatch.StartNew();

            while (sw.Elapsed.TotalMilliseconds <= timeout) {
                if (Directory.Exists(path))
                    return true;

                try {
                    Directory.CreateDirectory(path);
                } catch (IOException) {
                    Task.Delay(retryInterval).Wait();
                } catch (UnauthorizedAccessException) {
                    Task.Delay(retryInterval).Wait();
                }
            }
            return false;
        }

        /// <summary>
        /// Tries the delete directory, attempting to wait for any lingering file or directory handles to release during a specified timeout period.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        /// <param name="recursive">if set to <c>true</c> then recursively delete all files and folders under the directory specified.</param>
        /// <param name="deleteReadonlyFiles">if set to <c>true</c> then it will delete read-only files and directories without warning.</param>
        /// <param name="timeout">The timeout for retries in this operation in milliseconds. This does NOT time out because the actual deletion of files or directories is taking too long.</param>
        /// <param name="retryInterval">The interval in milliseconds of each attempt at deleting the directory should there be a possible unreleased handle lingering.</param>
        /// <returns>
        ///   <c>true</c> on success, <c>false</c> on failure.
        /// </returns>
        /// <exception cref="ArgumentNullException">path</exception>
        /// <exception cref="ArgumentException">
        /// Path cannot be an empty string or consist of only whitespace characters. - path
        /// or
        /// Path contains invalid characters. - path
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">timeout - Must be a positive value.
        /// or
        /// retryInterval - Must be a positive value and should be less than the timeout value.</exception>
        public static bool TryDeleteDirectory(string path, bool recursive = false, bool deleteReadonlyFiles = false, int timeout = 10000, int retryInterval = 50) {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path == string.Empty || string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be an empty string or consist of only whitespace characters.", nameof(path));
            if (path.IndexOfAny(Path.GetInvalidPathChars()) > -1)
                throw new ArgumentException("Path contains invalid characters.", nameof(path));
            if (timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Must be a positive value.");
            if (retryInterval < 0 || retryInterval > timeout)
                throw new ArgumentOutOfRangeException(nameof(retryInterval), "Must be a positive value and should be less than the timeout value.");

            if (deleteReadonlyFiles && Directory.Exists(path))
                ClearReadOnlyFlags(path, recursive);

            // Start the retry timeout timer
            var sw = Stopwatch.StartNew();

            var failedOnceBefore = false;
            while (sw.Elapsed.TotalMilliseconds <= timeout) {
                // Check if the directory doesn't exist at this point, be it because it wasn't there to begin with or it finally got removed by the OS...
                // Either way, if it no longer exists then hooray!
                if (!Directory.Exists(path))
                    return true;

                try {
                    // If we failed before, attempt deleting all files first.
                    if (failedOnceBefore)
                        Array.ForEach(Directory.GetFiles(path, "*", SearchOption.AllDirectories), File.Delete);

                    Directory.Delete(path, recursive);
                } catch (IOException) {
                    failedOnceBefore = true;
                    Task.Delay(retryInterval).Wait();
                } catch (UnauthorizedAccessException) {
                    failedOnceBefore = true;
                    Task.Delay(retryInterval).Wait();
                }
            }

            return false;
        }

        private static void ClearReadOnlyFlags(string path, bool recursive = false) {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path == string.Empty || string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be an empty string or consist of only whitespace characters.", nameof(path));
            if (path.IndexOfAny(Path.GetInvalidPathChars()) > -1)
                throw new ArgumentException("Path contains invalid characters.", nameof(path));

            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);

            if (!recursive)
                return;

            Array.ForEach(Directory.GetDirectories(path, "*", SearchOption.AllDirectories), f => ClearReadOnlyFlags(f));
            Array.ForEach(Directory.GetFiles(path, "*", SearchOption.AllDirectories), f => ClearReadOnlyFlags(f));
        }
    }
}