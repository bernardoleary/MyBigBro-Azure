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

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace AWS_ConsoleSample.Samples
{
    class AmazonS3ListBucketsSample
    {
        public static void InvokeListBuckets()
        {
            NameValueCollection appConfig = ConfigurationManager.AppSettings;

            AmazonS3 s3Client = AWSClientFactory.CreateAmazonS3Client(
                appConfig["AWSAccessKey"],
                appConfig["AWSSecretKey"],
                RegionEndpoint.USWest2
            );

            try
            {
                ListBucketsResponse response = s3Client.ListBuckets();
                int numBuckets = 0;
                numBuckets = response.Buckets.Count;

                Console.WriteLine("You have " + numBuckets + " Amazon S3 bucket(s).");
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.ErrorCode.Equals("InvalidAccessKeyId") ||
                    ex.ErrorCode.Equals("InvalidSecurity"))
                {
                    Console.WriteLine("Please check the provided AWS access credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3 yet, you can sign up at http://aws.amazon.com/s3.");
                }
                else
                {
                    Console.WriteLine("Caught Exception: " + ex.Message);
                    Console.WriteLine("Response Status Code: " + ex.StatusCode);
                    Console.WriteLine("Error Code: " + ex.ErrorCode);
                    Console.WriteLine("Request ID: " + ex.RequestId);
                    Console.WriteLine("XML: " + ex.XML);
                }
            }
            Console.WriteLine();
        }
    }
}
