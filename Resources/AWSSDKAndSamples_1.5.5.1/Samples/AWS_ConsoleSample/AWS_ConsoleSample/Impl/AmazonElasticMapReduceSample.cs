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
using Amazon.ElasticMapReduce;
using Amazon.ElasticMapReduce.Model;

namespace AWS_ConsoleSample.Samples
{
    class AmazonElasticMapReduceSample
    {
        public static void InvokeDescribeJobFlows()
        {
            NameValueCollection appConfig = ConfigurationManager.AppSettings;

            // Print the number of Amazon SimpleDB domains.
            AmazonElasticMapReduce emr = AWSClientFactory.CreateAmazonElasticMapReduceClient(
                appConfig["AWSAccessKey"],
                appConfig["AWSSecretKey"],
                RegionEndpoint.USWest2
                );

            try
            {
                DescribeJobFlowsResponse emrResponse = emr.DescribeJobFlows(new DescribeJobFlowsRequest());

                if (emrResponse.DescribeJobFlowsResult != null)
                {
                    int numFlows = 0;
                    numFlows = emrResponse.DescribeJobFlowsResult.JobFlows.Count;
                    Console.WriteLine("You have " + numFlows + " Amazon Elastic MapReduce job flow(s).");
                }
            }
            catch (AmazonElasticMapReduceException ex)
            {
                if (ex.ErrorCode.Equals("OptInRequired"))
                {
                    Console.WriteLine("You are not signed for Amazon Elastic MapReduce.");
                    Console.WriteLine("You can sign up at http://aws.amazon.com/elasticmapreduce.");
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
