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

namespace StahcRunner
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Xml.Serialization;
    using CommandLine;
    using Ornl.Csmd.Csrg.Stahc.Core;
    using Ornl.Csmd.Csrg.Stahc.Core.Model;

    /// <summary>
    /// 
    /// </summary>
    public class Program
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        public static void Main(string[] args)
        {
            WriteStatusLine(string.Empty);
            WriteStatusLine("Scientific Tool for Applications Harnessing the Cloud (STAHC)");
            WriteStatusLine(string.Format("Operation started at: {0}", DateTime.Now));
            WriteStatusLine(string.Empty);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // manifest object holds the parameters to control most of what happens within the
            // application
            StahcManifest manifest = null;

            // update the TCP/IP connection stack to let us multi-thread more...
            ServicePointManager.DefaultConnectionLimit = 64;

            var options = new CommandLineOptions();
            var parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));

            if (!parser.ParseArguments(args, options))
            {
                WriteStatusLine(string.Empty);
                Environment.Exit(1);
            }

            if (File.Exists(options.XmlSettingsFile))
            {
                manifest = LoadSettingsFromXml(options.XmlSettingsFile);

                if (manifest == null)
                {
                    WriteStatusLine(string.Empty);
                    Environment.Exit(1);
                }
            }
            else
            {
                WriteError("\nUnable to locate the xml settings file specified");
                WriteStatusLine(string.Empty);
                Environment.Exit(1);
            }

            // perform additional validation steps
            if (!SecondaryValidation(options.Operation, manifest))
            {
                WriteStatusLine(string.Empty);
                Environment.Exit(1);
            }

            // now, let's perform the correct operations based on what the user asked to do
            switch (options.Operation)
            {
                case ControlOperation.StageFiles:
                    UploadFiles(manifest);
                    break;

                case ControlOperation.Deploy:
                    var stahcPackageUrl = UploadFiles(manifest);
                    CreateDeployment(manifest, stahcPackageUrl);
                    StartDeployment(manifest);
                    GenerateQueueMessages(manifest);
                    WaitForDeploymentStarted(manifest);
                    MonitorQueue(manifest);
                    break;

                case ControlOperation.RetrieveOutput:
                    DownloadOutputFiles(manifest);
                    break;

                case ControlOperation.CleanUp:
                    StopDeployment(manifest);
                    WaitForDeploymentStopped(manifest);
                    DeleteDeployment(manifest);
                    break;

                case ControlOperation.CleanUpAll:
                    StopDeployment(manifest);
                    WaitForDeploymentStopped(manifest);
                    DeleteDeployment(manifest);
                    // remove files from blob storage
                    break;

                case ControlOperation.FullTest:
                    var packageUrl = UploadFiles(manifest);
                    CreateDeployment(manifest, packageUrl);
                    StartDeployment(manifest);
                    GenerateQueueMessages(manifest);
                    WaitForDeploymentStarted(manifest);
                    var computeWatch = new Stopwatch();
                    computeWatch.Start();
                    WriteStatusLine(string.Empty);
                    WriteStatusLine(string.Format("Compute Started at {0}", DateTime.Now));
                    WriteStatusLine(string.Empty);
                    MonitorQueue(manifest);
                    computeWatch.Stop();
                    WriteStatusLine(string.Empty);
                    WriteStatusLine(string.Format("Compute completed. {0} elapsed", computeWatch.Elapsed));
                    WriteStatusLine(string.Empty);
                    DownloadOutputFiles(manifest);
                    StopDeployment(manifest);
                    WaitForDeploymentStopped(manifest);
                    DeleteDeployment(manifest);
                    break;

                case ControlOperation.ListServices:
                    ListHostedServices(manifest);
                    break;

                case ControlOperation.GetDeployment:
                    GetDeployment(manifest);
                    break;

                case ControlOperation.ListServiceProperties:
                    GetHostedServicesProperties(manifest);
                    break;
            }

            // finish up and close nicely
            WriteStatusLine(string.Empty);
            stopwatch.Stop();
            WriteStatusLine(
                string.Format(
                "Operation Completed. Elapsed Time: {0}",
                stopwatch.Elapsed));
            WriteStatusLine("press any key to quit...");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void GenerateQueueMessages(StahcManifest manifest)
        {
            WriteStatus("Loading Job Queue...");

            for (int i = 0; i < manifest.Messages.Length; i++)
            {
                Utilities.CreateQueueMessage(
                    manifest.AccountName,
                    manifest.AccountKey,
                    manifest.StachQueueName,
                    manifest.Messages[i].Executables,
                    manifest.Messages[i].InputFiles,
                    manifest.Messages[i].OutputFiles,
                    manifest.Messages[i].FilesToRemove);

                WriteStatus(".");
            }

            WriteStatusLine(" Done");
        }

        /// <summary>
        /// Simple utility function to write an error message to the screen. The color of the text
        /// is set to Red to help highlight it. After the message is written, the colors are reset
        /// to their defaults.
        /// </summary>
        /// <param name="errorMessage"></param>
        private static void WriteError(string errorMessage)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMessage);
            Console.ResetColor();
        }

        private static void WriteStatusLine(string message)
        {
            Console.WriteLine(message);
        }

        private static void WriteStatus(string message)
        {
            Console.Write(message);
        }

        /// <summary>
        /// Provides secondary validation based on the operation specified. 
        /// </summary>
        /// <param name="operation">The operation the user wants to perform</param>
        /// <param name="manifest">The StachManifest object populated with the supplied values</param>
        /// <returns>True if all validation operations were successful. False otherwise.</returns>
        private static bool SecondaryValidation(ControlOperation operation, StahcManifest manifest)
        {
            // assume we are ready to go
            bool allGood = true;

            // perform validation based on the operation selected
            switch (operation)
            {
                case ControlOperation.StageFiles:
                case ControlOperation.Deploy:

                    // make sure the package file exists
                    if (!File.Exists(manifest.PackageFile))
                    {
                        WriteError("\nCannot locate the package file specified");
                        allGood = false;
                    }

                    // make sure that the certificate file exists
                    if (!File.Exists(manifest.CertificateFile))
                    {
                        WriteError("\nCannot locate the certificate file specified");
                        allGood = false;
                    }

                    // ensure that all of the staging files specified exist
                    if ((manifest.StagingFiles != null) && (manifest.StagingFiles.Length > 0))
                    {
                        for (int i = 0; i < manifest.StagingFiles.Length; i++)
                        {
                            if (!File.Exists(manifest.StagingFiles[i]))
                            {
                                WriteError(String.Format(
                                    "\nCannot locate staging file: {0}",
                                    manifest.StagingFiles[i]));
                                allGood = false;
                            }
                        }
                    }

                    // ensure that all of the data files specified exist
                    if ((manifest.DataFiles != null) && (manifest.DataFiles.Length > 0))
                    {
                        for (int i = 0; i < manifest.DataFiles.Length; i++)
                        {
                            if (!File.Exists(manifest.DataFiles[i]))
                            {
                                WriteError(String.Format(
                                    "\nCannot locate data file: {0}",
                                    manifest.DataFiles[i]));
                                allGood = false;
                            }
                        }
                    }

                    break;

                case ControlOperation.RetrieveOutput:
                    break;

                case ControlOperation.CleanUp:

                    // make sure the package file exists
                    if (!File.Exists(manifest.PackageFile))
                    {
                        WriteError("\nCannot locate the package file specified");
                        allGood = false;
                    }

                    // make sure that the certificate file exists
                    if (!File.Exists(manifest.CertificateFile))
                    {
                        WriteError("\nCannot locate the certificate file specified");
                        allGood = false;
                    }

                    break;

                case ControlOperation.FullTest:

                    // make sure the package file exists
                    if (!File.Exists(manifest.PackageFile))
                    {
                        WriteError("\nCannot locate the package file specified");
                        allGood = false;
                    }

                    // make sure that the certificate file exists
                    if (!File.Exists(manifest.CertificateFile))
                    {
                        WriteError("\nCannot locate the certificate file specified");
                        allGood = false;
                    }

                    break;
            }

            // let the caller know how we did
            return allGood;
        }

        private static string UploadFiles(StahcManifest manifest)
        {
            // 1 Upload STAHC files
            WriteStatus("Uploading STAHC Files...");

            // get the package path name
            var remotePackageKey = string.Format(
                "{0}/{1}",
                Constants.StackFilesPath,
                Path.GetFileName(manifest.PackageFile));

            var remotePackagePath = Utilities.UploadFileToCloud(
                manifest.AccountName,
                manifest.AccountKey,
                manifest.ContainerName,
                remotePackageKey,
                manifest.PackageFile);

            WriteStatusLine(" Done");

            // 2 Upload Staging Files (if any)
            if ((manifest.StagingFiles != null) && (manifest.StagingFiles.Length > 0))
            {
                WriteStatus("Uploading Staging Files...");

                for (int i = 0; i < manifest.StagingFiles.Length; i++)
                {
                    Utilities.UploadFileToCloud(
                        manifest.AccountName,
                        manifest.AccountKey,
                        manifest.ContainerName,
                        string.Format(
                            "{0}/{1}",
                            Constants.StagingFilesPath,
                            Path.GetFileName(manifest.StagingFiles[i])),
                        manifest.StagingFiles[i]);

                    WriteStatus(".");
                }

                WriteStatusLine(" Done");
            }

            // 2.1 Generate the staging operations file if necessary and upload it
            if ((manifest.StagingActions != null) && (manifest.StagingActions.Length > 0))
            {
                WriteStatus("Confiuring Staging Operations...");

                // we can serialize the blob
                XmlSerializer serializer = new XmlSerializer(typeof(Executeable[]));
                using (StringWriter stringWriter = new StringWriter())
                {
                    serializer.Serialize(stringWriter, manifest.StagingActions);
                    WriteStatus(".");
                    // upload the xml
                    Utilities.UploadStringToCloud(
                        manifest.AccountName, 
                        manifest.AccountKey, 
                        manifest.ContainerName, 
                        string.Format("{0}/{1}", Constants.StagingFilesPath, "stach_staging_actions.xml"), 
                        stringWriter.ToString());
                }

                WriteStatusLine(" Done");
            }

            // 3 Upload Data Files (if any)
            if ((manifest.DataFiles != null) && (manifest.DataFiles.Length > 0))
            {
                WriteStatus("Uploading Data Files...");

                for (int i = 0; i < manifest.DataFiles.Length; i++)
                {
                    Utilities.UploadFileToCloud(
                        manifest.AccountName,
                        manifest.AccountKey,
                        manifest.ContainerName,
                        string.Format("{0}/{1}",
                            Constants.DataFilesPath,
                            Path.GetFileName(manifest.DataFiles[i])),
                        manifest.DataFiles[i]);

                    WriteStatus(".");
                }

                WriteStatusLine(" Done");
            }

            return remotePackagePath;
        }

        private static void CreateDeployment(StahcManifest manifest, string packageUrl)
        {
            WriteStatus("Creating Deployment...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the CreateDeployment request and retrieve the requestID so we can
                // keep tabs on how it is going...
                var requestId = Utilities.CreateDeployment(
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentSlot.ToString(),
                    manifest.DeploymentName,
                    packageUrl,
                    manifest.ConfigurationLabel,
                    manifest.InstanceCount,
                    manifest.MaxJobLength,
                    manifest.AccountName,
                    manifest.AccountKey,
                    manifest.ContainerName,
                    manifest.QueueSleepTime,
                    manifest.StachQueueName,
                    managementCertificate);

                // monitor the async request and let the user know how things are going
                MonitorAsyncRequest(requestId, manifest.SubscriptionId, managementCertificate);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void StartDeployment(StahcManifest manifest)
        {
            WriteStatus("Starting Deployment...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the UpdateDeployment request and retrieve the requestID so we can
                // keep tabs on how it is going...
                var requestId = Utilities.UpdateDeployment(
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentName,
                    DeploymentStatus.Running,
                    managementCertificate);

                // monitor the async request and let the user know how things are going
                MonitorAsyncRequest(requestId, manifest.SubscriptionId, managementCertificate);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void StopDeployment(StahcManifest manifest)
        {
            WriteStatus("Stopping Deployment...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the UpdateDeployment request and retrieve the requestID so we can
                // keep tabs on how it is going...
                var requestId = Utilities.UpdateDeployment(
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentName,
                    DeploymentStatus.Suspended,
                    managementCertificate);

                // monitor the async request and let the user know how things are going
                MonitorAsyncRequest(requestId, manifest.SubscriptionId, managementCertificate);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void DeleteDeployment(StahcManifest manifest)
        {
            WriteStatus("Deleting Deployment...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the DeleteDeployment request and retrieve the requestID so we can
                // keep tabs on how it is going...
                var requestId = Utilities.DeleteDeployment(
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentName,
                    managementCertificate);

                // monitor the async request and let the user know how things are going
                MonitorAsyncRequest(requestId, manifest.SubscriptionId, managementCertificate);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void ListHostedServices(StahcManifest manifest)
        {
            WriteStatusLine("Listing Hosted Services...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the ListHostedServices request
                var services = Utilities.ListHostedServices(
                    manifest.SubscriptionId,
                    managementCertificate);

                foreach (HostedService service in services)
                {
                    WriteStatusLine(string.Format("\tName:\t{0}", service.ServiceName));
                    WriteStatusLine(string.Format("\tUrl:\t{0}\n", service.Url));
                }
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void DownloadOutputFiles(StahcManifest manifest)
        {
            WriteStatusLine("Retrieving Output Files...");

            Utilities stachCore = new Utilities();
            stachCore.FileDownloaded += stachCore_FileDownloaded;

            stachCore.DownloadFolderFiles(
                manifest.AccountName,
                manifest.AccountKey,
                manifest.ContainerName,
                "output",
                manifest.OutputLocation);

            WriteStatusLine(" Done");
        }

        private static void stachCore_FileDownloaded(object sender, StachFileDownloadEventArgs e)
        {
            WriteStatusLine(string.Format("\t{0}", e.FileName));
        }

        private static void GetHostedServicesProperties(StahcManifest manifest)
        {
            WriteStatusLine("Getting Hosted Service Properties...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the ListHostedServices request
                Utilities.GetHostedServicesProperties(
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    managementCertificate);

                //foreach (HostedService service in services)
                //{
                //    WriteStatusLine(string.Format("\tName:\t{0}", service.ServiceName));
                //    WriteStatusLine(string.Format("\tUrl:\t{0}\n", service.Url));
                //}
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }

            //// commandline parameters
            //string subscriptionID = commandLine["s"];
            //string serviceName = commandLine["sn"];

            //// URI FORMAT:https://management.core.windows.net/<subscription-id>/services/hostedservices/<service-name>

            //// Build url string
            //StringBuilder str = new StringBuilder();
            //str.Append("https://management.core.windows.net/");
            //str.AppendFormat("{0}", subscriptionID);
            //str.Append("/services/hostedservices/");
            //str.AppendFormat("{0}", serviceName);

            //// convert to string
            //string uri = str.ToString();

            //// make uri request using created uri string
            //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            //// get certificates
            //X509Certificate2 accessCertificate = GetManagementCertificate(commandLine);

            //// header info
            //request.Method = "GET";
            //request.ClientCertificates.Add(accessCertificate);
            //request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            //request.ContentType = Constants.ContentTypeXml;

            //try
            //{
            //    // Get the response
            //    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            //    // Display the status
            //    Console.WriteLine("\n...Getting Hosted Services...\n");
            //    Console.WriteLine(new StreamReader(response.GetResponseStream()).ReadToEnd());
            //}
            //catch (TimeoutException)
            //{
            //    Console.ForegroundColor = ConsoleColor.Red;
            //    Console.WriteLine("ERROR: Operation timed out, please try again...");
            //    Console.ForegroundColor = ConsoleColor.Gray;
            //    System.Environment.Exit(1);
            //}
            //catch
            //{
            //    Console.ForegroundColor = ConsoleColor.Red;
            //    Console.WriteLine("\nUnable to get hosted services, please check your input values and try again...");
            //    Console.ForegroundColor = ConsoleColor.Gray;
            //    System.Environment.Exit(1);
            //}
        }

        //// Enumerate Deployments 
        //// // // // // // 
        //private static void EnumerateDeployments(Arguments commandLine)
        //{
        //    // commandline parameters
        //    string subscriptionID = commandLine["s"];
        //    string serviceName = commandLine["sn"];

        //    // URI FORMAT:https://management.core.windows.net/<subscription-id>/services/hostedservices/<service-name>?embed-detail=true

        //    // Build url string
        //    StringBuilder str = new StringBuilder();
        //    str.Append("https://management.core.windows.net/");
        //    str.AppendFormat("{0}", subscriptionID);
        //    str.Append("/services/hostedservices/");
        //    str.AppendFormat("{0}", serviceName);
        //    str.Append("?embed-detail=true");

        //    // convert to string
        //    string uri = str.ToString();

        //    // make uri request using created uri string
        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

        //    // get certificates
        //    X509Certificate2 accessCertificate = GetManagementCertificate(commandLine);

        //    // header info
        //    request.Method = "GET";
        //    request.ClientCertificates.Add(accessCertificate);
        //    request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
        //    request.ContentType = Constants.ContentTypeXml;

        //    try
        //    {
        //        // Get the response
        //        HttpWebResponse response = (HttpWebResponse)request.GetResponse();

        //        // Display the status
        //        Console.WriteLine("\n...Enumerating Deployments...\n");
        //        Console.WriteLine(new StreamReader(response.GetResponseStream()).ReadToEnd());
        //    }
        //    catch (TimeoutException)
        //    {
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        Console.WriteLine("ERROR: Operation timed out, please try again...");
        //        Console.ForegroundColor = ConsoleColor.Gray;
        //        System.Environment.Exit(1);
        //    }
        //    catch 
        //    {
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        Console.WriteLine("\nUnable to enumerate hosted services, please check your input values and try again...");
        //        Console.ForegroundColor = ConsoleColor.Gray;
        //        System.Environment.Exit(1);
        //    }
        //}

        private static void GetDeployment(StahcManifest manifest)
        {
            WriteStatusLine("Getting Deployment...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                // send the GetDeployment request and retrieve the requestID so we can
                // keep tabs on how it is going...
                Utilities.GetDeployment(
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentName,
                    managementCertificate);

                //foreach (HostedService service in services)
                //{
                //    WriteStatusLine(string.Format("\tName:\t{0}", service.ServiceName));
                //    WriteStatusLine(string.Format("\tUrl:\t{0}\n", service.Url));
                //}
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void WaitForDeploymentStarted(StahcManifest manifest)
        {
            WriteStatus("Waiting for deployment to fully start...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                WaitForDeploymentStatus(
                    DeploymentStatus.Running,
                    InstanceStatus.Ready,
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentSlot,
                    managementCertificate,
                    true);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void WaitForDeploymentStopped(StahcManifest manifest)
        {
            WriteStatus("Waiting for deployment to fully stop...");

            try
            {
                var managementCertificate = new X509Certificate2(manifest.CertificateFile);

                WaitForDeploymentStatus(
                    DeploymentStatus.Suspended,
                    InstanceStatus.Stopped,
                    manifest.SubscriptionId,
                    manifest.ServiceName,
                    manifest.DeploymentSlot,
                    managementCertificate,
                    false);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void MonitorQueue(StahcManifest manifest)
        {
            WriteStatusLine("Waiting for Queue Length to empty...");

            try
            {
                WaitForQueueZero(
                    manifest.AccountName,
                    manifest.AccountKey,
                    manifest.StachQueueName);

                WriteStatusLine(string.Empty);
            }
            catch (WebException wex)
            {
                WriteError(string.Format("\nERROR: {0}\n", wex.Message));
            }
            catch (Exception ex)
            {
                WriteError(string.Format("\nERROR:{0}\n", ex.Message));
            }
        }

        private static void WaitForQueueZero(
            string accountName,
            string accountKey,
            string queueName)
        {
            // assume we haven't matched the desired status
            bool queueToZero = false;

            do
            {
                // sleep for a bit...
                Thread.Sleep(TimeSpan.FromSeconds(5));

                int queueLength = Utilities.GetQueueLength(accountName, accountKey, queueName);

                WriteStatus(string.Format("\r\t Queue Length: {0} at {1}",
                    queueLength, DateTime.Now));

                queueToZero = queueLength == 0;
            }
            while (!queueToZero);
        }

        private static void WaitForDeploymentStatus(
            DeploymentStatus deploymentStatus,
            InstanceStatus instanceStatus,
            string subscriptionId,
            string serviceName,
            DeploymentSlot deploymentSlot,
            X509Certificate2 managementCertificate,
            bool mostIsGoodEnough)
        {
            // assume we haven't matched the desired status
            bool statusMatches = false;

            do
            {
                // sleep for a bit...
                Thread.Sleep(TimeSpan.FromSeconds(5));
                WriteStatus(".");

                // get the current status
                FullDeploymentStatus current = Utilities.GetDeploymentSlotStatus(
                    subscriptionId,
                    serviceName,
                    deploymentSlot,
                    managementCertificate);

                // if the main status matches
                if (current.MainStatus == deploymentStatus)
                {
                    // good so far...
                    statusMatches = true;
                    int countMatch = 0;

                    // see if all instance status's also match
                    foreach (InstanceDetails instance in current.Instances)
                    {
                        if (instance.Status != instanceStatus)
                        {
                            // we have a bad apple
                            statusMatches = false;
                        }
                        else
                        {
                            countMatch++;
                        }
                    }

                    if (mostIsGoodEnough && ((double)countMatch / current.Instances.Count) >= 0.8)
                    {
                        statusMatches = true;
                    }
                }
            }
            while (!statusMatches);

            WriteStatusLine(string.Format(" {0}", deploymentStatus));
        }

        private static void MonitorAsyncRequest(
            string requestId,
            string subscriptionId,
            X509Certificate2 managementCertificate)
        {
            string asyncresponse = string.Empty;

            do
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                WriteStatus(".");
            }
            while (!Utilities.GetOperationStatus(requestId, subscriptionId, managementCertificate, out asyncresponse));

            WriteStatusLine(string.Format(" {0}", asyncresponse));
        }

        private static void Sleep(int iterations, TimeSpan iterationDuration)
        {
            WriteStatus("Simulating Work...");

            for (int i = 0; i < iterations; i++)
            {
                Thread.Sleep(iterationDuration);
                WriteStatus(".");
            }

            WriteStatusLine(" Done");
        }

        private static StahcManifest LoadSettingsFromXml(string optionsFilePath)
        {
            try
            {
                using (var textReader = new StreamReader(optionsFilePath))
                {
                    var manifestData = textReader.ReadToEnd();
                    textReader.Close();
                    XmlSerializer serializer = new XmlSerializer(typeof(StahcManifest));
                    StahcManifest manifest = (StahcManifest)serializer.Deserialize(new StringReader(manifestData));
                    return manifest;
                }
            }
            catch (Exception ex)
            {
                WriteError(string.Format("Unable to load settings file: {0}", ex.Message));
                return null;
            }
        }
    }
}