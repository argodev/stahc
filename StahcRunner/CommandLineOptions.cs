namespace StahcRunner
{
    using System;
    using CommandLine;
    using CommandLine.Text;
    using Ornl.Csmd.Csrg.Stahc.Core;

    /// <summary>
    /// Defines the command line parameters for this application 
    /// </summary>
    public class CommandLineOptions
    {
        /// <summary>
        /// Operation to perform. Must be one of the following: Deploy, RetrieveOutput, CleanUp, 
        /// or FullTest
        /// </summary>
        [Option("o", "operation", Required = true,
            HelpText = "Operation to perform. Must be one of the following: Deploy, " +
            "RetrieveOutput, CleanUp, or FullTest")]
        public ControlOperation Operation = ControlOperation.Deploy;

        /// <summary>
        /// Path to XML file that represents the settings for your job. Alternatively, you can 
        /// provide the parameters at the command line
        /// </summary>
        [Option("x", "settingsFile", Required = true,
            HelpText = "Path to XML file that represents the settings for your job. " +
            "Alternatively, you can provide the parameters at the command line")]
        public string XmlSettingsFile = string.Empty;

        ///// <summary>
        ///// Path to the input configuration file
        ///// </summary>
        //[Option("c", "configfile", Required = false,
        //    HelpText = "Path to the STAHC Azure configuration file (usually " +
        //    "ServiceConfiguration.cscfg)")]
        //public string ConfigurationFile = string.Empty;

        ///// <summary>
        ///// Path to the STAHC Package File
        ///// </summary>
        //[Option("p", "packageFile", Required = false,
        //    HelpText = "Path to the STAHC Azure package file (usually " +
        //    "ApplicationWorkerRole.cspkg)")]
        //public string PackageFile = string.Empty;

        ///// <summary>
        ///// Path to the local certificate file for managing Azure services
        ///// </summary>
        //[Option("r", "certificateFile", Required = false,
        //    HelpText = "Path to the local certificate file for managing Azure services. " +
        //    "(usually ends in .pem)")]
        //public string Certificate = string.Empty;

        ///// <summary>
        ///// Windows Azure Account Name
        ///// </summary>
        //[Option("n", "accountName", Required = false, HelpText = "Windows Azure Account Name")]
        //public string AccountName = string.Empty;

        ///// <summary>
        ///// Windows Azure Account Key (primary or secondary)
        ///// </summary>
        //[Option("k", "accountKey", Required = false, HelpText = "Windows Azure Account Key.")]
        //public string AccountKey = string.Empty;

        ///// <summary>
        ///// Name of the Azure Container where files should be persisted
        ///// </summary>
        //[Option("b", "containerName", Required = false,
        //    HelpText = "Name of the Azure Container where files should be persisted")]
        //public string ContainerName = string.Empty;

        ///// <summary>
        ///// Subscription ID for managing this account
        ///// </summary>
        //[Option("i", "subscriptionId", Required = false,
        //    HelpText = "Subscription ID for managing this account")]
        //public string SubscriptionId = string.Empty;

        ///// <summary>
        ///// The name of the service you are deploying to. Will look something like 'ornlldrdusnc'. 
        ///// Run this tool with the ListHostedServices option to see valid values
        ///// </summary>
        //[Option("y", "serviceName", Required = false,
        //    HelpText = "The name of the service you are deploying to. Will look something " +
        //    "like 'ornlldrdusnc'. Run this tool with the ListHostedServices option to see " +
        //    "valid values")]
        //public string ServiceName = string.Empty;

        ///// <summary>
        ///// A list of files to be deployed to each node prior to starting to process jobs
        ///// </summary>
        //[OptionArray("g", "stagingFiles", Required = false,
        //    HelpText = "A list of files to be deployed to each node prior to starting to " +
        //    "process jobs")]
        //public string[] StagingFiles = null;

        ///// <summary>
        ///// A list of commands to be executed on each node during OnStart().
        ///// these commands are called after any files included in StagingFiles[] are 
        ///// downloaded to the node
        ///// </summary>
        //[OptionArray("h", "stagingOperations", Required = false,
        //    HelpText = "A list of commands to be executed on each node during OnStart(). " +
        //    "these commands are called after any files included in stagingFiles[] are " +
        //    "downloaded to the node")]
        //public string[] StagingOperations = null;

        ///// <summary>
        ///// A list of files to be used as data input for various jobs
        ///// </summary>
        //[OptionArray("f", "dataFiles", Required = false,
        //    HelpText = "A list of files to be used as data input for various jobs")]
        //public string[] DataFiles = null;

        ///// <summary>
        ///// Azure code slot into which the code should be loaded. Options are Production or 
        ///// Staging. Defaults to Staging.
        ///// </summary>
        //[Option("s", "deploymentSlot", Required = false,
        //    HelpText = "Azure code slot into which the code should be loaded. Options are " +
        //    "Production or Staging. Defaults to Staging.")]
        //public DeploymentSlot TargetSlot = DeploymentSlot.Staging;

        ///// <summary>
        ///// Label for the deployment within the Azure portal. Defaults to the current date/time
        ///// </summary>
        //[Option("l", "label", Required = false,
        //    HelpText = "Label for the deployment within the Azure portal.")]
        //public string ConfigurationLabel = string.Format(
        //    "STAHC Deployment {0}",
        //    DateTime.Now.ToString());

        ///// <summary>
        ///// Deployment Name for the deployment within the Azure portal. Defaults to 
        ///// STAHCDeployment. Must not contain spaces.
        ///// </summary>
        //[Option("d", "deploymentName", Required = false,
        //    HelpText = "Name for the deployment. Defaults to STAHCDeployment. " +
        //    "Must not contain spaces.")]
        //public string DeploymentNameLabel = "STAHCDeployment";

        ///// <summary>
        ///// Folder on the local machine where output data should be copied
        ///// </summary>
        //[Option("z", "outputLocation", Required = false,
        //    HelpText = "Folder on the local machine where output data should be copied.")]
        //public string OutputLocation = string.Empty;

        ///// <summary>
        ///// The number of instances that should be used for this operation
        ///// </summary>
        //[Option("n", "instanceCount", Required = false,
        //    HelpText = "The number of instances that should be used for this operation.")]
        //public int InstanceCount = 1;

        ///// <summary>
        ///// The number of seconds that the Queue service should expect to wait before assuming a 
        ///// node failed to process a job. Defaults to 30
        ///// </summary>
        //[Option("w", "maxJobLength", Required = false,
        //    HelpText = "The number of seconds that the Queue service should expect to wait " +
        //    "before assuming a node failed to process a job. Defaults to 30.")]
        //public int MaxJobLength = 30;

        /// <summary>
        /// Displays the help screen
        /// </summary>
        /// <returns>
        /// Text representation of the parameter requirements for this application
        /// </returns>
        [HelpOption(HelpText = "Display this help screen")]
        public string GetUsage()
        {
            var help = new HelpText("\nSTAHC Utility");
            help.AddPreOptionsLine("This utility takes a collection of files and settings, uploads them to the");
            help.AddPreOptionsLine("Windows Azure platform and then deploys them. Also provided are methods for ");
            help.AddPreOptionsLine("terminating the operation and cleaning up after one is done. ");
            help.AddOptions(this);
            help.AddPostOptionsLine("\n");

            return help;
        }
    }
}