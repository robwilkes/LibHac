using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.IO
{
    public class IndirectStorage : Storage
    {
        private List<RelocationEntry> RelocationEntries { get; } = new List<RelocationEntry>();
        private List<long> RelocationOffsets { get; }

        private List<Storage> Sources { get; } = new List<Storage>();
        private BucketTree<RelocationEntry> BucketTree { get; }

        public IndirectStorage(Storage bucketTreeHeader, Storage bucketTreeData, params Storage[] sources)
        {
            Sources.AddRange(sources);

            BucketTree = new BucketTree<RelocationEntry>(bucketTreeHeader, bucketTreeData);

            foreach (BucketTreeBucket<RelocationEntry> bucket in BucketTree.Buckets)
            {
                RelocationEntries.AddRange(bucket.Entries);
            }

            for (int i = 0; i < RelocationEntries.Count - 1; i++)
            {
                RelocationEntries[i].Next = RelocationEntries[i + 1];
                RelocationEntries[i].OffsetEnd = RelocationEntries[i + 1].Offset;
            }

            RelocationEntries[RelocationEntries.Count - 1].OffsetEnd = BucketTree.BucketOffsets.OffsetEnd;
            RelocationOffsets = RelocationEntries.Select(x => x.Offset).ToList();

            Length = BucketTree.BucketOffsets.OffsetEnd;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            RelocationEntry entry = GetRelocationEntry(offset);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.Offset;

                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);
                int bytesRead = Sources[entry.SourceIndex].Read(destination.Slice(outPos, bytesToRead), entry.PhysicalOffset + entryPos);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                }
                else if (bytesRead == 0)
                {
                    break;
                }
            }

            return outPos;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length { get; }

        private RelocationEntry GetRelocationEntry(long offset)
        {
            int index = RelocationOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return RelocationEntries[index];
        }
    }
}
