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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.Threading;

using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace GettingStartedGuide
{
    class AmazonCloudWatchSample
    {
        static void Main(string[] args)
        {
            PerformanceCounter percentPageFile = new PerformanceCounter("Paging File", "% Usage",      "_Total");
            PerformanceCounter peakPageFile    = new PerformanceCounter("Paging File", "% Usage Peak", "_Total");


            AmazonCloudWatch client = Amazon.AWSClientFactory.CreateAmazonCloudWatchClient(RegionEndpoint.USWest2);

            // Once a minute, send paging file usage statistics to CloudWatch
            for (; ; )
            {
                IList<MetricDatum> data = new List<MetricDatum>();

                data.Add(new MetricDatum()
                    .WithMetricName("PagingFilePctUsage")
                    .WithTimestamp(DateTime.Now)
                    .WithUnit("Percent")
                    .WithValue(percentPageFile.NextValue()));

                data.Add(new MetricDatum()
                    .WithMetricName("PagingFilePctUsagePeak")
                    .WithTimestamp(DateTime.Now)
                    .WithUnit("Percent")
                    .WithValue(peakPageFile.NextValue()));
                    

                client.PutMetricData(new PutMetricDataRequest()
                    .WithMetricData(data)
                    .WithNamespace("System/Windows"));
                    
                Thread.Sleep(1000 * 60);
            }
        }
    }

    
}
