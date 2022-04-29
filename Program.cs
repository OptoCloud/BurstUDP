using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace BurstUDP
{
    public static class Program
    {
        static readonly ArrayPool<byte> arrayPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// Contains a chunk read from the file, this will be hashed, have its header written, hashed, then having its hash written to it. then it will be sent to the client.
        /// </summary>
        struct FileChunk
        {
            public FileChunk(Guid fileID, uint chunkId, byte[] data) : this()
            {
                FileID = fileID;
                ChunkID = chunkId;
                Data = data;
            }

            public Guid FileID { get; }
            public uint ChunkID;
            public byte[] Data;
        }

        /// <summary>
        /// Reads the file in chunks, and returns them as a lazy enumerable.
        /// </summary>
        /// <param name="fileID"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        static IEnumerable<FileChunk> ReadFileChunks(Guid fileID, string fileName)
        {
            // For debugging
            ulong nChunks = 0;

            // Open file for reads
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                uint chunkId = 0;

                // While there is still data to read
                while (fileStream.Position != fileStream.Length)
                {
                    // Get a array from the array pool to keep the memory usage down, and the speed up
                    byte[] packet = arrayPool.Rent(Constants.PacketMaxSize);

                    // Read the data into the array
                    var nRead = fileStream.Read(packet, Constants.FileChunkPacketOffset, Constants.FileChunkSize);
                    if (nRead <= 0)
                        break;

                    // For debugging
                    nChunks++;

                    // If we read less than the chunk size, then its probably the last chunk, resize it to the actual size
                    if (nRead < Constants.FileChunkSize)
                    {
                        byte[] partialPacket = new byte[nRead];
                        Array.Copy(packet, partialPacket, nRead);
                        packet = partialPacket;
                    }

                    // Queue the chunk for further processing
                    yield return new FileChunk(fileID, chunkId++, packet);
                }
            }

            Console.WriteLine($"Read {nChunks} chunks!");
        }

        static int taskNum = 0;

        /// <summary>
        /// Processes a enumerable of file chunks, and sends them to the client.
        /// </summary>
        /// <param name="fileChunks"></param>
        /// <returns></returns>
        static async Task ProcessAndTransmit(IEnumerable<FileChunk> fileChunks)
        {
            Console.WriteLine($"Launched Task[{Interlocked.Increment(ref taskNum)}]");

            using (SHA256 sha256 = SHA256.Create())
            {
                using (UdpClient udpClient = new UdpClient())
                {
                    // For testing
                    udpClient.Connect("127.0.0.1", 12345);

                    // Iterate through all the chunks
                    foreach (var fileChunk in fileChunks)
                    {
                        // Set the header ( message ID, file ID, and chunk ID )
                        fileChunk.Data[0] = (byte)1;
                        Array.Copy(fileChunk.FileID.ToByteArray(), 0, fileChunk.Data, 1, Constants.GuidSize);
                        Array.Copy(BitConverter.GetBytes(fileChunk.ChunkID), 0, fileChunk.Data, 1 + Constants.GuidSize, sizeof(uint));

                        // Hash the the header + filechunk data
                        byte[] hash = sha256.ComputeHash(fileChunk.Data, 0, fileChunk.Data.Length - Constants.HashSize);

                        // Copy the hash to the end of the packet
                        Array.Copy(hash, 0, fileChunk.Data, fileChunk.Data.Length - Constants.HashSize, Constants.HashSize);

                        // Send the packet
                        await udpClient.SendAsync(fileChunk.Data, fileChunk.Data.Length);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            string filePath = "exampleFile.rar";

            // Calculate number of chunks
            var numChunks = Utils.CalculateFileChunkCount(filePath);

            // Get desired degree of parallelism
            var degreeOfParallelism = (uint)Environment.ProcessorCount;

            // Get amount of chunks to process on each thread
            var chunksPerThread = (ulong)Math.Ceiling((double)numChunks / degreeOfParallelism);

            // YEET
            ReadFileChunks(Guid.NewGuid(), filePath) // Read all chunks
                .Batch(chunksPerThread)              // Split the chunks into batches
                .ForEachAsync(ProcessAndTransmit)    // Process them in parallel tasks
                .Wait();                             // Await the tasks

            while (true) { Thread.Sleep(100); }
        }
    }
}
