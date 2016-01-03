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
using System.Net;
using System.Text;
using System.Threading;

using Amazon;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;

namespace AmazonSimpleDB_AsyncSample
{
    class Program
    {
        const int MAX_ROWS = 50;

        static AmazonSimpleDB sdb;

        // Change the accessKeyID and the secretAccessKeyID to your credentials in the App.config file.
        // See http://aws.amazon.com/credentials  for more details.
        //
        // This sample adds 50 items in a domain using the synchronized API and then
        // the asynchronized API.
        static void Main(string[] args)
        {
            sdb = AWSClientFactory.CreateAmazonSimpleDBClient(RegionEndpoint.USWest2);

            addDataSynchronized();

            addDataAsynchronized();
            
            Console.WriteLine("Press Enter to continue...");
            Console.Read();
        }

        /// <summary>
        /// Add 50 items using the synchronized API.
        /// </summary>
        static void addDataSynchronized()
        {
            Console.WriteLine("Start testing synchronized method.");
            string domainName = "AsyncDomain";
            sdb.CreateDomain(new CreateDomainRequest()
                .WithDomainName(domainName));

            Results results = new Results();
            try
            {
                long start = DateTime.Now.Ticks;

                for (int i = 0; i < MAX_ROWS; i++)
                {
                    try
                    {
                        PutAttributesRequest request = new PutAttributesRequest()
                            .WithDomainName(domainName)
                            .WithItemName("ItemName" + i)
                            .WithAttribute(new ReplaceableAttribute()
                                .WithName("Value")
                                .WithValue(i.ToString()));
                        sdb.PutAttributes(request);
                        results.Successes++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        results.Errors++;
                    }
                }

                TimeSpan ts = new TimeSpan(DateTime.Now.Ticks - start);
                Console.WriteLine("Time: {0} ms Successes: {1} Errors: {2}", ts.TotalMilliseconds, results.Successes, results.Errors);
            }
            finally
            {
                sdb.DeleteDomain(new DeleteDomainRequest()
                    .WithDomainName(domainName));
            }
        }

        /// <summary>
        /// Add 50 items using the asynchronized API.
        /// </summary>
        static void addDataAsynchronized()
        {
            Console.WriteLine("Start testing asynchronized method.");
            string domainName = "AsyncDomain";
            sdb.CreateDomain(new CreateDomainRequest()
                .WithDomainName(domainName));

            Results results = new Results();
            List<WaitHandle> waitHandles = new List<WaitHandle>();
            try
            {
                long start = DateTime.Now.Ticks;
                for (int i = 0; i < MAX_ROWS; i++)
                {
                    PutAttributesRequest request = new PutAttributesRequest()
                        .WithDomainName(domainName)
                        .WithItemName("ItemName" + i)
                        .WithAttribute(new ReplaceableAttribute()
                            .WithName("Value")
                            .WithValue(i.ToString()));

                    // Start the put attributes operation.  The callback method will be called when the put attributes operation
                    // is complete or an error occurs.
                    IAsyncResult asyncResult = sdb.BeginPutAttributes(request, new AsyncCallback(Program.callBack), results);
                    waitHandles.Add(asyncResult.AsyncWaitHandle);
                }

                // Wait till all the requests that were started are completed.
                WaitHandle.WaitAll(waitHandles.ToArray());

                TimeSpan ts = new TimeSpan(DateTime.Now.Ticks - start);
                Console.WriteLine("Time: {0} ms Successes: {1} Errors: {2}", ts.TotalMilliseconds, results.Successes, results.Errors);
            }
            finally
            {
                sdb.DeleteDomain(new DeleteDomainRequest()
                    .WithDomainName(domainName));
            }
        }

        /// <summary>
        /// Callback method for the asynchronized method. 
        /// </summary>
        /// <param name="result"></param>
        static void callBack(IAsyncResult result)
        {
            Results results = result.AsyncState as Results;
            try
            {
                // If there was an error during the put attributes operation it will be thrown as part of the EndPutAttributes method.
                sdb.EndPutAttributes(result);
                results.Successes++;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                results.Errors++;
            }
        }

        public class Results
        {
            public int Successes;
            public int Errors;
        }
    }
}
