/*******************************************************************************
 *  Copyright 2008-2012 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *  Licensed under the Apache License, Version 2.0 (the "License"). You may not use
 *  this file except in compliance with the License. A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 *  or in the "license" file accompanying this file.
 *  This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 *  CONDITIONS OF ANY KIND, either express or implied. See the License for the
 *  specific language governing permissions and limitations under the License.
 * *****************************************************************************/
namespace Petboard.Util
{
    using System;
    using System.Linq;
    using System.Web;

    using Amazon.S3;
    using Amazon.S3.Model;

    using Properties;

    public static class BucketHelper
    {
        #region Public Methods

        public static void CheckForBucket(string itemKey, AmazonS3 s3Client)
        {
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                string userBucketName = String.Format(Settings.Default.BucketNameFormat, HttpContext.Current.User.Identity.Name, itemKey);
                using (ListBucketsResponse listBucketsResponse = s3Client.ListBuckets())
                {
                    S3Bucket bucket = listBucketsResponse.Buckets.FirstOrDefault(b => b.BucketName == userBucketName);
                    if (bucket == null)
                    {
                        PutBucketRequest putBucketRequest = new PutBucketRequest()
                            .WithBucketName(userBucketName);
                        PutBucketResponse putBucketResponse = s3Client.PutBucket(putBucketRequest);
                        putBucketResponse.Dispose();
                    }
                }
            }
        }

        #endregion
    }
}