using System;
using System.Collections.Generic;
using System.IO;

namespace RDC.Net
{
    public class SignatureSerializer
    {
        public static long Serialize(Stream s, List<RdcSignature> signatures)
        {
            //First write the hash length
            s.Write(BitConverter.GetBytes(signatures[0].Hash.Length), 0, 4);

            foreach (var sig in signatures)
            {
                s.Write(BitConverter.GetBytes(sig.Length), 0, 2);
                s.Write(sig.Hash, 0, sig.Hash.Length);
            }

            return 4 + signatures.Count * (2 + signatures[0].Hash.Length);
        }

        public static List<RdcSignature> Deserialize(Stream s)
        {
            List<RdcSignature> sigs = new List<RdcSignature>();
            Int64 offset = 0;
            using (BinaryReader br = new BinaryReader(s))
            {
                // read in hash length
                int hashLengh = br.ReadInt32();

                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    UInt16 length = br.ReadUInt16();
                    byte[] hash = br.ReadBytes(hashLengh);
                    sigs.Add(new RdcSignature(offset, length, hash));
                    offset += length;
                }
            }

            return sigs;
        }
    }
}
