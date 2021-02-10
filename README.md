RDC.Net
=======
This is an impelmentation of _Optimizing File Replication over Limited-Bandwidth Networks 
using Remote Differential Compression_ (see included PDF file).  Using the methods outlined in this paper, incremental 
changes to pairs or collections of files may be identified more efficently, in terms of memory and CPU, than algorithms
such as rsync, bsdiff, etc.  This comes at the sacrifice of somewhat larger "diff" transmissions and a more chatty interface.  
However, the core processing algorithm may be 

Effectively, the algorithm works like this:

* A file to be transmitted is broken into many smaller chunks.  The chunking algoritm is below.
* The client notifies the server of the file to download.
* The server responds with a list of chunks that corresponds to that file.  Each chunk contains the length, offset, and unique hash of the chunk.
* The client scans local files using the same algorithm and builds a library of chunks that it already has, and which are needed from the server.  
* The client transmits the list of required chunks (signature & length) to the server.
* The server responds with a stream containing the requested chunks in order.



The chunking algorithm used is as follows:
* Use a rolling hash function to calculate the checksum of each position in the file.
* For simplicity, MD5 is used, but may be changed so long as both the client and server use the same algorithm.
* Note this checksum is just a hash, not for security purposes, so cryptographic weaknesses are less important.
* Each time the lower N (defined on both client & server) bits of a hash are all 0, mark that as the boundry of a chunk.
* Don't chunk if the chunk size is below a specified threshold (avoids many small chunks).
* Force a chunk if the chunk size is above a specified threshold (avoids single overly large chunk).

