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
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Security;
    using System.Web.UI;
    using System.Web.UI.HtmlControls;
    using System.Web.UI.WebControls;

    using Util;

    public partial class Login : Page
    {
        #region Methods

        protected void LoginButton_Click(object sender, EventArgs e)
        {
            string userName = this.UserNameTextBox.Text.Trim();
            // Passwords can start and/or end with spaces
            string password = this.PasswordTextBox.Text;
            bool userNameMissing = this.CheckUserNameMissing(userName);
            bool passwordMissing = this.CheckPasswordMissing(password);
            bool invalidCombo = false;

            // Style controls by error
            this.UserNameTextBox.StyleByError(userNameMissing);
            this.PasswordTextBox.StyleByError(passwordMissing);

            // Determine if we need to show error rollup
            bool errors = userNameMissing || passwordMissing;

            if (!errors)
            {
                if (Membership.ValidateUser(userName, password))
                {
                    FormsAuthentication.RedirectFromLoginPage(userName, false);
                }
                else
                {
                    errors = true;
                    invalidCombo = true;
                }
            }

            // The value of errors is reset in the previous block. Resist
            // the temptation to fold this block into an "else"
            if (errors)
            {
                this.Notify.Show("Errors were encountered. Please see messages below.");
                this.SigninErrors.Visible = errors;
                //
                // Determine which parts of error rollup to show
                //
                new Dictionary<HtmlGenericControl, bool> { 
                { this.UserNameIsMissing, userNameMissing }, 
                { this.PasswordIsMissing, passwordMissing },
                { this.UnrecognizedLogin, invalidCombo } }
                    .ToggleVisibilityByError();
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void SignupButton_Click(object sender, EventArgs e)
        {
            string userName = this.NewUserNameTextBox.Text.Trim();
            string password = this.NewPasswordTextBox.Text;
            string email = this.EmailTextBox.Text.Trim();
            MembershipCreateStatus status = MembershipCreateStatus.Success;

            bool userNameIsMissing = this.CheckUserNameMissing(userName);
            bool emailAddressIsMissing = this.CheckEmailAddressMissing(email);
            bool passwordIsMissing = this.CheckPasswordMissing(password);
            bool passwordsAreNotMatched = this.CheckPasswordsDontMatch(password, this.ConfirmNewPasswordTextBox.Text);
            bool userNameInvalid = this.CheckUserName(userName);
            bool userNameTaken = false;
            bool emailAlreadyRegistered = false;

            // Determine if we need to show error rollup
            bool showSignUpErrors = userNameIsMissing ||
                passwordIsMissing ||
                passwordsAreNotMatched ||
                userNameInvalid;

            if (!showSignUpErrors)
            {
                Membership.Provider.CreateUser(
                    userName,
                    password,
                    email,
                    String.Empty,
                    String.Empty,
                    true,
                    Guid.NewGuid().ToString(),
                    out status
                    );

                userNameTaken = status == MembershipCreateStatus.DuplicateUserName;
                emailAlreadyRegistered = status == MembershipCreateStatus.DuplicateEmail;

                showSignUpErrors = showSignUpErrors || userNameTaken || emailAlreadyRegistered;
            }

            if (showSignUpErrors)
            {
                // Style controls by error
                this.NewUserNameTextBox.StyleByError(userNameIsMissing || userNameTaken || userNameInvalid);
                this.EmailTextBox.StyleByError(emailAddressIsMissing || emailAlreadyRegistered);
                this.NewPasswordTextBox.StyleByError(passwordIsMissing || passwordsAreNotMatched);
                this.ConfirmNewPasswordTextBox.StyleByError(passwordsAreNotMatched);

                // Determine which parts of error rollup to show
                new Dictionary<HtmlGenericControl, bool> 
                {
                    { this.NewUserNameIsMissing, userNameIsMissing },
                    { this.NewPasswordIsMissing, passwordIsMissing },
                    { this.UserNameTaken, userNameTaken },
                    { this.EmailAlreadyRegistered, emailAlreadyRegistered },
                    { this.PasswordsDontMatch, passwordsAreNotMatched },
                    { this.InvalidUserName, userNameInvalid }
                }.ToggleVisibilityByError();
            }

            this.SignupErrors.Visible = showSignUpErrors;
            if (showSignUpErrors)
            {
                this.Notify.Show("Errors were encountered. Please see messages below.");
            }
            else
            {
                if (status == MembershipCreateStatus.Success &&
                    Membership.ValidateUser(userName, password))
                {
                    FormsAuthentication.RedirectFromLoginPage(userName, false);
                }
            }
        }

        private bool CheckEmailAddressMissing(string emailAddressText)
        {
            return  String.IsNullOrEmpty(emailAddressText);
        }

        private bool CheckEmailAlreadyRegistered(string emailAddressText)
        {
            string userName = Membership.GetUserNameByEmail(emailAddressText);
            return !String.IsNullOrEmpty(userName);
        }

        private bool CheckPasswordMissing(string passwordText)
        {
            return String.IsNullOrEmpty(passwordText);
        }

        private bool CheckPasswordsDontMatch(string passwordText, string confirmPasswordText)
        {
            return (passwordText != confirmPasswordText);
        }

        private bool CheckUserNameMissing(string userName)
        {
            return String.IsNullOrEmpty(userName);
        }

        private bool CheckUserName(string userName)
        {
            string nameEsc = Uri.EscapeDataString(userName);
            return !userName.Equals(nameEsc);
        }

        private bool CheckUserNameTaken(string userName)
        {
            return Membership.GetUser(userName) != null;
        }

        #endregion
    }
}