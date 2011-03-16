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

namespace Testing
{
    using System;
    using System.IO;
    using System.Xml.Serialization;
    using Ornl.Csmd.Csrg.Stahc.Core;
    using Ornl.Csmd.Csrg.Stahc.Core.Model;

    class Program
    {
        static void Main(string[] args)
        {
            //int v1 = 17;
            //int v2 = 18;

            //bool result = ((double)v1 / v2) >= 0.8;

            //Console.WriteLine(result);
            //return;

            // generate a large batch of jobs
            StahcJob[] jobs = new StahcJob[1000];

            for (int i = 0; i < jobs.Length; i++)
            {
                var outFile = string.Format("ndvi_{0}.raw", i.ToString("0000"));
                var job = new StahcJob()
                {
                    Executables = new Executeable[] 
                    {   
                        new Executeable() 
                        {  
                            Path = @"jre6\bin\java.exe",
                            Arguments = @"-cp ext-lib\weka.jar;. rMiner.analysis.NDVI -i modis-2008-143 -o " + outFile
                        }
                    },
                    InputFiles = new string[]
                    {
                        "modis-2008-143.aux",
                        "modis-2008-143.hdr",
                        "modis-2008-143.raw",
                    },
                    OutputFiles = new string[]
                    {
                        outFile
                    },
                    FilesToRemove = new String[]
                    {
                        outFile
                    }
                };

                jobs[i] = job;
            }

            
            // test our serialization mapping
            var mainMessage = new StahcManifest()
            {
                AccountName = "govornlcsmdgfdltest",
                AccountKey = "/lCYh9/LpMwiu/1TRvtxWjNmRjcxwKoc9U9CHdMAGn6906D6rYUM8MAE0Mwp26WmaIMJRUbGGxGtGxmIOtb/8Q==",
                PackageFile = @"D:\distribution\aztest\ApplicationWorkerRole.cspkg",
                CertificateFile = @"D:\Workspaces\argodev\Externals\azurecerts\AzureMgmt.cer",
                ContainerName = "bubba08",
                StagingFiles = new string[]
                {
                    @"D:\distribution\aztest\raju.zip",
                    @"D:\distribution\aztest\jre6.zip",
                    @"D:\distribution\aztest\no-frills.exe",
                    @"D:\distribution\aztest\UnzDll.dll",
                    @"D:\distribution\aztest\ZipDll.dll"
                },
                StagingActions = new Executeable[]
                {
                    new Executeable()
                    {
                        Path = "no-frills.exe",
                        Arguments = "raju.zip"
                    },
                    new Executeable()
                    {
                        Path = "no-frills.exe",
                        Arguments = "jre6.zip"
                    }
                },
                DataFiles = new string[]
                {
                    @"D:\distribution\aztest\modis-2008-143.aux",
                    @"D:\distribution\aztest\modis-2008-143.hdr",
                    @"D:\distribution\aztest\modis-2008-143.raw"
                },
                DeploymentSlot = DeploymentSlot.Production,
                SubscriptionId = "5014778d-6130-47b9-966e-709b69f7a76a",
                ServiceName = "ornlldrdusnc",
                OutputLocation = @"D:\distribution\aztest\Output",
                InstanceCount = 1,
                MaxJobLength = 30,
                StachQueueName = "StachJobQueue",
                Messages = jobs
                //Messages = new StahcJob[]
                //{
                //    new StahcJob()
                //    {
                //        Executables = new Executeable[] 
                //        {   
                //            new Executeable() 
                //            {  
                //                Path = @"jre6\bin\java.exe",
                //                Arguments = @"-cp ext-lib\weka.jar;. rMiner.analysis.NDVI -i modis-2008-143 -o ndvi_0000.raw"
                //            }
                //        },
                //        InputFiles = new string[]
                //        {
                //            "modis-2008-143.aux",
                //            "modis-2008-143.hdr",
                //            "modis-2008-143.raw",
                //        },
                //        OutputFiles = new string[]
                //        {
                //            "ndvi_0000.raw"
                //        },
                //        FilesToRemove = new String[]
                //        {
                //            "ndvi_0000.raw"
                //        }
                //    },
                //    new StahcJob()
                //    {
                //        Executables = new Executeable[] 
                //        {   
                //            new Executeable() 
                //            {  
                //                Path = @"jre6\bin\java.exe",
                //                Arguments = @"-cp ext-lib\weka.jar;. rMiner.analysis.NDVI -i modis-2008-143 -o ndvi_0001.raw"
                //            }
                //        },
                //        InputFiles = new String[]
                //        {
                //            "modis-2008-143.aux",
                //            "modis-2008-143.hdr",
                //            "modis-2008-143.raw",
                //        },
                //        OutputFiles = new string[]
                //        {
                //            "ndvi_0001.raw"
                //        },
                //        FilesToRemove = new String[]
                //        {
                //            "ndvi_0001.raw"
                //        }
                //    },
                //    new StahcJob()
                //    {
                //        Executables = new Executeable[] 
                //        {   
                //            new Executeable() 
                //            {  
                //                Path = @"jre6\bin\java.exe",
                //                Arguments = @"-cp ext-lib\weka.jar;. rMiner.analysis.NDVI -i modis-2008-143 -o ndvi_0002.raw"
                //            }
                //        },
                //        InputFiles = new String[]
                //        {
                //            "modis-2008-143.aux",
                //            "modis-2008-143.hdr",
                //            "modis-2008-143.raw",
                //        },
                //        OutputFiles = new string[]
                //        {
                //            "ndvi_0002.raw"
                //        },
                //        FilesToRemove = new String[]
                //        {
                //            "ndvi_0002.raw"
                //        }
                //    },
                //    new StahcJob()
                //    {
                //        Executables = new Executeable[] 
                //        {   
                //            new Executeable() 
                //            {  
                //                Path = @"jre6\bin\java.exe",
                //                Arguments = @"-cp ext-lib\weka.jar;. rMiner.analysis.NDVI -i modis-2008-143 -o ndvi_0003.raw"
                //            }
                //        },
                //        InputFiles = new String[]
                //        {
                //            "modis-2008-143.aux",
                //            "modis-2008-143.hdr",
                //            "modis-2008-143.raw",
                //        },
                //        OutputFiles = new string[]
                //        {
                //            "ndvi_0003.raw"
                //        },
                //        FilesToRemove = new String[]
                //        {
                //            "ndvi_0003.raw"
                //        }
                //    },
                //    new StahcJob()
                //    {
                //        Executables = new Executeable[] 
                //        {   
                //            new Executeable() 
                //            {  
                //                Path = @"jre6\bin\java.exe",
                //                Arguments = @"-cp ext-lib\weka.jar;. rMiner.analysis.NDVI -i modis-2008-143 -o ndvi_0004.raw"
                //            }
                //        },
                //        InputFiles = new String[]
                //        {
                //            "modis-2008-143.aux",
                //            "modis-2008-143.hdr",
                //            "modis-2008-143.raw",
                //        },
                //        OutputFiles = new string[]
                //        {
                //            "ndvi_0004.raw"
                //        },
                //        FilesToRemove = new String[]
                //        {
                //            "ndvi_0004.raw"
                //        }
                //    }
                //}
            };

            XmlSerializer serializer = new XmlSerializer(typeof(StahcManifest));
            using (StringWriter stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, mainMessage);
                // Put message on queue
                Console.WriteLine("Here we go!");
                var blob = stringWriter.ToString();
                Console.WriteLine(blob);
            }

            return;
        }
    }
}
