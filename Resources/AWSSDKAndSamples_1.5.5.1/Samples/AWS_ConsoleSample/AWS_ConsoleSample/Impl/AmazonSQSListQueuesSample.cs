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
using Amazon.SQS;
using Amazon.SQS.Model;

namespace AWS_ConsoleSample.Samples
{
    class AmazonSQSListQueuesSample
    {
        public static void InvokeListQueues()
        {
            NameValueCollection appConfig = ConfigurationManager.AppSettings;

            // Print the number of Amazon SimpleDB domains.
            AmazonSQS sqs = AWSClientFactory.CreateAmazonSQSClient(
                appConfig["AWSAccessKey"],
                appConfig["AWSSecretKey"],
                RegionEndpoint.USWest2
                );

            try
            {
                ListQueuesResponse sqsResponse = sqs.ListQueues(new ListQueuesRequest());

                if (sqsResponse.IsSetListQueuesResult())
                {
                    int numQueues = 0;
                    numQueues = sqsResponse.ListQueuesResult.QueueUrl.Count;
                    Console.WriteLine("You have " + numQueues + " Amazon SQS queues(s).");
                }
            }
            catch (AmazonSQSException ex)
            {
                Console.WriteLine("Caught Exception: " + ex.Message);
                Console.WriteLine("Response Status Code: " + ex.StatusCode);
                Console.WriteLine("Error Code: " + ex.ErrorCode);
                Console.WriteLine("Error Type: " + ex.ErrorType);
                Console.WriteLine("Request ID: " + ex.RequestId);
                Console.WriteLine("XML: " + ex.XML);
            }
            Console.WriteLine();
        }
    }
}
