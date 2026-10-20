using System;
using System.IO;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Infostructure.MyBigBro.Domain;
using Microsoft.Extensions.Options;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent
{
    public class AzureStorageServiceAgent : IStorageServiceAgent
    {
        private readonly StorageOptions _options;

        public AzureStorageServiceAgent()
        {
            _options = new StorageOptions();
        }

        public AzureStorageServiceAgent(IOptions<StorageOptions> options)
        {
            _options = options?.Value ?? new StorageOptions();
        }

        public AzureStorageServiceAgent(StorageOptions options)
        {
            _options = options ?? new StorageOptions();
        }

        public string PutImage(IWebCamImage webCamImage, out string key)
        {
            if (webCamImage?.Data != null)
            {
                string fileName = Guid.NewGuid() + ".jpg";
                var serviceClient = new BlobServiceClient(_options.AzureConnectionString);
                var containerClient = serviceClient.GetBlobContainerClient(_options.AzureContainer);
                containerClient.CreateIfNotExists(PublicAccessType.Blob);

                var blobClient = containerClient.GetBlobClient(fileName);
                using (var memStream = new MemoryStream(webCamImage.Data))
                {
                    blobClient.Upload(memStream, new BlobHttpHeaders { ContentType = "image/jpeg" });
                }

                string rootUrl = (_options.PublicRootUrl ?? "http://localhost:10000/devstoreaccount1").TrimEnd('/');
                string url = $"{rootUrl}/{_options.AzureContainer}/{fileName}";
                key = fileName;
                return url;
            }

            key = null;
            return null;
        }
    }
}
