using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RDC.Net
{
    public class Chunking
    {
        public static List<RdcSignature> Chunk(Stream stream)
        {
            RabinRolling impl = new RabinRolling();

            int averageChunkBitLength = 13;
            int minChunk = 1024;
            int maxChunk = UInt16.MaxValue;
            int window = 1024;

            return impl.Chunk(stream, averageChunkBitLength, minChunk, maxChunk, window);
        }

        public static List<RdcSignature> Chunk(Stream stream, int averagChunkBitLength, int minChunk, int maxChunk, int window)
        {
            RabinRolling impl = new RabinRolling();
            return impl.Chunk(stream, averagChunkBitLength, minChunk, maxChunk, window);
        }

        private class RabinRolling
        {

            public static int hconst = 69069; // good hash multiplier for MOD 2^32
            //public static int hconst = 500173;

            public int mult = 1; // this will hold the p^n value
            int[] buffer; // circular buffer - reading from file stream
            int buffptr = 0;
            byte[] hashBuffer;
            int hashLength;
            HashAlgorithm hashAlg;
            static long REPORT_BOUNDRY = 1024 * 1024;

            public List<RdcSignature> Chunk(Stream stream, int averagChunkBitLength, int minChunk, int maxChunk, int window)
            {
                hashAlg = MD5.Create();
                hashBuffer = new byte[maxChunk];
                hashLength = 0;

                int mask = (1 << averagChunkBitLength) - 1;

                List<RdcSignature> signatures = new List<RdcSignature>();
                long length = stream.Length;
                long lastStart = 0;

                // get the initial hash window //
                int hash = inithash(window, stream);
                long position = window; //position starts at window size

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                while (position < length)
                {
                    // chunk boundry is found and chunk is sufficiently large
                    if ((hash & mask) == 0 && hashLength >= minChunk)
                    {
                        lastStart = position;
                        signatures.Add(new RdcSignature(position, (ushort)hashLength, hashAlg.ComputeHash(hashBuffer, 0, hashLength)));
                        hashLength = 0;
                    }
                    else if (position - lastStart + 1 >= maxChunk)   // force a break if the chunk is large enough
                    {
                        lastStart = position;
                        signatures.Add(new RdcSignature(position, (ushort)hashLength, hashAlg.ComputeHash(hashBuffer, 0, hashLength)));
                        hashLength = 0;
                    }

                    // next window's hash  //
                    hash = nexthash(hash, stream);
                    /////////////////////////
                    position++;
                    //if (position % REPORT_BOUNDRY == 0) { 
                    //    Console.WriteLine("{0}ms", stopwatch.ElapsedMilliseconds);
                    //    stopwatch.Restart();
                    //}

                }

                //If we didn't have a break on the last position of the file
                if (hashLength > 0)
                {
                    lastStart = position;
                    signatures.Add(new RdcSignature(position, (ushort)hashLength, hashAlg.ComputeHash(hashBuffer, 0, hashLength)));
                    hashLength = 0;
                }

                return signatures;
            }

            private int nexthash(int prevhash, Stream stream)
            {
                int c = stream.ReadByte(); // next byte from stream

                prevhash -= mult * buffer[buffptr]; // remove the last value
                prevhash *= hconst; // multiply the whole chain with prime
                prevhash += c; // add the new value
                buffer[buffptr] = c; // circular buffer, 1st pos == lastpos
                buffptr = (buffptr + 1) % buffer.Length;
                hashBuffer[hashLength++] = (byte)c;
                return prevhash;
            }

            private int inithash(int length, Stream stream)
            {
                buffer = new int[length]; // create circular buffer

                int hash = 0;

                // calculate the hash sum of p^n * a[x]
                for (int i = 0; i < length; i++)
                {
                    int c = stream.ReadByte();
                    if (c == -1) // file is shorter than the required window size
                        break;

                    // store byte so we can remove it from the hash later
                    buffer[buffptr] = c;
                    buffptr = (buffptr + 1) % buffer.Length;

                    hashBuffer[hashLength++] = (byte)c;

                    hash *= hconst; // multiply the current hash with constant
                    hash += c; // add byte to hash

                    if (i > 0) // calculate the large p^n value for later usage
                        mult *= hconst;
                }

                return hash;
            }
        }

        private class OtherRolling
        {
            /// <summary>
            /// http://haishibai.blogspot.com/2011/03/slicing-files-dynamically-c.html
            /// http://pdos.csail.mit.edu/papers/lbfs:sosp01/lbfs.pdf
            /// http://research.microsoft.com/apps/pubs/default.aspx?id=64692
            /// http://msdn.microsoft.com/en-us/library/dd358117.aspx - Microsoft RDC implementation
            /// </summary>
            /// <param name="s"></param>
            /// <param name="boundary"></param>
            /// <param name="hashAlg"></param>
            /// <returns></returns>
            private List<RdcSignature> Chunk(byte[] s, HashAlgorithm hashAlg, ulong boundary, int windowSize, int minWindow)
            {
                // Optimizations that can be made:
                // stream read the file.  Would need a "look ahead" buffer to be filled, maybe one block at a time
                // instead of casting the bytes of the file to UInt64, read in (up to) 4 bytes at a time
                // allow max window to be > UInt16.MaxValue (why?)

                List<RdcSignature> signatures = new List<RdcSignature>();

                //ulong Q = 100007; //Use a much larger prime number in your code!
                ulong Q = 500000000023;         // Next prime # after 500 billion.  Still fits comfortably in a 64-bit ulong
                ulong D = 256;                  // no clue - hash constant?
                //ulong D = 69069;                  // no clue - hash constant?
                ulong pow = 1;


                for (int k = 1; k < windowSize; k++)
                    pow = (pow * D) % Q;

                ulong sig = 0;
                int lastIndex = 0;

                //Read initial windowSize bytes out of the file and compute the signature
                for (int i = 0; i < windowSize; i++)
                    sig = (sig * D + (ulong)s[i]) % Q;


                for (int j = 1; j <= s.Length - windowSize; j++)
                {
                    // Update the rolling signature
                    sig = (sig + Q - pow * (ulong)s[j - 1] % Q) % Q;
                    sig = (sig * D + (ulong)s[j + windowSize - 1]) % Q;

                    if ((sig & boundary) == 0)  // Do we match the boundry condition?
                    {
                        if (j + 1 - lastIndex >= minWindow)  //and our break is AT LEAST as big as the minimum window size
                        {
                            byte[] hash = hashAlg.ComputeHash(s, lastIndex, j + 1 - lastIndex);
                            signatures.Add(new RdcSignature(lastIndex, (UInt16)(j + 1 - lastIndex), hash));
                            lastIndex = j + 1;
                        }
                    }
                    //  more than 64K without a break, force one so our chunk isn't too large
                    //  also ensures that length will fit in a 16-bit number - UInt16
                    else if (j + 1 - lastIndex == UInt16.MaxValue)  //*should* never be > UInt16.MaxValue
                    {
                        byte[] hash = hashAlg.ComputeHash(s, lastIndex, j + 1 - lastIndex);
                        signatures.Add(new RdcSignature(lastIndex, (UInt16)(j + 1 - lastIndex), hash));
                        lastIndex = j + 1;
                    }
                    else if (j + 1 - lastIndex >= UInt16.MaxValue)
                    {
                        throw new ArgumentOutOfRangeException();
                    }

                }

                //If there are any remaining bytes...
                if (lastIndex < s.Length - 1)
                {
                    byte[] hash = hashAlg.ComputeHash(s, lastIndex, s.Length - lastIndex);
                    signatures.Add(new RdcSignature(lastIndex, (UInt16)(s.Length - lastIndex), hash));
                }
                return signatures;
            }

        }

        private class OtherRolling2
        {
            /// <summary>
            /// http://haishibai.blogspot.com/2011/03/slicing-files-dynamically-c.html
            /// http://pdos.csail.mit.edu/papers/lbfs:sosp01/lbfs.pdf
            /// http://research.microsoft.com/apps/pubs/default.aspx?id=64692
            /// http://msdn.microsoft.com/en-us/library/dd358117.aspx - Microsoft RDC implementation
            /// </summary>
            /// <param name="s"></param>
            /// <param name="boundary"></param>
            /// <param name="hashAlg"></param>
            /// <returns></returns>
            private List<RdcSignature> Chunk(byte[] s, HashAlgorithm hashAlg, ulong boundary, int windowSize, UInt16 minChunkBytes, UInt16 maxChunkBytes)
            {
                // Optimizations that can be made:
                // stream read the file.  Would need a "look ahead" buffer to be filled, maybe one block at a time
                // instead of casting the bytes of the file to UInt64, read in (up to) 4 bytes at a time
                // allow max window to be > UInt16.MaxValue (why?)

                List<RdcSignature> signatures = new List<RdcSignature>();

                //ulong Q = 100007; //Use a much larger prime number in your code!
                ulong Q = 500000000023;         // Next prime # after 500 billion.  Still fits comfortably in a 64-bit ulong
                ulong D = 256;                  // no clue - hash constant?
                //ulong D = 69069;                  // no clue - hash constant?
                ulong pow = 1;

                // Build up the 
                for (int k = 1; k < windowSize; k++)
                    pow = (pow * D) % Q;

                ulong sig = 0;
                int lastIndex = 0;

                //Read initial windowSize bytes out of the file and compute the signature
                for (int i = 0; i < windowSize; i++)
                    sig = (sig * D + (ulong)s[i]) % Q;                      // add head


                for (int j = 1; j <= s.Length - windowSize; j++)
                {
                    // Update the rolling signature
                    sig = (sig + Q - pow * (ulong)s[j - 1] % Q) % Q;        // subtract tail
                    sig = (sig * D + (ulong)s[j + windowSize - 1]) % Q;     // add head

                    if ((sig & boundary) == 0)  // Do we match the boundry condition?
                    {
                        if (j + 1 - lastIndex >= minChunkBytes)  //and our break is AT LEAST as big as the minimum window size
                        {
                            byte[] hash = hashAlg.ComputeHash(s, lastIndex, j + 1 - lastIndex);
                            signatures.Add(new RdcSignature(lastIndex, (UInt16)(j + 1 - lastIndex), hash));
                            lastIndex = j + 1;
                        }
                    }
                    //  more than 64K without a break, force one so our chunk isn't too large
                    //  also ensures that length will fit in a 16-bit number - UInt16
                    else if (j + 1 - lastIndex == UInt16.MaxValue)  //*should* never be > UInt16.MaxValue
                    {
                        byte[] hash = hashAlg.ComputeHash(s, lastIndex, j + 1 - lastIndex);
                        signatures.Add(new RdcSignature(lastIndex, (UInt16)(j + 1 - lastIndex), hash));
                        lastIndex = j + 1;
                    }
                    else if (j + 1 - lastIndex >= UInt16.MaxValue)
                    {
                        throw new ArgumentOutOfRangeException();
                    }

                }

                //If there are any remaining bytes...
                if (lastIndex < s.Length - 1)
                {
                    byte[] hash = hashAlg.ComputeHash(s, lastIndex, s.Length - lastIndex);
                    signatures.Add(new RdcSignature(lastIndex, (UInt16)(s.Length - lastIndex), hash));
                }
                return signatures;
            }

        }

    }
}
