using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RDC.Net;

namespace RDC.Net_Client
{
    class Program
    {
        static void Main(string[] args)
        {
            //FullTest();
            Client client = new Client();

            //client.TestFiles("Win2k8 R2 IIS(E).zip", "Win2k8 R2 x64 SQL2k8(D).zip");

            //client.TestFiles("pdf1.pdf", "pdf2.pdf");
            //client.TestFiles("pdf1.pdf", "pdf3.pdf");
            //client.TestFiles("pdf2.pdf", "pdf3.pdf");

            //Console.WriteLine("\n---------------------------------------------------");
            //client.TestFiles("GitExtensions241SetupComplete.msi", "GitExtensions244SetupComplete.msi");

            ////Console.WriteLine("\n---------------------------------------------------");
            ////client.TestFiles("GitExtensions241SetupComplete.msi.sigs", "GitExtensions244SetupComplete.msi.sigs");

            ////Console.WriteLine("\n---------------------------------------------------");
            ////client.TestFiles("GitExtensions241SetupComplete.msi.sigs.sigs", "GitExtensions244SetupComplete.msi.sigs.sigs");

            //Console.WriteLine("\n---------------------------------------------------");
            //client.TestFiles("GitExtensions243Setup.msi", "GitExtensions244Setup.msi");

            //Console.WriteLine("\n---------------------------------------------------");
            //client.TestFiles("GitExtensions241SetupComplete.msi", "GitExtensions244SetupComplete.msi");

            //Console.WriteLine("\n---------------------------------------------------");
            client.TestFiles("SQLEXPR_x86_ENU.exe", "SQLEXPR_x64_ENU.exe");

            Console.ReadLine();
        }

        //private static void FullTest()
        //{
        //    List<RdcSignature> seedSigs;
        //    List<RdcSignature> sourceSigs;

        //    //string seedFile = "ClosedXML_v0.68.0.zip";
        //    //string sourceFile = "ClosedXML_v0.68.1.0.zip";

        //    string seedFile = "a.pdf";      //on client
        //    string sourceFile = "b.pdf";    //on server

        //    //string seedFile = "GitExtensions241SetupComplete.msi";
        //    //string sourceFile = "GitExtensions244SetupComplete.msi";

        //    //string seedFile = "GitExtensions243Setup.msi";
        //    //string sourceFile = "GitExtensions244Setup.msi";

        //    //string seedFile = "SCA Pricing Proposal - JK.pdf";
        //    //string sourceFile = "SCA Pricing Proposal - JK v2.pdf";


        //    //string targetFile = "out.pdf";  // final target;
        //    Stopwatch stopwatch = new Stopwatch();

        //    stopwatch.Start();
        //    long length = 0;
        //    using (FileStream fs = File.OpenRead(seedFile))
        //    {
        //        seedSigs = new Chunking().Chunk(fs, 8192, 1024, UInt16.MaxValue, 1024);

        //        using (FileStream outFile = new FileStream("seed.sig", FileMode.Create))
        //            SignatureSerializer.Serialize(outFile, seedSigs);

        //        length = fs.Length;
        //    }
        //    stopwatch.Stop();
        //    Console.WriteLine("Processed {0:N1} bytes in {1}ms", length, stopwatch.ElapsedMilliseconds);

        //    stopwatch.Start();
        //    using (FileStream fs = File.OpenRead(sourceFile))
        //    {
        //        //byte[] fileBytes = new byte[fs.Length];
        //        //fs.Read(fileBytes, 0, (int)fs.Length);
        //        //sourceSigs = Chunking.Chunk(fileBytes);
        //        sourceSigs = new Chunking().Chunk(fs, 8192, 1024, UInt16.MaxValue, 1024);


        //        using (FileStream outFile = new FileStream("source.sig", FileMode.Create))
        //            SignatureSerializer.Serialize(outFile, sourceSigs);

        //        length = fs.Length;
        //    }
        //    Console.WriteLine("Processed {0:N1} bytes in {1}ms", length, stopwatch.ElapsedMilliseconds);

        //    RdcSignatureComparer comparer = new RdcSignatureComparer();

        //    //For each block in the sever file
        //    List<RdcNeed> needs = new List<RdcNeed>();
        //    foreach (var mSig in sourceSigs)
        //    {
        //        RdcSignature matchedSig = seedSigs.FirstOrDefault(sig => comparer.Equals(mSig, sig));

        //        //See if the local file has this chunk
        //        if (!seedSigs.Contains(mSig, comparer))
        //        {
        //            needs.Add(new RdcNeed() { blockType = RdcNeedType.Source, length = mSig.Length, offset = mSig.Offset });
        //        }
        //        else
        //        {
        //            needs.Add(new RdcNeed() { blockType = RdcNeedType.Seed, length = matchedSig.Length, offset = matchedSig.Offset });
        //        }
        //    }

        //    //Console.WriteLine("Need {0} of {1}.  Savings is {2:N0} bytes, or {3:p2}.", needCount, sourceSigs.Count, savings, ((double)(sourceSigs.Count - needCount)) / sourceSigs.Count);

        //    int haveCount = needs.Count(n => n.blockType == RdcNeedType.Seed);
        //    int haveSize = needs.Where(n => n.blockType == RdcNeedType.Seed).Sum(n => n.length);
        //    int needCount = needs.Count(n => n.blockType == RdcNeedType.Source);
        //    int needSize = needs.Where(n => n.blockType == RdcNeedType.Source).Sum(n => n.length);
        //    int totalSize = needs.Sum(n => n.length);
        //    double savings = ((double)haveSize) / ((double)totalSize);

        //    double chuckSizeAvg = sourceSigs.Average(s => s.Length);
        //    double chunkSizeMissing = needs.Where(n => n.blockType == RdcNeedType.Source).Average(s => s.length);

        //    Console.WriteLine("Need {0} of {1} chunks, or {2:N0} of {3:N0} bytes. ({4:P0} savings).", needCount, needs.Count, needSize, totalSize, savings);
        //    Console.WriteLine("Average: {0:N0} (source) / {1:N0} (missing) bytes per chunk", chuckSizeAvg, chunkSizeMissing);

        //    int remote = needs.Count(s => s.blockType == RdcNeedType.Source);

        //    //  Build up the target file!
        //    string outFileName = Path.GetFileNameWithoutExtension(seedFile) + " - target" + Path.GetExtension(seedFile);

        //    using (FileStream fs = File.OpenWrite(outFileName))
        //    {
        //        foreach (var need in needs)
        //        {
        //            if (need.blockType == RdcNeedType.Seed)
        //                fs.Write(GetChunk(seedFile, need.offset, need.length), 0, need.length);
        //            else if (need.blockType == RdcNeedType.Source)
        //                fs.Write(GetChunk(sourceFile, need.offset, need.length), 0, need.length);
        //        }
        //    }

        //    Console.ReadLine();
        //}

        public static byte[] GetChunk(string file, long offset, int length)
        {
            byte[] output = new byte[length];
            using (FileStream fs = File.OpenRead(file))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Read(output, 0, length);
            }
            return output;
        }
    }
}
