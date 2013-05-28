using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using System.Text;
using System.Threading.Tasks;

namespace RDC.Net
{
    [Serializable]
    public struct RdcSignature
    {
        public Int64 Offset;        // 4
        public UInt16 Length;       // 2
        public byte[] Hash;         // 16 typically

        public RdcSignature(Int64 offset, UInt16 length, byte[] hash)
        {
            Offset = offset;
            Length = length;
            Hash = new byte[hash.Length];
            Array.Copy(hash, Hash, hash.Length);
        }
    }

    public enum RdcNeedType
    {
        Source = 0, // client file
        Seed,       // server file
        Target      // output file

    }

    public struct RdcNeed
    {
        public RdcNeedType blockType;
        public Int64 offset;
        public UInt16 length;
    }
   

    public class RdcSignatureComparer : IEqualityComparer<RdcSignature>
    {
        private static ByteArrayComparer comparer = new ByteArrayComparer();

        public bool Equals(RdcSignature x, RdcSignature y)
        {
            return comparer.Equals(x.Hash, y.Hash);
        }

        public int GetHashCode(RdcSignature obj)
        {
            return obj.GetHashCode();
        }
    }

}
