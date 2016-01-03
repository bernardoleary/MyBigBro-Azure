/*******************************************************************************
* Copyright 2009-2012 Amazon.com, Inc. or its affiliates. All Rights Reserved.
* 
* Licensed under the Apache License, Version 2.0 (the "License"). You may
* not use this file except in compliance with the License. A copy of the
* License is located at
* 
* http://aws.amazon.com/apache2.0/
* 
* or in the "license" file accompanying this file. This file is
* distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
* KIND, either express or implied. See the License for the specific
* language governing permissions and limitations under the License.
*******************************************************************************/

using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace GettingStartedGuide
{
    class S3Sample
    {
        // Change the accessKeyID and the secretAccessKeyID to your credentials in the App.config file.
        // See http://aws.amazon.com/credentials  for more details.
        // You must also sign up for an Amazon S3 account for this to work
        // See http://aws.amazon.com/s3/ for details on creating an Amazon S3 account
        // Change the bucketName and keyName fields to values that match your bucketname and keyname
        static string bucketName;
        static string keyName;
        static AmazonS3 client;

        public static void Main(string[] args)
        {
            if (checkRequiredFields())
            {
                using (client = Amazon.AWSClientFactory.CreateAmazonS3Client(RegionEndpoint.USWest2))
                {
                    Console.WriteLine("Listing buckets");
                    ListingBuckets();

                    Console.WriteLine("Creating a bucket");
                    CreateABucket();

                    Console.WriteLine("Writing an object");
                    WritingAnObject();

                    Console.WriteLine("Reading an object");
                    ReadingAnObject();

                    Console.WriteLine("Deleting an object");
                    DeletingAnObject();

                    Console.WriteLine("Listing objects");
                    ListingObjects();
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static bool checkRequiredFields()
        {
            NameValueCollection appConfig = ConfigurationManager.AppSettings;

            if (string.IsNullOrEmpty(appConfig["AWSAccessKey"]))
            {
                Console.WriteLine("AWSAccessKey was not set in the App.config file.");
                return false;
            }
            if (string.IsNullOrEmpty(appConfig["AWSSecretKey"]))
            {
                Console.WriteLine("AWSSecretKey was not set in the App.config file.");
                return false;
            }
            if (string.IsNullOrEmpty(bucketName))
            {
                Console.WriteLine("The variable bucketName is not set.");
                return false;
            }
            if (string.IsNullOrEmpty(keyName))
            {
                Console.WriteLine("The variable keyName is not set.");
                return false;
            }

            return true;
        }

        static void ListingBuckets()
        {
            try
            {
                using (ListBucketsResponse response = client.ListBuckets())
                {
                    foreach (S3Bucket bucket in response.Buckets)
                    {
                        Console.WriteLine("You own Bucket with name: {0}", bucket.BucketName);
                    }
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An Error, number {0}, occurred when listing buckets with the message '{1}", amazonS3Exception.ErrorCode, amazonS3Exception.Message);
                }
            }
        }

        static void CreateABucket()
        {
            try
            {
                PutBucketRequest request = new PutBucketRequest();
                request.BucketName = bucketName;
                client.PutBucket(request);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null && (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") || amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An Error, number {0}, occurred when creating a bucket with the message '{1}", amazonS3Exception.ErrorCode, amazonS3Exception.Message);
                }
            }
        }

        static void WritingAnObject()
        {
            try
            {
                // simple object put
                PutObjectRequest request = new PutObjectRequest();
                request.WithContentBody("this is a test")
                    .WithBucketName(bucketName)
                    .WithKey(keyName);

                S3Response response = client.PutObject(request);
                response.Dispose();

                // put a more complex object with some metadata and http headers.
                PutObjectRequest titledRequest = new PutObjectRequest();
                titledRequest.WithMetaData("title", "the title")
                    .WithContentBody("this object has a title")
                    .WithBucketName(bucketName)
                    .WithKey(keyName);

                using (S3Response responseWithMetadata = client.PutObject(titledRequest))
                {
                    WebHeaderCollection headers = response.Headers;
                    foreach (string key in headers.Keys)
                    {
                        Console.WriteLine("Response Header: {0}, Value: {1}", key, headers.Get(key));
                    }
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when writing an object", amazonS3Exception.Message);
                }
            }
        }

        static void ReadingAnObject()
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest().WithBucketName(bucketName).WithKey(keyName);

                using (GetObjectResponse response = client.GetObject(request))
                {
                    string title = response.Metadata["x-amz-meta-title"];
                    Console.WriteLine("The object's title is {0}", title);
                    string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), keyName);
                    if (!File.Exists(dest))
                    {
                        response.WriteResponseStreamToFile(dest);
                    }
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when reading an object", amazonS3Exception.Message);
                }
            }
        }

        static void DeletingAnObject()
        {
            try
            {
                DeleteObjectRequest request = new DeleteObjectRequest();
                request.WithBucketName(bucketName)
                    .WithKey(keyName);
                using (DeleteObjectResponse response = client.DeleteObject(request))
                {
                    WebHeaderCollection headers = response.Headers;
                    foreach (string key in headers.Keys)
                    {
                        Console.WriteLine("Response Header: {0}, Value: {1}", key, headers.Get(key));
                    }
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when deleting an object", amazonS3Exception.Message);
                }
            }
        }

        static void ListingObjects()
        {
            try
            {
                ListObjectsRequest request = new ListObjectsRequest();
                request.BucketName = bucketName;
                using (ListObjectsResponse response = client.ListObjects(request))
                {
                    foreach (S3Object entry in response.S3Objects)
                    {
                        Console.WriteLine("key = {0} size = {1}", entry.Key, entry.Size);
                    }
                }

                // list only things starting with "foo"
                request.WithPrefix("foo");
                using (ListObjectsResponse response = client.ListObjects(request))
                {
                    foreach (S3Object entry in response.S3Objects)
                    {
                        Console.WriteLine("key = {0} size = {1}", entry.Key, entry.Size);
                    }
                }

                // list only things that come after "bar" alphabetically
                request.WithPrefix(null)
                    .WithMarker("bar");
                using (ListObjectsResponse response = client.ListObjects(request))
                {
                    foreach (S3Object entry in response.S3Objects)
                    {
                        Console.WriteLine("key = {0} size = {1}", entry.Key, entry.Size);
                    }
                }

                // only list 3 things
                request.WithPrefix(null)
                    .WithMarker(null)
                    .WithMaxKeys(3);
                using (ListObjectsResponse response = client.ListObjects(request))
                {
                    foreach (S3Object entry in response.S3Objects)
                    {
                        Console.WriteLine("key = {0} size = {1}", entry.Key, entry.Size);
                    }
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null && (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") || amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when listing objects", amazonS3Exception.Message);
                }
            }
        }
    }
}