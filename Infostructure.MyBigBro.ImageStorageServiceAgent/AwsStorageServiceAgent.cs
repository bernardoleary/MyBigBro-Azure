using System;
using System.Configuration;
using System.IO;
using Amazon.S3;
using Amazon.S3.Model;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent
{
    public class AwsStorageServiceAgent : IStorageServiceAgent
    {
        public string PutImage(IWebCamImage webCamImage, out string key)
        {
            if (webCamImage.Data != null) // accept the file
            {
                // AWS credentials.
                const string accessKey = "{sensitive}";
                const string secretKey = "{sensitive}";

                // Setup the filename.
                string fileName = Guid.NewGuid() + ".jpg";
                string url = null;

                // Drop the image.
                AmazonS3 client;
                using (client = Amazon.AWSClientFactory.CreateAmazonS3Client(accessKey, secretKey))
                {                    
                    var request = new PutObjectRequest();
                    var config = ConfigurationManager.AppSettings;
                    request.WithBucketName(config["awsBucket"])
                        .WithCannedACL(S3CannedACL.PublicRead)
                        .WithKey(fileName).WithInputStream(new MemoryStream(webCamImage.Data));
                    S3Response response = client.PutObject(request);
                    url = config["awsRootUrl"] + "/" + config["awsBucket"] + "/" + fileName;
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
