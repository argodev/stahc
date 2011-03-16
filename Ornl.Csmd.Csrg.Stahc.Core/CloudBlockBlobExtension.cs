//--------------------------------------------------------------------------------------------------
// <author>Jonathan Rann, Rob Gillen</author>
// <authorEmail>jcrann84@gmail.com, gillenre@ornl.gov</authorEmail>
// <remarks>
// </remarks>
// <copyright file="Program.cs" company="Oak Ridge National Laboratory">
//
//   Copyright (c) 2011 Oak Ridge National Laboratory, unless otherwise noted.  
//
//   Permission is hereby granted, free of charge, to any person obtaining a copy
//   of this software and associated documentation files (the "Software"), to deal
//   in the Software without restriction, including without limitation the rights
//   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//   copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions:
//
//   The above copyright notice and this permission notice shall be included in all
//   copies or substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//   SOFTWARE.
//
// </copyright>
//--------------------------------------------------------------------------------------------------

namespace Ornl.Csmd.Csrg.Stahc.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.StorageClient;
    using Ornl.Csmd.Csrg.Stahc.Core.Model;

    public static class CloudBlockBlobExtension
    {
        /// <summary>
        /// Uploads a file from the file system to a blob. Parallel implementation of
        /// UploadFile().
        /// </summary>
        /// <param name="blob">Blob object that is extended by this method</param>
        /// <param name="fileName">The file to be uploaded.</param>
        /// <param name="options">A Microsoft.WindowsAzure.StorageClient.BlobRequestOptions object indicating any addtional options to be specified on the request</param>
        /// <param name="maxBlockSize">The maximum size of an individual block transferred</param>
        public static void ParallelUploadFile(this CloudBlockBlob blob, string fileName, BlobRequestOptions options, int maxBlockSize)
        {
            var file = new FileInfo(fileName);
            long fileSize = file.Length;

            // let's figure out how big the file is here
            long leftToRead = fileSize;
            int startPosition = 0;

            // have 1 block for every maxBlockSize bytes plus 1 for the remainder
            var blockCount =
                ((int)Math.Floor((double)(fileSize / maxBlockSize))) + 1;

            // setup the control array
            BlockTransferDetail[] transferDetails = new BlockTransferDetail[blockCount];

            // create an array of block keys
            string[] blockKeys = new string[blockCount];
            var blockIds = new List<string>();


            // populate the control array...
            for (int j = 0; j < transferDetails.Length; j++)
            {
                int toRead = (int)(maxBlockSize < leftToRead ?
                    maxBlockSize :
                    leftToRead);

                string blockId = Convert.ToBase64String(
                        ASCIIEncoding.ASCII.GetBytes(
                        string.Format("Block{0}Stop", j.ToString("00000000000"))));

                //Console.WriteLine(blockId);
                transferDetails[j] = new BlockTransferDetail()
                {
                    StartPosition = startPosition,
                    BytesToRead = toRead,
                    BlockId = blockId
                };

                if (toRead > 0)
                {
                    blockIds.Add(blockId);
                }

                // increment the starting position
                startPosition += toRead;
                leftToRead -= toRead;
            }

            // now we do a || upload of the file.
            var result = Parallel.For(0, transferDetails.Length, j =>
            {
                using (FileStream fs = new FileStream(file.FullName,
                    FileMode.Open, FileAccess.Read))
                {
                    byte[] buff = new byte[transferDetails[j].BytesToRead];
                    BinaryReader br = new BinaryReader(fs);

                    // move the file system reader to the proper position
                    fs.Seek(transferDetails[j].StartPosition, SeekOrigin.Begin);
                    br.Read(buff, 0, transferDetails[j].BytesToRead);

                    if (buff.Length > 0)
                    {
                        // calculate the block-level hash
                        string blockHash = Helpers.GetMD5HashFromStream(buff);

                        //blob.PutBlock(transferDetails[j].BlockId, new MemoryStream(buff), blockHash, options);
                        blob.PutBlock(transferDetails[j].BlockId, new MemoryStream(buff), null, options);
                    }
                }
            });

            // commit the file
            blob.PutBlockList(blockIds);
        }


        public static void ParallelDownloadToFile(this CloudBlockBlob blob, string fileName, int maxBlockSize)
        {
            // refresh the values
            blob.FetchAttributes();

            long fileSize = blob.Attributes.Properties.Length;
            var filePath = Path.GetDirectoryName(fileName);
            var fileNameWithoutPath = Path.GetFileNameWithoutExtension(fileName);

            // let's figure out how big the file is here
            long leftToRead = fileSize;
            int startPosition = 0;

            // have 1 block for every maxBlockSize bytes plus 1 for the remainder
            var blockCount =
                ((int)Math.Floor((double)(fileSize / maxBlockSize))) + 1;

            // setup the control array
            BlockTransferDetail[] transferDetails = new BlockTransferDetail[blockCount];

            // create an array of block keys
            string[] blockKeys = new string[blockCount];
            var blockIds = new List<string>();

            // populate the control array...
            for (int j = 0; j < transferDetails.Length; j++)
            {
                int toRead = (int)(maxBlockSize < leftToRead ?
                    maxBlockSize :
                    leftToRead);

                string blockId = Path.Combine(filePath, 
                    string.Format("{0}_{1}.dat",
                    fileNameWithoutPath,
                    j.ToString("00000000000")));

                transferDetails[j] = new BlockTransferDetail()
                {
                    StartPosition = startPosition,
                    BytesToRead = toRead,
                    BlockId = blockId
                };

                if (toRead > 0)
                {
                    blockIds.Add(blockId);
                }

                // increment the starting position
                startPosition += toRead;
                leftToRead -= toRead;
            }

            // now we do a || download of the file.
            var result = Parallel.For(0, transferDetails.Length, j =>
            {
                // get the blob as a stream
                using (BlobStream stream = blob.OpenRead())
                {
                    // move to the proper location
                    stream.Seek(transferDetails[j].StartPosition, SeekOrigin.Begin);

                    // setup a buffer with the proper size
                    byte[] buff = new byte[transferDetails[j].BytesToRead];

                    // read into the buffer
                    stream.Read(buff, 0, transferDetails[j].BytesToRead);

                    // flush the buffer to disk
                    using (Stream fileStream = new FileStream(transferDetails[j].BlockId, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (BinaryWriter bw = new BinaryWriter(fileStream))
                        {
                            bw.Write(buff);
                            bw.Close();
                        }
                    }

                    buff = null;
                }
            });

            // assemble the file into one now...
            using (Stream fileStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                using (BinaryWriter bw = new BinaryWriter(fileStream))
                {
                    // loop through each of the files on the disk
                    for (int j = 0; j < transferDetails.Length; j++)
                    {
                        // read them into the file (append)
                        bw.Write(File.ReadAllBytes(transferDetails[j].BlockId));

                        // and then delete them
                        File.Delete(transferDetails[j].BlockId);
                    }
                }
            }

            transferDetails = null;
        }

            // TEST: non-parallel version of the blocked download
            //for(int j = 0; j < transferDetails.Length; j++)
            //{
            //    // get the blob as a stream
            //    using (BlobStream stream = blob.OpenRead())
            //    {
            //        // move to the proper location
            //        stream.Seek(transferDetails[j].StartPosition, SeekOrigin.Begin);

            //        // setup a buffer with the proper size
            //        byte[] buff = new byte[transferDetails[j].BytesToRead];

            //        // read into the buffer
            //        stream.Read(buff, 0, transferDetails[j].BytesToRead);

            //        // flush the buffer to disk
            //        FileInfo f = new FileInfo(transferDetails[j].BlockId);
            //        BinaryWriter bw = new BinaryWriter(f.OpenWrite());
            //        bw.Write(buff);
            //        bw.Close();
            //    }
            //}

            // commit the file
            //blob.PutBlockList(blockIds);
        //}
    }
}