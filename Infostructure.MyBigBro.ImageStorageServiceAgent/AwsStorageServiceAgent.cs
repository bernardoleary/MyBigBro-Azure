using System;
using System.IO;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Infostructure.MyBigBro.Domain;
using Microsoft.Extensions.Options;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent
{
    public class AwsStorageServiceAgent : IStorageServiceAgent
    {
        private readonly StorageOptions _options;

        public AwsStorageServiceAgent()
        {
            _options = new StorageOptions();
        }

        public AwsStorageServiceAgent(IOptions<StorageOptions> options)
        {
            _options = options?.Value ?? new StorageOptions();
        }

        public AwsStorageServiceAgent(StorageOptions options)
        {
            _options = options ?? new StorageOptions();
        }

        public string PutImage(IWebCamImage webCamImage, out string key)
        {
            if (webCamImage?.Data != null)
            {
                string fileName = Guid.NewGuid() + ".jpg";
                var s3Config = new AmazonS3Config
                {
                    ForcePathStyle = true
                };
                if (!string.IsNullOrWhiteSpace(_options.AwsServiceUrl))
                {
                    s3Config.ServiceURL = _options.AwsServiceUrl;
                }

                var credentials = new BasicAWSCredentials(_options.AwsAccessKey, _options.AwsSecretKey);
                using (var client = new AmazonS3Client(credentials, s3Config))
                {
                    bool bucketExists = AmazonS3Util.DoesS3BucketExistV2Async(client, _options.AwsBucket)
                        .GetAwaiter().GetResult();
                    if (!bucketExists)
                    {
                        client.PutBucketAsync(new PutBucketRequest
                        {
                            BucketName = _options.AwsBucket,
                            UseClientRegion = true
                        }).GetAwaiter().GetResult();
                    }

                    using (var inputStream = new MemoryStream(webCamImage.Data))
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _options.AwsBucket,
                            Key = fileName,
                            InputStream = inputStream,
                            ContentType = "image/jpeg",
                            CannedACL = S3CannedACL.PublicRead
                        };
                        client.PutObjectAsync(putRequest).GetAwaiter().GetResult();
                    }
                }

                string rootUrl = (_options.PublicRootUrl ?? _options.AwsServiceUrl ?? "http://localhost:9000").TrimEnd('/');
                string url = rootUrl.EndsWith("/" + _options.AwsBucket, StringComparison.OrdinalIgnoreCase)
                    ? $"{rootUrl}/{fileName}"
                    : $"{rootUrl}/{_options.AwsBucket}/{fileName}";

                key = fileName;
                return url;
            }

            key = null;
            return null;
        }
    }
}
