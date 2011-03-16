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
    using System.IO;
    using System.IO.Compression;
    using System.Security.Cryptography;
    using System.Text;

    public class Helpers
    {
        public static string GetMD5HashFromFile(string fileName)
        {
            using (FileStream file = new FileStream(fileName, FileMode.Open))
            {
                using (MD5 md5 = new MD5CryptoServiceProvider())
                {
                    byte[] retVal = md5.ComputeHash(file);
                    file.Close();
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < retVal.Length; i++)
                        sb.Append(retVal[i].ToString("x2"));
                    return sb.ToString();
                }
            }
        }

        public static string GetMD5HashFromStream(byte[] data)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                byte[] retVal = md5.ComputeHash(data);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                    sb.Append(retVal[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static byte[] DecompressData(byte[] data)
        {
            byte[] processedData;

            // get a memory stream for the source
            using (Stream sourceStream = new MemoryStream(data))
            {
                // get a memory stream for the target
                using (MemoryStream targetStream = new MemoryStream())
                {
                    int totalRead = 0;

                    // setup the compressor
                    using (GZipStream compressor = new GZipStream(sourceStream, CompressionMode.Decompress))
                    {
                        byte[] buffer = new byte[4096];
                        int numRead = 0;

                        while ((numRead = compressor.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            totalRead += numRead;
                            targetStream.Write(buffer, 0, numRead);
                        }

                    }

                    // now we need to flush the data to our byte buffer
                    processedData = targetStream.ToArray();
                }
            }

            return processedData;
        }

        public static byte[] CompressData(byte[] data)
        {
            byte[] processedData;
            int totalRead = 0;

            // get a memory stream for the source
            using (MemoryStream sourceStream = new MemoryStream(data))
            {
                // get a memory stream for the target
                using (MemoryStream targetStream = new MemoryStream())
                {
                    using (GZipStream compressor = new GZipStream(targetStream,
                            CompressionMode.Compress))
                    {
                        // copy the source file into the compression stream.
                        byte[] buffer = new byte[4096];
                        int numRead = 0;
                        while ((numRead = sourceStream.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            totalRead += numRead;
                            compressor.Write(buffer, 0, numRead);
                        }
                    }

                    // now we need to flush the data to our byte buffer
                    processedData = targetStream.ToArray();
                }
            }

            return processedData;
        }
    }
}