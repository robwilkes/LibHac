﻿using System.IO;

namespace LibHac.IO
{
    public class BucketTree<T> where T : BucketTreeEntry, new()
    {
        private const int BucketAlignment = 0x4000;
        public BucketTreeHeader Header { get; }
        public BucketTreeBucket<BucketTreeEntry> BucketOffsets { get; }
        public BucketTreeBucket<T>[] Buckets { get; }

        public BucketTree(Storage header, Storage data)
        {
            Header = new BucketTreeHeader(header);
            var reader = new BinaryReader(data.AsStream());

            BucketOffsets = new BucketTreeBucket<BucketTreeEntry>(reader);

            Buckets = new BucketTreeBucket<T>[BucketOffsets.EntryCount];

            for (int i = 0; i < BucketOffsets.EntryCount; i++)
            {
                reader.BaseStream.Position = (i + 1) * BucketAlignment;
                Buckets[i] = new BucketTreeBucket<T>(reader);
            }
        }
    }

    public class BucketTreeHeader
    {
        public string Magic;
        public int Version;
        public int NumEntries;
        public int Field1C;

        public BucketTreeHeader(Storage stream)
        {
            var reader = new BinaryReader(stream.AsStream());

            Magic = reader.ReadAscii(4);
            Version = reader.ReadInt32();
            NumEntries = reader.ReadInt32();
            Field1C = reader.ReadInt32();
        }
    }

    public class BucketTreeBucket<T> where T : BucketTreeEntry, new()
    {
        public int Index;
        public int EntryCount;
        public long OffsetEnd;
        public T[] Entries;


        public BucketTreeBucket(BinaryReader reader)
        {
            Index = reader.ReadInt32();
            EntryCount = reader.ReadInt32();
            OffsetEnd = reader.ReadInt64();
            Entries = new T[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                Entries[i] = new T();
                Entries[i].Read(reader);
            }
        }
    }

    public class BucketTreeEntry
    {
        public long Offset { get; set; }

        internal virtual void Read(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
        }
    }

    public class AesSubsectionEntry : BucketTreeEntry
    {
        public uint Field8 { get; set; }
        public uint Counter { get; set; }

        public AesSubsectionEntry Next { get; set; }
        public long OffsetEnd { get; set; }

        internal override void Read(BinaryReader reader)
        {
            base.Read(reader);
            Field8 = reader.ReadUInt32();
            Counter = reader.ReadUInt32();
        }
    }

    public class RelocationEntry : BucketTreeEntry
    {
        public long PhysicalOffset { get; set; }
        public int SourceIndex { get; set; }

        public RelocationEntry Next { get; set; }
        public long OffsetEnd { get; set; }

        internal override void Read(BinaryReader reader)
        {
            base.Read(reader);
            PhysicalOffset = reader.ReadInt64();
            SourceIndex = reader.ReadInt32();
        }
    }
}