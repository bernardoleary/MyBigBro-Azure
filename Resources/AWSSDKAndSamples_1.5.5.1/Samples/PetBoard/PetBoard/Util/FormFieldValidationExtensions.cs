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
namespace Petboard.Util
{
    using System;
    using System.Collections.Generic;
    using System.Web.UI.HtmlControls;
    using System.Web.UI.WebControls;

    public static class FormFieldValidationExtensions
    {
        #region Constants and Fields

        private static readonly string ErrorInTextBoxClassName = "error_in_textbox";

        #endregion

        #region Public Methods

        public static void StyleByError(this TextBox textBox, bool error)
        {
            if (error)
            {
                if (String.IsNullOrEmpty(textBox.CssClass))
                {
                    textBox.CssClass += ErrorInTextBoxClassName;
                }
                else
                {
                    textBox.CssClass = String.Concat(textBox.CssClass, " ", ErrorInTextBoxClassName);
                }
            }
            else
            {
                textBox.CssClass = textBox.CssClass.Replace(ErrorInTextBoxClassName, String.Empty).Trim();
            }
        }

        public static void ToggleVisibilityByError(this Dictionary<HtmlGenericControl, bool> controls)
        {
            foreach (var control in controls)
            {
                control.Key.Visible = control.Value;
            }
        }

        #endregion
    }
}