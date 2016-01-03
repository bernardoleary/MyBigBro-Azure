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

namespace AWS_ConsoleSample
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("AWS .NET SDK Sample Project");
            Console.WriteLine("===========================================");

            //Print the number of Amazon EC2 instances.
            AWS_ConsoleSample.Samples.AmazonEC2DescribeInstancesSample.InvokeListInstances();

            //Print the number of Elastic Load Balancing load balancers.
            AWS_ConsoleSample.Samples.ElasticLoadBalancingSample.InvokeDescribeLoadBalancers();

            //Print the number of Amazon CloudWatch metrics.
            AWS_ConsoleSample.Samples.AmazonCloudWatchSample.InvokeListMetrics();

            //Print the number of Auto Scaling triggers.
            AWS_ConsoleSample.Samples.AutoScalingSample.InvokeDescribeAutoScalingGroups();

            //Print the number of Amazon SimpleDB domains.
            AWS_ConsoleSample.Samples.AmazonSimpleDBListDomainsSample.InvokeListDomains();

            // Print the number of Amazon S3 buckets.
            AWS_ConsoleSample.Samples.AmazonS3ListBucketsSample.InvokeListBuckets();

            //Print the number of Amazon CloudFront distributions.
            //TODO: Add CloudFront when it becomes available.

            //Print the number of Amazon SQS queues.
            AWS_ConsoleSample.Samples.AmazonSQSListQueuesSample.InvokeListQueues();

            //Print the number of Elastic MapReduce jobs.
            AWS_ConsoleSample.Samples.AmazonElasticMapReduceSample.InvokeDescribeJobFlows();

            //Print the number of Amazon RDS databases.
            //TODO: Add RDS when it becomes available.

            Console.WriteLine("Press Enter to continue...");
            Console.Read();
        }
    }
}