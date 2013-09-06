using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RDC.Net;
using System.Threading;
using System.Data;
using System.Threading.Tasks;

namespace RDC.Net_Client
{
    class Client
    {
        LimitedConcurrencyLevelTaskScheduler scheduler = new LimitedConcurrencyLevelTaskScheduler(10);
        List<Task> tasks = new List<Task>();
        TaskFactory factory;

        ConcurrentBag<RdcSignature> signatureBag = new ConcurrentBag<RdcSignature>();
        int _chunkBits, _minChunk, _maxChunk, _window;

        public Client(int chunkBits, int minChunk, int maxChunk, int window)
        {
            factory = new TaskFactory(scheduler);
            _chunkBits = chunkBits;
            _minChunk = minChunk;
            _maxChunk = maxChunk;
            _window = window;
        }

        public class IndexComparer : IEqualityComparer<Index>
        {
            public bool Equals(Index x, Index y)
            {
                if (x.Length != y.Length)
                    return false;
                int index = 0;
                int length = x.Hash.Length;
                while (index < length)
                {
                    if (x.Hash[index] != y.Hash[index])
                        return false;
                    index++;
                }
                return true;
            }

            public int GetHashCode(Index obj)
            {
                return obj.Length.GetHashCode();
            }
        }

        public class Index
        {
            public int Length { get; set; }
            public byte[] Hash { get; set; }
        }

        //Return is a length:hash tuple
        public Dictionary<Index, int> Scan(string path)
        {
            Task t = factory.StartNew(() => ScanDirectory(path));
            Thread.Sleep(500);

            //Snooze until tasks are all done
            do
            {
                //Console.WriteLine("{0:G} directories to process", scheduler.TaskCount);
                Thread.Sleep(500);
            } while (scheduler.TaskCount > 0);

            Console.WriteLine("{0:G} directories to process", scheduler.TaskCount);
            Console.WriteLine("Done scanning directories");

            Dictionary<Index, int> indexes = new Dictionary<Index, int>(new IndexComparer());
            foreach (RdcSignature sig in signatureBag)
            {
                Index key = new Index() { Length = sig.Length, Hash = sig.Hash };
                if (!indexes.ContainsKey(key))
                    indexes.Add(key, 0);
                indexes[key]++;
            }

            return indexes;
        }


        private void ScanDirectory(string path)
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                string dir2 = dir;
                tasks.Add(factory.StartNew(() => ScanDirectory(dir2)));
            }

            foreach (string file in Directory.GetFiles(path))
            {
                ScanFile(file);
            }
        }

        private void ScanFile(string path)
        {
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    foreach (RdcSignature sig in Chunking.Chunk(fs, _chunkBits, _minChunk, _maxChunk, _window))
                        signatureBag.Add(sig);
                }
            }
            finally { }
        }

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


    // Provides a task scheduler that ensures a maximum concurrency level while  
    // running on top of the thread pool. 
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        // The list of tasks to be executed  
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks) 

        // The maximum concurrency level allowed by this scheduler.  
        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items.  
        private int _delegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism.  
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        // Queues a task to the scheduler.  
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough  
            // delegates currently queued or running to process tasks, schedule another.  
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler.  
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items. 
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue. 
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed, 
                            // note that we're done processing, and get out. 
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue 
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread 
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        // Attempts to execute the specified task on the current thread.  
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining 
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue 
            if (taskWasPreviouslyQueued)
                // Try to run the task.  
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler.  
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler.  
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler.  
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }

        public int TaskCount
        {
            get
            {
                lock (_tasks)
                {
                    return _tasks.Count;
                }
            }
        }
    }


    class RDCFile
    {
        public Guid ID { get; set; }
        public string Path { get; set; }
        public byte[] Hash { get; set; }
    }

    class RDCFileChunk
    {
        public Guid ID { get; set; }
        public long offset { get; set; }
        public UInt16 Length { get; set; }
        public byte[] Hash { get; set; }
    }
}
