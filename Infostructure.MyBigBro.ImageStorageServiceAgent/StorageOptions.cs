using System;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent
{
    public class StorageOptions
    {
        public string Provider { get; set; } =
            Environment.GetEnvironmentVariable("Storage__Provider") ?? "Azure";

        public string AzureConnectionString { get; set; } =
            Environment.GetEnvironmentVariable("Storage__AzureConnectionString")
            ?? "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:10000/devstoreaccount1;";

        public string AzureContainer { get; set; } =
            Environment.GetEnvironmentVariable("Storage__AzureContainer") ?? "images";

        public string PublicRootUrl { get; set; } =
            Environment.GetEnvironmentVariable("Storage__PublicRootUrl")
            ?? "http://localhost:10000/devstoreaccount1";

        public string AwsBucket { get; set; } =
            Environment.GetEnvironmentVariable("Storage__AwsBucket") ?? "mybigbro";

        public string AwsServiceUrl { get; set; } =
            Environment.GetEnvironmentVariable("Storage__AwsServiceUrl") ?? "http://localhost:9000";

        public string AwsAccessKey { get; set; } =
            Environment.GetEnvironmentVariable("Storage__AwsAccessKey") ?? "minioadmin";

        public string AwsSecretKey { get; set; } =
            Environment.GetEnvironmentVariable("Storage__AwsSecretKey") ?? "minioadmin123";
    }
}
