namespace StahcCloudHost
{
    using System.IO;
    using System.Xml.Serialization;
    using Microsoft.WindowsAzure.StorageClient;
    using Ornl.Csmd.Csrg.Stahc.Core.Model;
    public static class Extensions
    {
        public static StahcJob ToStahcJob(this CloudQueueMessage queueMessage)
        {
            XmlSerializer s = new XmlSerializer(typeof(StahcJob));
            return (StahcJob)s.Deserialize(new StringReader(queueMessage.AsString));
        }
    }
}
