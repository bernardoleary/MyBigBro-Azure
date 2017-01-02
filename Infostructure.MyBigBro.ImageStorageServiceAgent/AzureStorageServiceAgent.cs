using System;
using System.Configuration;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent
{
    public class AzureStorageServiceAgent : IStorageServiceAgent
    {
        public string PutImage(IWebCamImage webCamImage, out string key)
        {
            if (webCamImage.Data != null) // accept the file
            {
                // Config
                var config = ConfigurationManager.AppSettings;
                var azureAccountName = config["azureAccountName"];
                var azureAccountKey = config["azureAccountKey"];

                // Azure credentials.
                string connectionString =
                    "DefaultEndpointsProtocol=https;" +
                    "AccountName=" + azureAccountName + ";" +
                    "AccountKey=" + azureAccountKey + ";";

                // Setup the filename.
                string fileName = Guid.NewGuid() + ".jpg";
                string url = null;

                // Setup the Azure stuff.
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(config["azureContainer"]);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

                // Create or overwrite the "myblob" blob with contents from a local file.
                using (var memStream = new MemoryStream(webCamImage.Data))
                {
                    blockBlob.UploadFromStream(memStream);
                    url = config["azureRootUrl"] + "/" + config["azureContainer"] + "/" + fileName;
                } 

                // Return our result.
                key = fileName;
                return url;
            }
            key = null;
            return null;
        }
    }
}
