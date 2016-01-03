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
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;

namespace AWS_ConsoleSample.Samples
{
    class AmazonSimpleDBListDomainsSample
    {
        public static void InvokeListDomains()
        {
            NameValueCollection appConfig = ConfigurationManager.AppSettings;

            // Print the number of Amazon SimpleDB domains.
            AmazonSimpleDB sdb = AWSClientFactory.CreateAmazonSimpleDBClient(
                appConfig["AWSAccessKey"],
                appConfig["AWSSecretKey"],
                RegionEndpoint.USWest2
                );

            try
            {
                ListDomainsResponse sdbResponse = sdb.ListDomains(new ListDomainsRequest());

                if (sdbResponse.IsSetListDomainsResult())
                {
                    int numDomains = 0;
                    numDomains = sdbResponse.ListDomainsResult.DomainName.Count;
                    Console.WriteLine("You have " + numDomains + " Amazon SimpleDB domain(s).");
                }
            }
            catch (AmazonSimpleDBException ex)
            {
                if (ex.ErrorCode.Equals("AuthFailure"))
                {
                    Console.WriteLine("Please check the provided AWS access credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon SimpleDB yet, you can sign up at http://aws.amazon.com/simpledb.");
                }
                else
                {
                    Console.WriteLine("Caught Exception: " + ex.Message);
                    Console.WriteLine("Response Status Code: " + ex.StatusCode);
                    Console.WriteLine("Error Code: " + ex.ErrorCode);
                    Console.WriteLine("Error Type: " + ex.ErrorType);
                    Console.WriteLine("Request ID: " + ex.RequestId);
                    Console.WriteLine("XML: " + ex.XML);
                }
            }
            Console.WriteLine();
        }
    }
}
