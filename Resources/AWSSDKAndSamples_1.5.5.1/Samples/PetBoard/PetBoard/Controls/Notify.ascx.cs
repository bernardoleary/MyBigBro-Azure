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
namespace Petboard.Controls
{
    using System;
    using System.Web.UI;

    public partial class Notify : UserControl
    {
        #region Properties

        public string CssClass { get; set; }

        #endregion

        #region Public Methods

        public void Show(string message)
        {
            this.container.Visible = true;
            this.container.Attributes["class"] = this.CssClass;
            this.Notification.Text = message;
        }

        #endregion

        #region Methods

        protected void Page_Load(object sender, EventArgs e)
        {
            this.Notification.Text = String.Empty;
            this.container.Visible = false;
        }

        #endregion
    }
}