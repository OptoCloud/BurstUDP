using System;
using System.IO;

namespace BurstUDP
{
    internal static class Utils
    {
        /// <summary>
        /// Calculates the amount of chunks the given fileSize will be divided into.
        /// </summary>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public static UInt32 CalculateFileChunkCount(ulong fileSize)
        {
            return (UInt32)Math.Ceiling((double)fileSize / Constants.FileChunkSize);
        }
        
        /// <summary>
        /// Calculates the amount of chunks the given file will be divided into.
        /// </summary>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public static UInt32 CalculateFileChunkCount(string filePath)
        {
            long fileSize = new FileInfo(filePath).Length;
            if (fileSize < 0)
                return 0;

            return CalculateFileChunkCount((ulong)fileSize);
        }
    }
}
