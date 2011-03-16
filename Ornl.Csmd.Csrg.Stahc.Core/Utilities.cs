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
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Serialization;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.StorageClient;
    using Ornl.Csmd.Csrg.Stahc.Core.Model;

    public delegate void StachFileDownloadEventHandler(object sender, StachFileDownloadEventArgs e);

    public class Utilities
    {
        public static string UploadFileToCloud(
            string accountName,
            string accountKey,
            string containerName,
            string targetPath,
            string localPath)
        {
            var credentials = new StorageCredentialsAccountAndKey(accountName, accountKey);
            return UploadFileToCloud(credentials, containerName, targetPath, localPath);
        }

        public static string UploadFileToCloud(
            StorageCredentialsAccountAndKey credentials,
            string containerName,
            string targetPath,
            string localPath)
        {
            // instantiate the Cloud blob client
            var account = new CloudStorageAccount(credentials, false);
            var client = account.CreateCloudBlobClient();

            // ensure that the target container exists
            var container = client.GetContainerReference(containerName);
            container.CreateIfNotExist();

            // upload the file
            var blob = container.GetBlockBlobReference(targetPath);

            // we should check to see if the file exists prior to 
            // uploading. If it is already there, don't waste the upload time
            try
            {
                blob.FetchAttributes();

                // if we get a valid value back for the length, we know that the file
                // already exists and we can likely continue on w/o re-uploading
                if (blob.Attributes.Properties.Length > 0)
                {
                    // does the remote file size match the local file size?
                    long remoteLength = blob.Attributes.Properties.Length;
                    long localLength = (new FileInfo(localPath)).Length;

                    // file already exists... can we avoid re-uploading?
                    if (remoteLength != localLength)
                    {
                        blob.ParallelUploadFile(localPath, null, 1048576);
                    }
                }
                else
                {
                    blob.ParallelUploadFile(localPath, null, 1048576);
                }
            }
            catch (StorageClientException)
            {
                // most often due to the blob not existing. Squash
                // and continue.
                blob.ParallelUploadFile(localPath, null, 1048576);
            }

            // return the URL
            return blob.Uri.ToString();
        }

        public static string UploadStringToCloud(
            string accountName,
            string accountKey,
            string containerName,
            string targetPath,
            string content)
        {
            var credentials = new StorageCredentialsAccountAndKey(accountName, accountKey);
            return UploadStringToCloud(credentials, containerName, targetPath, content);
        }

        public static string UploadStringToCloud(
            StorageCredentialsAccountAndKey credentials,
            string containerName,
            string targetPath,
            string content)
        {
            // instantiate the Cloud blob client
            var account = new CloudStorageAccount(credentials, false);
            var client = account.CreateCloudBlobClient();

            // ensure that the target container exists
            var container = client.GetContainerReference(containerName);
            container.CreateIfNotExist();

            // upload the file
            var blob = container.GetBlockBlobReference(targetPath);
            blob.UploadText(content);

            // return the URL
            return blob.Uri.ToString();
        }

        public static string CreateDeployment(
            string subscriptionId,
            string serviceName,
            string deploymentSlot,
            string deploymentName,
            string packageUrl,
            string configurationLabel,
            int instanceCount,
            int maxJobLength,
            string accountName,
            string accountKey,
            string container,
            int queueSleepTime,
            string queueName,
            X509Certificate2 managementCertificate)
        {
            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/
            //                hostedservices/<service-name>/deploymentslots/<deployment-slot-name>
            var url = string.Format("{0}{1}/services/hostedservices/{2}/deploymentslots/{3}",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                serviceName,
                deploymentSlot);

            // Base64 encode configuration label and file
            var base64label = EncodeAsciiStringTo64(configurationLabel);
            var base64config = GetSettings(
                instanceCount,
                accountName,
                accountKey,
                queueSleepTime,
                maxJobLength,
                container,
                queueName);

            // build request body
            StringBuilder blob = new StringBuilder();
            blob.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            blob.Append("<CreateDeployment xmlns=\"http://schemas.microsoft.com/windowsazure\">\n");
            blob.AppendFormat("\t<Name>{0}</Name>\n", deploymentName);
            blob.AppendFormat("\t<PackageUrl>{0}</PackageUrl>\n", packageUrl);
            blob.AppendFormat("\t<Label>{0}</Label>\n", base64label);
            blob.AppendFormat("\t<Configuration>{0}</Configuration>\n", base64config);
            blob.Append("</CreateDeployment>\n");

            // encode request body then put it in a byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(blob.ToString());

            // make request
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);

            // header info
            request.Method = "POST";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;
            request.ContentLength = byteArray.Length;

            Stream dataStream = request.GetRequestStream();

            // write the data to the request stream.
            dataStream.Write(byteArray, 0, byteArray.Length);

            // Get the response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // Get the x-ms-requestID
            string requestID = response.GetResponseHeader(Constants.RequestIdHeader);

            // Clean up the streams
            dataStream.Close();
            response.Close();

            return requestID;
        }

        public static string UpdateDeployment(
            string subscriptionId,
            string serviceName,
            string deploymentName,
            DeploymentStatus newStatus,
            X509Certificate2 managementCertificate)
        {
            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/
            //                hostedservices/<service-name>/deployments/<deployment-name>
            var url = string.Format("{0}{1}/services/hostedservices/{2}/deployments/{3}/?comp=status",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                serviceName,
                deploymentName);

            // make request
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);

            // header info
            request.Method = "POST";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Build uri string
            StringBuilder blob = new StringBuilder();
            blob.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            blob.Append("<UpdateDeploymentStatus xmlns=\"http://schemas.microsoft.com/windowsazure\">\n");

            switch (newStatus)
            {
                case DeploymentStatus.Running:
                    blob.AppendFormat("<Status>{0}</Status>\n", Constants.StartServiceStatus);
                    break;
                case DeploymentStatus.Suspended:
                    blob.AppendFormat("<Status>{0}</Status>\n", Constants.StopServiceStatus);
                    break;
                default:
                    // do nothing
                    break;
            }

            blob.AppendFormat("<Status>{0}</Status>\n", Constants.StartServiceStatus);
            blob.Append("</UpdateDeploymentStatus>\n");

            // encode request body then put it in a byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(blob.ToString());
            request.ContentLength = byteArray.Length;

            Stream dataStream = request.GetRequestStream();

            // write the data to the request stream.
            dataStream.Write(byteArray, 0, byteArray.Length);

            // close the Stream object.
            dataStream.Close();

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // Get the x-ms-requestID
            string requestId = response.GetResponseHeader(Constants.RequestIdHeader);

            // Clean up the streams
            response.Close();

            return requestId;
        }

        public static string DeleteDeployment(
            string subscriptionId,
            string serviceName,
            string deploymentName,
            X509Certificate2 managementCertificate)
        {
            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/
            //                hostedservices/<service-name>/deployments/<deployment-name>
            var url = string.Format("{0}{1}/services/hostedservices/{2}/deployments/{3}",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                serviceName,
                deploymentName);

            // make request
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);

            // header info
            request.Method = "DELETE";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // Get the x-ms-requestID
            string requestId = response.GetResponseHeader(Constants.RequestIdHeader);

            // Clean up the streams
            response.Close();

            return requestId;
        }

        public static bool GetOperationStatus(
            string requestId,
            string subscriptionId,
            X509Certificate2 managementCertificate,
            out string asyncresponse)
        {
            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/operations/<request-id>
            var url = string.Format("{0}{1}/operations/{2}",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                requestId);

            // make uri request using created uri string
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // make header, method, and certificated requests
            request.Method = "GET";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // put response into string text
            using (StreamReader dataStream = new StreamReader(response.GetResponseStream()))
            {
                string text = dataStream.ReadToEnd();
                // create an xml document
                XmlDocument xml = new XmlDocument();
                // load up the response text as xml
                xml.LoadXml(text);
                // get the NS manager
                XmlNamespaceManager ns = new XmlNamespaceManager(xml.NameTable);
                ns.AddNamespace("az", Constants.AzureXmlNamespace);
                // get innertext of Status node
                string result = xml.SelectSingleNode("//az:Status", ns).InnerText;
                if (result != "InProgress")
                {
                    // NOTE: it may have failed... let's handle that case as well
                    if (result.Equals("Failed"))
                        asyncresponse = string.Format("{0} - {1}", result, xml.SelectSingleNode("//az:Message", ns).InnerText);
                    else
                        asyncresponse = result;
                    // return true if we are done
                    return true;
                }
                else
                {
                    asyncresponse = String.Empty;
                    // return false otherwise
                    return false;
                }
            }
        }

        public static List<HostedService> ListHostedServices(
            string subscriptionId,
            X509Certificate2 managementCertificate)
        {
            // create the list to return
            var services = new List<HostedService>();

            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/hostedservices
            var url = string.Format("{0}{1}/services/hostedservices",
                Constants.AzureManagementUrlBase,
                subscriptionId);

            // make uri request using created uri string
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // make header, method, and certificated requests
            request.Method = "GET";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // put response into string text
            using (StreamReader dataStream = new StreamReader(response.GetResponseStream()))
            {
                string text = dataStream.ReadToEnd();
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(text);

                XmlNamespaceManager ns = new XmlNamespaceManager(xml.NameTable);
                ns.AddNamespace("az", Constants.AzureXmlNamespace);
                
                // get the collection of nodes
                XmlNodeList serviceNodes = xml.SelectNodes("//az:HostedService", ns);

                foreach (XmlNode node in serviceNodes)
                {
                    services.Add(new HostedService { ServiceName = node.SelectSingleNode("az:ServiceName", ns).InnerText, Url = node.SelectSingleNode("az:Url", ns).InnerText });
                }
            }

            return services;
        }

        public static void GetHostedServicesProperties(
            string subscriptionId,
            string serviceName,
            X509Certificate2 managementCertificate)
        {
            // create the list to return
            var services = new List<HostedService>();

            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/hostedservices
            //          /<service-name
            var url = string.Format("{0}{1}/services/hostedservices/{2}",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                serviceName);

            // make uri request using created uri string
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // make header, method, and certificated requests
            request.Method = "GET";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // put response into string text
            using (StreamReader dataStream = new StreamReader(response.GetResponseStream()))
            {
                string text = dataStream.ReadToEnd();
                // create an xml document
                XmlDocument xml = new XmlDocument();
                // load up the response text as xml
                xml.LoadXml(text);
                // get the NS manager
                XmlNamespaceManager ns = new XmlNamespaceManager(xml.NameTable);
                ns.AddNamespace("az", Constants.AzureXmlNamespace);
                // get the collection of nodes
                XmlNodeList serviceNodes = xml.SelectNodes("//az:HostedService", ns);
                foreach (XmlNode node in serviceNodes)
                    services.Add(new HostedService { ServiceName = node.SelectSingleNode("az:ServiceName", ns).InnerText, Url = node.SelectSingleNode("az:Url", ns).InnerText });
            }

            //return services;
        }

        public static FullDeploymentStatus GetDeploymentSlotStatus(
            string subscriptionId,
            string serviceName,
            DeploymentSlot deploymentSlot,
            X509Certificate2 managementCertificate)
        {
            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/hostedservices
            //          /<service-name>/deploymentslots/<deployment-name/
            var url = string.Format("{0}{1}/services/hostedservices/{2}/deploymentslots/{3}",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                serviceName,
                deploymentSlot);

            // make uri request using created uri string
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // make header, method, and certificated requests
            request.Method = "GET";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // put response into string text
            using (StreamReader dataStream = new StreamReader(response.GetResponseStream()))
            {
                string text = dataStream.ReadToEnd();
                // create an xml document
                XmlDocument xml = new XmlDocument();
                // load up the response text as xml
                xml.LoadXml(text);
                // get the NS manager
                XmlNamespaceManager ns = new XmlNamespaceManager(xml.NameTable);
                ns.AddNamespace("az", Constants.AzureXmlNamespace);
                // return the status
                DeploymentStatus currentStatus;
                var statusText = xml.SelectSingleNode("//az:Status", ns).InnerText;
                if (Enum.TryParse<DeploymentStatus>(statusText, true, out currentStatus))
                {
                    FullDeploymentStatus fullStatus = new FullDeploymentStatus { MainStatus = currentStatus };
                    // now try to get the status values for each instance
                    XmlNodeList instances = xml.SelectNodes("//az:RoleInstance", ns);
                    foreach (XmlNode instance in instances)
                    {
                        var instanceStatus = new InstanceDetails { RoleName = instance.SelectSingleNode("az:RoleName", ns).InnerText, InstanceName = instance.SelectSingleNode("az:InstanceName", ns).InnerText, Status = (InstanceStatus)Enum.Parse(typeof(InstanceStatus), instance.SelectSingleNode("az:InstanceStatus", ns).InnerText) };
                        fullStatus.Instances.Add(instanceStatus);
                    }
                    return fullStatus;
                }
                else
                    throw new ArgumentOutOfRangeException("Status", "The status returned for the deployment is outside the range of acceptable values");
            }
        }

        public static void GetDeployment(
            string subscriptionId,
            string serviceName,
            string deploymentName,
            X509Certificate2 managementCertificate)
        {
            // create the list to return
            var services = new List<HostedService>();

            // Build uri string
            // format:https://management.core.windows.net/<subscription-id>/services/hostedservices
            //          /<service-name>/deployments/<deployment-name/
            var url = string.Format("{0}{1}/services/hostedservices/{2}/deploymentslots/{3}",
                Constants.AzureManagementUrlBase,
                subscriptionId,
                serviceName,
                "Production");

            // make uri request using created uri string
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // make header, method, and certificated requests
            request.Method = "GET";
            request.ClientCertificates.Add(managementCertificate);
            request.Headers.Add(Constants.VersionHeader, Constants.VersionTarget);
            request.ContentType = Constants.ContentTypeXml;

            // Get the response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // put response into string text
            using (StreamReader dataStream = new StreamReader(response.GetResponseStream()))
            {
                string text = dataStream.ReadToEnd();
                // create an xml document
                XmlDocument xml = new XmlDocument();
                // load up the response text as xml
                xml.LoadXml(text);
                // get the NS manager
                XmlNamespaceManager ns = new XmlNamespaceManager(xml.NameTable);
                ns.AddNamespace("az", Constants.AzureXmlNamespace);
                // get the collection of nodes
                XmlNodeList serviceNodes = xml.SelectNodes("//az:HostedService", ns);
                foreach (XmlNode node in serviceNodes)
                    services.Add(new HostedService { ServiceName = node.SelectSingleNode("az:ServiceName", ns).InnerText, Url = node.SelectSingleNode("az:Url", ns).InnerText });
            }
        }

        public static void CreateQueueMessage(
            string accountName,
            string accountKey,
            string queueName,
            Executeable[] executeables,
            string[] inputFiles,
            string[] outFiles,
            string[] removeFiles)
        {
            var storageAccount = new CloudStorageAccount(
            new StorageCredentialsAccountAndKey(accountName, accountKey), false);

            CloudQueueClient queueStorage = storageAccount.CreateCloudQueueClient();

            var jobQueue = queueStorage.GetQueueReference(queueName);
            jobQueue.CreateIfNotExist();

            var message = new StahcJob()
            {
                Executables = executeables,
                InputFiles = inputFiles,
                OutputFiles = outFiles,
                FilesToRemove = removeFiles
            };

            XmlSerializer serializer = new XmlSerializer(typeof(StahcJob));
            using (StringWriter sw1 = new StringWriter())
            {
                serializer.Serialize(sw1, message);
                // Put message on queue
                CloudQueueMessage cloudMessage = new CloudQueueMessage(sw1.ToString());
                jobQueue.AddMessage(cloudMessage);
            }
        }

        public static int GetQueueLength(
            string accountName,
            string accountKey,
            string queueName)
        {
            var storageAccount = new CloudStorageAccount(
            new StorageCredentialsAccountAndKey(accountName, accountKey), false);

            CloudQueueClient queueStorage = storageAccount.CreateCloudQueueClient();

            var jobQueue = queueStorage.GetQueueReference(queueName);
            jobQueue.CreateIfNotExist();

            return jobQueue.RetrieveApproximateMessageCount();
        }

        public static string GenerateAzureConfigurationFile(
            int instances, 
            string accountName, 
            string accountKey,
            int queueSleepTime,
            int maxJobLength,
            string jobContainer,
            string queueName)
        {
            StringBuilder xml = new StringBuilder("<?xml version=\"1.0\"?>");
            xml.Append("<ServiceConfiguration serviceName=\"StahcCloudService\" " +
                "xmlns=\"http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration\">");
            xml.Append("<Role name=\"StahcCloudHost\">");
            xml.AppendFormat("<Instances count=\"{0}\" />", instances);
            xml.Append("<ConfigurationSettings>");
            xml.AppendFormat("<Setting name=\"DiagnosticsConnectionString\" " +
                "value=\"DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}\" />",
                accountName,
                accountKey);
            xml.AppendFormat("<Setting name=\"QueueSleepTime\" value=\"{0}\" />", queueSleepTime);
            xml.AppendFormat("<Setting name=\"DataConnectionString\" " +
                "value=\"DefaultEndpointsProtocol=http;AccountName={0};AccountKey={1}\" />",
                accountName,
                accountKey);
            xml.AppendFormat("<Setting name=\"MaxJobLength\" value=\"{0}\" />", maxJobLength);
            xml.AppendFormat("<Setting name=\"JobContainer\" value=\"{0}\" />", jobContainer);
            xml.AppendFormat("<Setting name=\"StachQueueName\" value=\"{0}\" />", queueName);
            xml.Append("</ConfigurationSettings></Role></ServiceConfiguration>");

            return xml.ToString();
        }

        private static string EncodeAsciiStringTo64(string unencoded)
        {
            return Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(unencoded));
        }

        private static string GetSettings(
            int instanceCount,
            string accountName,
            string accountKey,
            int queueSleepTime,
            int maxJobLength,
            string container,
            string queueName)
        {
            var settings = GenerateAzureConfigurationFile(
                instanceCount,
                accountName,
                accountKey,
                queueSleepTime,
                maxJobLength,
                container,
                queueName);

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(settings));
        }

        public void DownloadFolderFiles(
            string accountName,
            string accountKey,
            string containerName,
            string folder,
            string targetPath)
        {
            var storageAccount = new CloudStorageAccount(
                new StorageCredentialsAccountAndKey(accountName, accountKey), false);
            var blobClient = storageAccount.CreateCloudBlobClient();
            blobClient.RetryPolicy = RetryPolicies.RetryExponential(3, TimeSpan.FromSeconds(10));
            var container = blobClient.GetContainerReference(containerName);
            container.CreateIfNotExist();
            CloudBlobDirectory stagingFiles = container.GetDirectoryReference(folder);

            // get all of the files in that container
            //foreach (IListBlobItem blob in stagingFiles.ListBlobs())
            //{
            //    // get the url
            //    var fileName = Path.GetFileName(blob.Uri.ToString());
            //    var localPath = Path.Combine(targetPath, fileName);
            //    var blockBlob = container.GetBlockBlobReference(blob.Uri.ToString());

            //    //blockBlob.DownloadToFile(localPath, new BlobRequestOptions() { Timeout = TimeSpan.FromSeconds(180) });
            //    blockBlob.ParallelDownloadToFile(localPath, 4194304);
            //    OnFileDownloaded(new StachFileDownloadEventArgs() { FileName = fileName });
            //    blockBlob = null;
            //}

            // limit to 2 concurrent files at a time otherwise we often end up with 
            // System.OutOfMemory exceptions being thrown.
            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = 2 };

            Parallel.ForEach(stagingFiles.ListBlobs(), options, blob =>
            {
                // get the url
                var fileName = Path.GetFileName(blob.Uri.ToString());
                var localPath = Path.Combine(targetPath, fileName);
                var blockBlob = container.GetBlockBlobReference(blob.Uri.ToString());

                //blockBlob.DownloadToFile(localPath, new BlobRequestOptions() { Timeout = TimeSpan.FromSeconds(180) });
                blockBlob.ParallelDownloadToFile(localPath, 4194304);
                OnFileDownloaded(new StachFileDownloadEventArgs() { FileName = fileName });

                blockBlob = null;
            });             
        }

        public event StachFileDownloadEventHandler FileDownloaded;

        protected virtual void OnFileDownloaded(StachFileDownloadEventArgs e)
        {
            FileDownloaded(this, e);
        }
    }
}