using System;
using Infostructure.MyBigBro.Domain;
using Microsoft.Extensions.Options;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent
{
    public class ConfigurableStorageServiceAgent : IStorageServiceAgent
    {
        private readonly IStorageServiceAgent _innerAgent;

        public ConfigurableStorageServiceAgent(IOptions<StorageOptions> options)
        {
            var opts = options?.Value ?? new StorageOptions();
            if (string.Equals(opts.Provider, "MinIO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(opts.Provider, "S3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(opts.Provider, "Aws", StringComparison.OrdinalIgnoreCase))
            {
                _innerAgent = new AwsStorageServiceAgent(opts);
            }
            else
            {
                _innerAgent = new AzureStorageServiceAgent(opts);
            }
        }

        public string PutImage(IWebCamImage webCamImageData, out string key)
        {
            return _innerAgent.PutImage(webCamImageData, out key);
        }
    }
}
