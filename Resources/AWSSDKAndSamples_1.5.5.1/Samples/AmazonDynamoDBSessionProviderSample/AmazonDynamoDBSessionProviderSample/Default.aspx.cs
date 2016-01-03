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
using System.Web;
using System.Web.UI;


using System.Text;
using System.IO;

using Amazon;
using Amazon.DynamoDB;
using Amazon.DynamoDB.DocumentModel;

using Amazon.SessionProvider;

namespace AmazonDynamoDBSessionProviderSample
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Add some session data.
            if (this.Session["PageCount"] == null)
                this.Session["PageCount"] = 1;
            else
                this.Session["PageCount"] = Convert.ToInt32(this.Session["PageCount"]) + 1;


            // Look at the DynamoDB table
            this.InspectSessionTable();
        }

        public int PageCount
        {
            get
            {
                if (this.Session["PageCount"] == null)
                    return 1;

                return Convert.ToInt32(this.Session["PageCount"]);
            }
        }

        /// <summary>
        /// This method will show where the session state data is stored in Amazon DynamoDB.
        /// This is just for demonstration purposes and normal use of the session is done 
        /// through the Context.Session property.
        /// </summary>
        private void InspectSessionTable()
        {
            using (StringWriter sr = new StringWriter())
            {
                try
                {
                    var dynamoDb = AWSClientFactory.CreateAmazonDynamoDBClient(RegionEndpoint.USWest2);

                    var table = Table.LoadTable(dynamoDb, "ASP.NET_SessionState");

                    sr.WriteLine("<li>Table: {0}</li>", table.TableName);
                    sr.WriteLine("<li>Hash Key: {0}</li>", table.HashKeyName);

                    var item = table.GetItem(this.Context.Session.SessionID);
                    if (item == null || !item.Contains(DynamoDBSessionStateStore.ATTRIBUTE_CREATE_DATE))
                    {
                        sr.WriteLine("<li>Session Not Persisted Yet</li>");
                    }
                    else
                    {                      
                        sr.WriteLine("<li>Session Created: {0}</li>", (DateTime)item[DynamoDBSessionStateStore.ATTRIBUTE_CREATE_DATE]);
                        sr.WriteLine("<li>Session Expires: {0}</li>", (DateTime)item[DynamoDBSessionStateStore.ATTRIBUTE_EXPIRES]);
                    }
                }
                catch (AmazonDynamoDBException ex)
                {
                    if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
                    {
                        sr.WriteLine("The account you are using is not signed up for Amazon DynamoDB.");
                        sr.WriteLine("<br />");
                        sr.WriteLine("You can sign up for Amazon DynamoDB at http://aws.amazon.com/dynamodb/");
                        sr.WriteLine("<br />");
                        sr.WriteLine("<br />");
                    }
                    else
                    {
                        sr.WriteLine("Caught Exception: " + ex.Message);
                        sr.WriteLine("<br />");
                        sr.WriteLine("Response Status Code: " + ex.StatusCode);
                        sr.WriteLine("<br />");
                        sr.WriteLine("Error Code: " + ex.ErrorCode);
                        sr.WriteLine("<br />");
                        sr.WriteLine("Error Type: " + ex.ErrorType);
                        sr.WriteLine("<br />");
                        sr.WriteLine("<br />");
                    }
                }

                this.dynamoDBPlaceholder.Text = sr.ToString();
            }
        }
    }
}