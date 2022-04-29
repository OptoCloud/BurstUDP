namespace BurstUDP
{
    internal static class Constants
    {
        public const int GuidSize = 16;
        public const int HashSize = 32;

        public const int FileChunkSize = 4 * 1024;
        public const int FileChunkPacketOffset = 1 + GuidSize + sizeof(uint);

        public const int PacketMinSize = 1 + HashSize; // MessageID + Hash
        public const int PacketMaxSize = 1 + GuidSize + sizeof(uint) + FileChunkSize + HashSize; // MessageID + Guid + FileChunk + Hash

        public const int PacketDefaultHeaderSize = PacketMinSize;
    }
}
