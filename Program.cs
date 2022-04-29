﻿using System;
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

        static IEnumerable<FileChunk> ReadFileChunks(Guid fileID, string fileName)
        {
            ulong nChunks = 0;
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                uint chunkId = 0;
                while (fileStream.Position != fileStream.Length)
                {
                    byte[] packet = arrayPool.Rent(Constants.PacketMaxSize);

                    var nRead = fileStream.Read(packet, Constants.FileChunkPacketOffset, Constants.FileChunkSize);
                    if (nRead <= 0)
                        break;

                    nChunks++;

                    if (nRead < Constants.FileChunkSize)
                    {
                        byte[] partialPacket = new byte[nRead];
                        Array.Copy(packet, partialPacket, nRead);
                        packet = partialPacket;
                    }

                    yield return new FileChunk(fileID, chunkId++, packet);
                }
            }

            Console.WriteLine($"Read {nChunks} chunks!");
        }

        static int taskNum = 0;
        static async Task ProcessAndTransmit(IEnumerable<FileChunk> fileChunks)
        {
            Console.WriteLine($"Launched Task[{Interlocked.Increment(ref taskNum)}]");

            using (SHA256 sha256 = SHA256.Create())
            {
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Connect("127.0.0.1", 12345);

                    foreach (var fileChunk in fileChunks)
                    {
                        // Set header
                        fileChunk.Data[0] = (byte)1;
                        Array.Copy(fileChunk.FileID.ToByteArray(), 0, fileChunk.Data, 1, Constants.GuidSize);
                        Array.Copy(BitConverter.GetBytes(fileChunk.ChunkID), 0, fileChunk.Data, 1 + Constants.GuidSize, sizeof(uint));

                        // Hash the chunk
                        byte[] hash = sha256.ComputeHash(fileChunk.Data, 0, fileChunk.Data.Length - Constants.HashSize);

                        // Copy the hash to the end of the packet
                        Array.Copy(hash, 0, fileChunk.Data, fileChunk.Data.Length - Constants.HashSize, Constants.HashSize);

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
