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

namespace Ornl.Csmd.Csrg.Stahc.Core.Model
{
    using System;
    using System.Xml.Serialization;

    [Serializable()]
    public class StahcManifest
    {
        public string AccountName { get; set; }
        public string AccountKey { get; set; }
        public string PackageFile { get; set; }
        public string CertificateFile { get; set; }
        public string ContainerName { get; set; }

        [XmlArrayItem(ElementName = "StagingFile", NestingLevel = 0)]
        public string[] StagingFiles { get; set; }

        [XmlArrayItem(ElementName = "StagingAction", NestingLevel = 0)]
        public Executeable[] StagingActions { get; set; }

        [XmlArrayItem(ElementName = "DataFile", NestingLevel = 0)]
        public string[] DataFiles { get; set; }
        public DeploymentSlot DeploymentSlot { get; set; }
        public string SubscriptionId { get; set; }
        public string ServiceName { get; set; }
        public string OutputLocation { get; set; }
        public int InstanceCount { get; set; }
        public int MaxJobLength { get; set; }
        public string StachQueueName { get; set; }
        public StahcJob[] Messages { get; set; }

        // defaults
        public string DeploymentName { get; set; }
        public string ConfigurationLabel { get; set; }
        public int QueueSleepTime { get; set; }

        public StahcManifest()
        {
            // initialize the defaults
            DeploymentName = "STAHCDeployment";
            ConfigurationLabel = string.Format("STAHC Deployment {0}", DateTime.Now);
            QueueSleepTime = 60;
        }
    }
}