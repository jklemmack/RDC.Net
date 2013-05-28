using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RDC.Net;

namespace RDC.Net_Client
{
    class Client
    {
        public void TestFiles(string seedFile, string sourceFile)
        {
            List<RdcSignature> seedSigs;
            List<RdcSignature> sourceSigs;

            int chunkBits = 13;
            int minChunk = 512;
            int maxChunk = UInt16.MaxValue;
            int window = 100;

            Stopwatch stopwatch = new Stopwatch();
            using (FileStream fs = File.OpenRead(seedFile))
            {
                stopwatch.Start();
                seedSigs = Chunking.Chunk(fs, chunkBits, minChunk, maxChunk, window);
                stopwatch.Stop();
                long sigFileLength = 0;
                using (FileStream fs2 = File.OpenWrite(seedFile + ".sigs"))
                    sigFileLength = SignatureSerializer.Serialize(fs2, seedSigs);
                Console.WriteLine("{4:D4} chunks in {0}ms.  {1:D6} / {2:D10} ({3:p})"
                    , stopwatch.ElapsedMilliseconds, sigFileLength, fs.Length, ((double)sigFileLength) / fs.Length, seedSigs.Count);
            }
            stopwatch.Reset();

            using (FileStream fs = File.OpenRead(sourceFile))
            {
                stopwatch.Start();
                sourceSigs = Chunking.Chunk(fs, chunkBits, minChunk, maxChunk, window);
                stopwatch.Stop();
                long sigFileLength = 0;
                using (FileStream fs2 = File.OpenWrite(sourceFile + ".sigs"))
                    sigFileLength = SignatureSerializer.Serialize(fs2, sourceSigs);
                Console.WriteLine("{4:D4} chunks  in {0}ms.  {1:D6} / {2:D10} ({3:p})"
                    , stopwatch.ElapsedMilliseconds, sigFileLength, fs.Length, ((double)sigFileLength) / fs.Length, sourceSigs.Count);
            }

            RdcSignatureComparer comparer = new RdcSignatureComparer();

            //For each block in the sever file
            stopwatch.Restart();
            Dictionary<int, List<RdcSignature?>> seedLookup = new Dictionary<int, List<RdcSignature?>>();
            foreach (var sig in seedSigs)
            {
                if (!seedLookup.ContainsKey(sig.Length))
                    seedLookup.Add(sig.Length, new List<RdcSignature?>());
                seedLookup[sig.Length].Add(sig);
            }

            List<RdcNeed> needs = new List<RdcNeed>();
            ByteArrayComparer hashComparer = new ByteArrayComparer();
            List<RdcSignature?> sigList = null;
            foreach (var mSig in sourceSigs)
            {
                //See if we have the server sig in our local cache
                if (seedLookup.TryGetValue(mSig.Length, out sigList))
                {
                    RdcSignature? matchedSig = sigList.FirstOrDefault(sig => comparer.Equals(mSig, sig.Value));
                    //We have it!
                    if (matchedSig != null)
                        needs.Add(new RdcNeed() { blockType = RdcNeedType.Seed, length = matchedSig.Value.Length, offset = matchedSig.Value.Offset });
                    else //don't - need it from server
                        needs.Add(new RdcNeed() { blockType = RdcNeedType.Source, length = mSig.Length, offset = mSig.Offset });
                }
                else // don't - need it from server
                    needs.Add(new RdcNeed() { blockType = RdcNeedType.Source, length = mSig.Length, offset = mSig.Offset });

            }
            stopwatch.Stop();

            int haveCount = needs.Count(n => n.blockType == RdcNeedType.Seed);
            int haveSize = needs.Where(n => n.blockType == RdcNeedType.Seed).Sum(n => n.length);
            int needCount = needs.Count(n => n.blockType == RdcNeedType.Source);
            int needSize = needs.Where(n => n.blockType == RdcNeedType.Source).Sum(n => n.length);
            int totalSize = needs.Sum(n => n.length);
            double savings = ((double)haveSize) / ((double)totalSize);

            double chuckSizeAvg = sourceSigs.Average(s => s.Length);
            double chunkSizeMissing = needs.Where(n => n.blockType == RdcNeedType.Source).Average(s => s.length);

            Console.WriteLine("Calculated needs in {0}ms", stopwatch.ElapsedMilliseconds);
            Console.WriteLine("Need {0} of {1} chunks, or {2:N0} of {3:N0} bytes. ({4:P2} savings, {6} chunks, {5:N0} bytes).", needCount, needs.Count, needSize, totalSize, savings, haveSize, needs.Count - needCount);
            Console.WriteLine("Average: {0:N0} (source) / {1:N0} (missing) bytes per chunk", chuckSizeAvg, chunkSizeMissing);

        }
    }
}
