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
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;

namespace AWS_ConsoleSample.Samples
{
    class AutoScalingSample
    {
        public static void InvokeDescribeAutoScalingGroups()
        {
            NameValueCollection appConfig = ConfigurationManager.AppSettings;

            // Print the number of Auto Scaling triggers.
            AmazonAutoScaling autoscaling = AWSClientFactory.CreateAmazonAutoScalingClient(
                appConfig["AWSAccessKey"],
                appConfig["AWSSecretKey"],
                RegionEndpoint.USWest2
                );

            try
            {
                DescribeAutoScalingGroupsResponse asResponse = autoscaling.DescribeAutoScalingGroups(new DescribeAutoScalingGroupsRequest());

                int numAutoScalingGroups = 0;
                if (asResponse.DescribeAutoScalingGroupsResult.AutoScalingGroups != null)
                {
                    numAutoScalingGroups = asResponse.DescribeAutoScalingGroupsResult.AutoScalingGroups.Count;
                }
                Console.WriteLine("You have " + numAutoScalingGroups + " Auto Scaling group(s).");
            }
            catch (AmazonAutoScalingException ex)
            {
                if (ex.ErrorCode.Equals("OptInRequired"))
                {
                    Console.WriteLine("You are not signed for Amazon EC2.");
                    Console.WriteLine("You can sign up at http://aws.amazon.com/ec2.");
                }
                else
                {
                    Console.WriteLine("Caught Exception: " + ex.Message);
                    Console.WriteLine("Response Status Code: " + ex.StatusCode);
                    Console.WriteLine("Error Code: " + ex.ErrorCode);
                    Console.WriteLine("Error Type: " + ex.ErrorType);
                    Console.WriteLine("Request ID: " + ex.RequestId);
                }
            }
            Console.WriteLine();
        }
    }
}
