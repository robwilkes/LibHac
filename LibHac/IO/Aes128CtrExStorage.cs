using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.IO
{
    public class Aes128CtrExStorage : Aes128CtrStorage
    {
        private List<AesSubsectionEntry> SubsectionEntries { get; } = new List<AesSubsectionEntry>();
        private List<long> SubsectionOffsets { get; }
        private BucketTree<AesSubsectionEntry> BucketTree { get; }

        public Aes128CtrExStorage(Storage baseStorage, byte[] key, long counterOffset, byte[] ctrHi, BktrPatchInfo bktr, bool keepOpen)
            : base(baseStorage, key, counterOffset, keepOpen, ctrHi)
        {
            BktrHeader header = bktr.EncryptionHeader;
            var headerStorage = new MemoryStorage(header.Header);

            SubStorage bucketTreeStorage =
                new CachedStorage(new Aes128CtrStorage(baseStorage, key, counterOffset, true, ctrHi), 0x4000, 4, false)
                    .Slice(header.Offset, header.Size);

            BucketTree = new BucketTree<AesSubsectionEntry>(headerStorage, bucketTreeStorage);

            foreach (BucketTreeBucket<AesSubsectionEntry> bucket in BucketTree.Buckets)
            {
                SubsectionEntries.AddRange(bucket.Entries);
            }

            // Add a subsection for the BKTR headers to make things easier
            var headerSubsection = new AesSubsectionEntry
            {
                Offset = bktr.RelocationHeader.Offset,
                Counter = (uint)(ctrHi[4] << 24 | ctrHi[5] << 16 | ctrHi[6] << 8 | ctrHi[7]),
                OffsetEnd = long.MaxValue
            };

            SubsectionEntries.Add(headerSubsection);

            for (int i = 0; i < SubsectionEntries.Count - 1; i++)
            {
                SubsectionEntries[i].Next = SubsectionEntries[i + 1];
                SubsectionEntries[i].OffsetEnd = SubsectionEntries[i + 1].Offset;
            }

            SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            AesSubsectionEntry entry = GetSubsectionEntry(offset);
            UpdateCounterSubsection(entry.Counter);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);
                int bytesRead = base.ReadSpan(destination.Slice(outPos, bytesToRead), inPos);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                    UpdateCounterSubsection(entry.Counter);
                }
                else if (bytesRead == 0)
                {
                    break;
                }
            }

            return outPos;
        }

        private AesSubsectionEntry GetSubsectionEntry(long offset)
        {
            int index = SubsectionOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return SubsectionEntries[index];
        }

        private void UpdateCounterSubsection(uint value)
        {
            Counter[7] = (byte)value;
            Counter[6] = (byte)(value >> 8);
            Counter[5] = (byte)(value >> 16);
            Counter[4] = (byte)(value >> 24);
        }
    }
}
