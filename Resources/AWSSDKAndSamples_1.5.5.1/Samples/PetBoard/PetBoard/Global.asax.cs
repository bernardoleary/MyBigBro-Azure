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
namespace Petboard
{
    using System;
    using System.Web;
    using System.Web.Security;

    public class Global : HttpApplication
    {
        #region Methods

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
        }

        protected void Application_End(object sender, EventArgs e)
        {
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Server.Transfer("~/Error.aspx");
        }

        protected void Application_Start(object sender, EventArgs e)
        {
            ((SimpleDbMembershipProvider)Membership.Provider).MembershipProviderConfigError += MembershipProviderConfigError;
        }

        protected void Session_End(object sender, EventArgs e)
        {
        }

        protected void Session_Start(object sender, EventArgs e)
        {
        }

        private static void MembershipProviderConfigError(object sender, EventArgs e)
        {
            if (HttpContext.Current.Request.Path.ToLowerInvariant() != "/setup.aspx")
            {
                HttpContext.Current.Response.Redirect("~/Setup.aspx");
            }
        }

        #endregion
    }
}