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

namespace StahcCloudHost
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Xml.Serialization;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Diagnostics;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using Microsoft.WindowsAzure.StorageClient;
    using Ornl.Csmd.Csrg.Stahc.Core;
    using Ornl.Csmd.Csrg.Stahc.Core.Model;

    public class WorkerRole : RoleEntryPoint
    {
        private CloudQueue stahcJobQueue;
        private int queueSleepTime;
        private int maxJobLength;
        private string jobContainer;
        private string scratchPath;
        private CloudStorageAccount storageAccount;

        public override void Run()
        {
            while (true)
            {
                try
                {
                    // TODO: Should be using a repository pattern
                    var queueMessage = stahcJobQueue.GetMessage(
                        TimeSpan.FromSeconds(maxJobLength));

                    if (queueMessage != null)
                    {
                        do
                        {
                            var message = queueMessage.ToStahcJob();

                            // TODO: is there a better way to do this? (repository?)
                            DownloadJobFiles(message.InputFiles);
                            
                            if (RunJobTasks(message.Executables))
                            {
                                UploadResultsToCloud(message.OutputFiles);
                                DeleteFromLocalStorage(message.FilesToRemove);
                                stahcJobQueue.DeleteMessage(queueMessage);
                                // TODO: better diagnostics approach?
                                Trace.WriteLine("Deleted message from the queue", "Verbose");
                            }
                            else
                            {
                                Trace.TraceError(
                                    "Unable to process job taks. Review logs for more details");
                            }

                            // try to get the next message
                            queueMessage = stahcJobQueue.GetMessage(
                                TimeSpan.FromSeconds(maxJobLength));

                        } while (queueMessage != null);
                    }
                    else
                    {
                        Trace.WriteLine("No message found", "Verbose");
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message, "Error");
                }

                Thread.Sleep(TimeSpan.FromSeconds(queueSleepTime));
            }
        }

        /// <summary>
        /// This is where we get the role instance configured and ready to begin processing
        /// STAHC jobs
        /// </summary>
        /// <returns>True if succesfully configured. False otherwise</returns>
        public override bool OnStart()
        {
            ServicePointManager.DefaultConnectionLimit = 64;

            DiagnosticMonitorConfiguration config = DiagnosticMonitor.GetDefaultInitialConfiguration();

            config.PerformanceCounters.DataSources.Add(
                new PerformanceCounterConfiguration()
                {
                    CounterSpecifier = @"\Processor(_Total)\% Processor Time",
                    SampleRate = TimeSpan.FromSeconds(30)
                });

            config.PerformanceCounters.DataSources.Add(
                new PerformanceCounterConfiguration()
                {
                    CounterSpecifier = @"\Network Interface(*)\Bytes Total/sec",
                    SampleRate = TimeSpan.FromSeconds(30)
                });

            config.DiagnosticInfrastructureLogs.ScheduledTransferPeriod = System.TimeSpan.FromMinutes(5);
            config.DiagnosticInfrastructureLogs.ScheduledTransferLogLevelFilter = LogLevel.Error;
            config.Logs.ScheduledTransferPeriod = System.TimeSpan.FromMinutes(5);
            config.Logs.ScheduledTransferLogLevelFilter = LogLevel.Verbose;
            config.PerformanceCounters.ScheduledTransferPeriod = System.TimeSpan.FromMinutes(1);
            config.WindowsEventLog.ScheduledTransferPeriod = System.TimeSpan.FromMinutes(5);

            DiagnosticMonitor.Start("DiagnosticsConnectionString", config);

            // restart the role upon all configuration changes 
            RoleEnvironment.Changing += RoleEnvironmentChanging;

            // read storage account configuration settings
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });

            // get the scratch path
            scratchPath = RoleEnvironment.GetLocalResource(Constants.AzureScratchName).RootPath;

            // get the time to sleep between runs of the queue monitoring loop
            queueSleepTime = int.Parse(
                RoleEnvironment.GetConfigurationSettingValue("QueueSleepTime"),
                CultureInfo.InvariantCulture);

            // get the max time (seconds) that the server should take to process a queue job
            maxJobLength = int.Parse(
                RoleEnvironment.GetConfigurationSettingValue("MaxJobLength"),
                CultureInfo.InvariantCulture);

            // get the storage container to be used for processing job data
            jobContainer = RoleEnvironment.GetConfigurationSettingValue("JobContainer");

            // get queue data/configuration
            storageAccount = CloudStorageAccount.FromConfigurationSetting("DataConnectionString");

            // get the name of the queue used for this job set
            var queueName = RoleEnvironment.GetConfigurationSettingValue("StachQueueName");

            // get the queues
            CloudQueueClient queueStorage = storageAccount.CreateCloudQueueClient();
            queueStorage.RetryPolicy = RetryPolicies.RetryExponential(3, TimeSpan.FromSeconds(10));

            stahcJobQueue = queueStorage.GetQueueReference(queueName);
            stahcJobQueue.CreateIfNotExist();

            // report on read values
            Trace.WriteLine(string.Format("QueueSleepTime: '{0}'", 
                queueSleepTime.ToString(CultureInfo.InvariantCulture)), "Verbose");
            Trace.WriteLine(string.Format("MaxJobLength: '{0}'", 
                maxJobLength.ToString(CultureInfo.InvariantCulture)), "Verbose");
            Trace.WriteLine(string.Format("JobContainer: '{0}'", jobContainer), "Verbose");
            Trace.WriteLine(string.Format("StachQueueName: '{0}'", queueName), "Verbose");

            // read-in/download all source files
            DownloadStagingFiles();

            // loop through and execute each of the actions (if any) provided in the staging file
            var stagingControlFile = Path.Combine(
                scratchPath, 
                Constants.StagingActionsFileName);

            if (File.Exists(stagingControlFile))
            {
                var sucessful = RunStagingActions(stagingControlFile);
                
                if (!sucessful)
                {
                    Trace.TraceError(
                        "Unable to complete staging actions. Review logs for more detail.");
                    return sucessful;
                }
            }

            return base.OnStart();
        }

        /// <summary>
        /// Helper method that looks at the staging folder in the current job container
        /// and downloads any files found to the current scratch path
        /// </summary>
        private void DownloadStagingFiles()
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(jobContainer);
            container.CreateIfNotExist();
            CloudBlobDirectory stagingFiles = container.GetDirectoryReference(Constants.StagingFilesPath);

            // get all of the files in that container
            foreach (IListBlobItem blob in stagingFiles.ListBlobs())
            {
                // get the url
                var fileName = Path.GetFileName(blob.Uri.ToString());
                var localPath = Path.Combine(scratchPath, fileName);
                var blockBlob = container.GetBlockBlobReference(blob.Uri.ToString());
                blockBlob.DownloadToFile(localPath);
                Trace.WriteLine(string.Format("Downloaded file: {0}", fileName), "Verbose");
            }
        }

        /// <summary>
        /// Helper method to load up the staging actions file, deserialize it, and then 
        /// execute each of the calls defined therein.
        /// </summary>
        /// <param name="stagingFilePath">Path to the staging Actions XML file</param>
        private bool RunStagingActions(string stagingFilePath)
        {
            bool successful = true;

            // TODO: There's got to be a better/clearer way to accomplish this...
            XmlSerializer serializer = new XmlSerializer(typeof(Executeable[]));
            Executeable[] actions = (Executeable[])serializer.Deserialize(new StreamReader(stagingFilePath));

            for (int i = 0; i < actions.Length; i++)
            {
                successful = RunTask(actions[i].Path, actions[i].Arguments);

                if (!successful)
                {
                    break;
                }
            }

            return successful;
        }

        /// <summary>
        /// Helper method to download any job-specific files (normally data/query files).
        /// Prior to downloading a given file a check is made to ensure that it doesn't already
        /// exist on local storage. If it is found, the download is skipped and reported.
        /// </summary>
        /// <param name="jobFiles">
        /// String Array containing the relative paths of the files to be downloaded for the 
        /// current job. These paths are to be relative to the container + dataFilesPath.
        /// </param>
        private void DownloadJobFiles(string[] jobFiles)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(jobContainer);
            container.CreateIfNotExist();

            for (int i = 0; i < jobFiles.Length; i++)
            {
                var localPath = Path.Combine(scratchPath, jobFiles[i]);

                // If file doesn't exist in given location, download it 
                if (!File.Exists(localPath))
                {
                    var remoteRelativePath = string.Format("{0}/{1}",
                        Constants.DataFilesPath,
                        jobFiles[i]);
                    var blockBlob = container.GetBlockBlobReference(remoteRelativePath);
                    blockBlob.DownloadToFile(localPath);
                    Trace.WriteLine(string.Format("Downloaded file: {0}", jobFiles[i]), 
                        "Verbose");
                }
                else
                {
                    Trace.WriteLine("File exists in local storage", localPath);
                }
            }
        }

        /// <summary>
        /// Helper function to run each of the tasks defined in the job
        /// </summary>
        /// <param name="executeables">Array of tasks to execute.</param>
        /// <returns>True if all tasks completed successfully. False otherwise.</returns>
        private bool RunJobTasks(Executeable[] executeables)
        {
            bool successful = true;

            for (int i = 0; i < executeables.Length; i++)
            {
                successful = RunTask(executeables[i].Path, executeables[i].Arguments);
                
                if (!successful)
                {
                    break;
                }
            }

            return successful;
        }

        /// <summary>
        /// Helper function to upload the output/result files to persistent storage
        /// </summary>
        /// <param name="outputFiles">
        /// Array of files to be uploaded. It is assumed that the values in this array are
        /// relative to the scratch path
        /// </param>
        private void UploadResultsToCloud(string[] outputFiles)
        {
            Trace.WriteLine("Uploading output files", "Verbose");

            try
            {
                var client = storageAccount.CreateCloudBlobClient();
                var container = client.GetContainerReference(jobContainer);
                container.CreateIfNotExist();

                for (int i = 0; i < outputFiles.Length; i++)
                {
                    var localPath = Path.Combine(scratchPath, outputFiles[i]);
                    var remotePath = string.Format("{0}/{1}",
                        Constants.OutputFilesPath,
                        outputFiles[i]);
                    var blob = container.GetBlockBlobReference(remotePath);
                    blob.UploadFile(localPath);
                    Trace.WriteLine(string.Format("Uploaded file: {0}", blob.Uri), "Verbose");
                }
            }
            catch (TimeoutException)
            {
                Trace.TraceError("Operation timed out, please try again...");
            }
            catch (FormatException)
            {
                Trace.TraceError(
                    "Cannot upload files, please check input parameters and try again...");
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to upload file: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Helper method to execute a command. 
        /// </summary>
        /// <param name="applicationPath">Path, relative to the scratch path, of the executeable</param>
        /// <param name="arguments">String of command line arguments to be passed to the executeable</param>
        /// <returns></returns>
        private bool RunTask(string applicationPath, string arguments)
        {
            var startInfo = new ProcessStartInfo 
                { 
                    FileName = Path.Combine(scratchPath, applicationPath), 
                    Arguments = arguments, 
                    UseShellExecute = true, 
                    WorkingDirectory = scratchPath 
                };

            try
            {
                using (Process p = Process.Start(startInfo))
                {
                    p.WaitForExit();
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message, "Error");
                return false;
            }
        }

        /// <summary>
        /// Helper method to remove files from the local disk. This is used to clean up 
        /// intermediaray files generated during a given job that may interfere with subsequent
        /// job runs.
        /// </summary>
        /// <param name="filesToRemove">
        /// Array of file names to be removed. It is assumed that these values are relative to 
        /// the scratch path
        /// </param>
        private void DeleteFromLocalStorage(string[] filesToRemove)
        {
            for (int i = 0; i < filesToRemove.Length; i++)
            {
                string fileRemove = Path.Combine(scratchPath, filesToRemove[i]);

                if (File.Exists(fileRemove))
                {
                    File.Delete(fileRemove);
                }
            }
        }

        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                e.Cancel = true;
            }
        }
    }
}