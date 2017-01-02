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
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Configuration.Provider;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Configuration;
    using System.Web.Hosting;
    using System.Web.Security;

    using Amazon.SimpleDB;
    using Amazon.SimpleDB.Model;

    using Properties;
    using Util;

    using Attribute = Amazon.SimpleDB.Model.Attribute;

    public sealed class SimpleDbMembershipProvider : MembershipProvider
    {
        #region Constants and Fields

        private readonly bool _enablePasswordReset;

        private readonly bool _enablePasswordRetrieval;

        private readonly int _maxInvalidPasswordAttempts;

        private readonly int _minRequiredNonAlphanumericCharacters;

        private readonly int _minRequiredPasswordLength;

        private readonly int _passwordAttemptWindow;

        private readonly string _passwordStrengthRegularExpression;

        private readonly bool _requiresQuestionAndAnswer;

        private readonly bool _requiresUniqueEmail;

        private AmazonSimpleDBClient _simpleDBClient;

        private MachineKeySection _machineKey;

        private MembershipPasswordFormat _passwordFormat;

        static readonly string[] PasswordStrings = { "Password", "PasswordAnswer" };

        #endregion

        #region Constructors and Destructors

        public SimpleDbMembershipProvider()
        {
        }

        public SimpleDbMembershipProvider(bool enablePasswordReset, bool enablePasswordRetrieval, int maxInvalidPasswordAttempts, int minRequiredNonAlphanumericCharacters, int minRequiredPasswordLength, int passwordAttemptWindow, string passwordStrengthRegularExpression, bool requiresQuestionAndAnswer, bool requiresUniqueEmail)
        {
            this._enablePasswordReset = enablePasswordReset;
            this._requiresUniqueEmail = requiresUniqueEmail;
            this._requiresQuestionAndAnswer = requiresQuestionAndAnswer;
            this._passwordStrengthRegularExpression = passwordStrengthRegularExpression;
            this._passwordAttemptWindow = passwordAttemptWindow;
            this._minRequiredPasswordLength = minRequiredPasswordLength;
            this._minRequiredPasswordLength = minRequiredPasswordLength;
            this._minRequiredNonAlphanumericCharacters = minRequiredNonAlphanumericCharacters;
            this._minRequiredNonAlphanumericCharacters = minRequiredNonAlphanumericCharacters;
            this._maxInvalidPasswordAttempts = maxInvalidPasswordAttempts;
            this._enablePasswordRetrieval = enablePasswordRetrieval;
        }

        // Ensure that the SimpleDB Client's managed and unmanaged resources are released.
        ~SimpleDbMembershipProvider()
        {
            this._simpleDBClient.Dispose();
        }

        #endregion

        #region Events

        public event EventHandler MembershipProviderConfigError;

        #endregion

        #region Properties

        public override string ApplicationName { get; set; }

        public override bool EnablePasswordReset
        {
            get
            {
                return this._enablePasswordReset;
            }
        }

        public override bool EnablePasswordRetrieval
        {
            get
            {
                return this._enablePasswordRetrieval;
            }
        }

        public override int MaxInvalidPasswordAttempts
        {
            get
            {
                return this._maxInvalidPasswordAttempts;
            }
        }

        public override int MinRequiredNonAlphanumericCharacters
        {
            get
            {
                return this._minRequiredNonAlphanumericCharacters;
            }
        }

        public override int MinRequiredPasswordLength
        {
            get
            {
                return this._minRequiredPasswordLength;
            }
        }

        public override int PasswordAttemptWindow
        {
            get
            {
                return this._passwordAttemptWindow;
            }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get
            {
                return this._passwordFormat;
            }
        }

        public override string PasswordStrengthRegularExpression
        {
            get
            {
                return this._passwordStrengthRegularExpression;
            }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get
            {
                return this._requiresQuestionAndAnswer;
            }
        }

        public override bool RequiresUniqueEmail
        {
            get
            {
                return this._requiresUniqueEmail;
            }
        }

        #endregion

        #region Public Methods

        public override bool ChangePassword(string userName, string oldPwd, string newPwd)
        {
            if (!this.ValidateUser(userName, oldPwd))
            {
                return false;
            }

            PutAttributesRequest request = new PutAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(userName)
                .WithAttribute(new ReplaceableAttribute
                    {
                        Name = "Password",
                        Value = newPwd,
                        Replace = true
                    }
                );
            this._simpleDBClient.PutAttributes(request);
            return true;
        }

        public override bool ChangePasswordQuestionAndAnswer(string userName, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser CreateUser(string userName, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            MembershipUser user = this.GetUser(userName, false);
            if (user == null)
            {
                string existingUser = this.GetUserNameByEmail(email);
                if (String.IsNullOrEmpty(existingUser))
                {
                    List<ReplaceableAttribute> data = new List<ReplaceableAttribute>();
                    data.Add(new ReplaceableAttribute().WithName("Email").WithValue(email));
                    data.Add(new ReplaceableAttribute().WithName("Password").WithValue(password));
                    if (passwordQuestion != null)
                    {
                        data.Add(new ReplaceableAttribute().WithName("PasswordQuestion").WithValue(passwordQuestion));
                    }

                    if (passwordAnswer != null)
                    {
                        data.Add(new ReplaceableAttribute().WithName("PasswordAnswer").WithValue(passwordAnswer));
                    }

                    data.Add(new ReplaceableAttribute().WithName("IsApproved").WithValue(isApproved.ToString()));
                    PutAttributesRequest request = new PutAttributesRequest()
                        .WithDomainName(Settings.Default.AWSMembershipDomain)
                        .WithItemName(userName);
                    request.Attribute = data;
                    this._simpleDBClient.PutAttributes(request);
                    status = MembershipCreateStatus.Success;
                    user = this.GetUser(userName, false);
                }
                else
                {
                    status = MembershipCreateStatus.DuplicateEmail;
                }
            }
            else
            {
                status = MembershipCreateStatus.DuplicateUserName;
            }

            return user;
        }

        public override bool DeleteUser(string userName, bool deleteAllRelatedData)
        {
            DeleteAttributesRequest request = new DeleteAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(userName);
            this._simpleDBClient.DeleteAttributes(request);
            return true;
        }

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            var users = new MembershipUserCollection();
            totalRecords = 0;
            return users;
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            totalRecords = 0;
            return new MembershipUserCollection();
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            MembershipUserCollection users = new MembershipUserCollection();
            SelectRequest request = new SelectRequest()
                .WithSelectExpression(String.Concat("Select * from `", Settings.Default.AWSMembershipDomain, "`"));
            SelectResult result = this._simpleDBClient.Select(request).SelectResult;
            if (result != null)
            {
                totalRecords = result.Item.Count;
                string userName = String.Empty;
                string email = String.Empty;
                string passwordQuestion = String.Empty;
                bool isApproved = false;
                foreach (Item item in result.Item)
                {
                    List<Attribute> attributes = item.Attribute;
                    foreach (Attribute att in attributes)
                    {
                        switch (att.Name)
                        {
                            case "Email":
                                {
                                    email = att.Value;
                                    break;
                                }
                            case "PasswordQuestion":
                                {
                                    passwordQuestion = att.Value;
                                    break;
                                }
                            case "IsApproved":
                                {
                                    isApproved = bool.Parse(att.Value);
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                    userName = item.Name;

                    users.Add(new MembershipUser(
                        this.Name,
                        userName,
                        String.Empty,
                        email,
                        passwordQuestion,
                        String.Empty,
                        isApproved,
                        false,
                        DateTime.Today,
                        DateTime.Today,
                        DateTime.Today,
                        DateTime.Today,
                        DateTime.Today
                        ));
                }
            }
            else
            {
                totalRecords = 0;
            }

            return users;
        }

        public override int GetNumberOfUsersOnline()
        {
            throw new NotImplementedException();
        }

        public override string GetPassword(string userName, string answer)
        {
            GetAttributesRequest request = new GetAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(userName)
                .WithAttributeName(PasswordStrings);
            string password = String.Empty;
            GetAttributesResult result = this._simpleDBClient.GetAttributes(request).GetAttributesResult;
            if (result != null)
            {
                foreach (Attribute att in result.Attribute)
                {
                    switch (att.Name)
                    {
                        case "Password":
                        {
                            password = att.Value;
                            break;
                        }
                    }
                }
            }

            return password;
        }

        public override MembershipUser GetUser(string userName, bool userIsOnline)
        {
            this.VerifyKeys();

            MembershipUser user = null;
            if (!String.IsNullOrEmpty(userName))
            {
                DomainHelper.CheckForDomain(Settings.Default.AWSMembershipDomain, _simpleDBClient);
                GetAttributesRequest request = new GetAttributesRequest()
                    .WithDomainName(Settings.Default.AWSMembershipDomain)
                    .WithItemName(userName);
                GetAttributesResult result = this._simpleDBClient.GetAttributes(request).GetAttributesResult;
                if (result == null ||
                    result.Attribute.Count == 0)
                {
                    return null;
                }

                string email = String.Empty;
                string passwordQuestion = String.Empty;
                bool isApproved = false;
                List<Attribute> attributes = result.Attribute;
                foreach (Attribute att in attributes)
                {
                    switch (att.Name)
                    {
                        case "Email":
                        {
                            email = att.Value;
                            break;
                        }
                        case "PasswordQuestion":
                        {
                            passwordQuestion = att.Value;
                            break;
                        }
                        case "IsApproved":
                        {
                            isApproved = bool.Parse(att.Value);
                            break;
                        }
                        default:
                        break;
                    }
                }
                user = new MembershipUser(
                    this.Name, 
                    userName, 
                    String.Empty, 
                    email, 
                    passwordQuestion, 
                    String.Empty, 
                    isApproved, 
                    false, 
                    DateTime.Today, 
                    DateTime.Today, 
                    DateTime.Today, 
                    DateTime.Today, 
                    DateTime.Today
                    );
            }

            return user;
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            return this.GetUser(providerUserKey.ToString(), userIsOnline);
        }

        public override string GetUserNameByEmail(string email)
        {
            this.VerifyKeys();

            if (!String.IsNullOrEmpty(email))
            {
                DomainHelper.CheckForDomain(Settings.Default.AWSMembershipDomain, _simpleDBClient);
                SelectRequest request = new SelectRequest();
                request.SelectExpression = String.Concat(
                    "Select * from `",
                    Settings.Default.AWSMembershipDomain,
                    "` where Email='",
                    email,
                    "'"
                    );
                SelectResult result = this._simpleDBClient.Select(request).SelectResult;
                if (result == null ||
                    result.Item.Count == 0)
                {
                    return String.Empty;
                }
                else
                {
                    return result.Item[0].Name;
                }
            }

            throw new ArgumentNullException("email", "The email passed in is null");
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (String.IsNullOrEmpty(name))
            {
                name = "SimpleDbMembershipProvider";
            }

            base.Initialize(name, config);
            string tempFormat = config["passwordFormat"];
            if (tempFormat == null)
            {
                tempFormat = "Hashed";
            }

            switch (tempFormat)
            {
                case "Hashed":
                {
                    this._passwordFormat = MembershipPasswordFormat.Hashed;
                    break;
                }
                case "Encrypted":
                {
                    this._passwordFormat = MembershipPasswordFormat.Encrypted;
                    break;
                }
                case "Clear":
                {
                    this._passwordFormat = MembershipPasswordFormat.Clear;
                    break;
                }
            }

            this._simpleDBClient = new AmazonSimpleDBClient(Amazon.RegionEndpoint.USWest2);

            CreateDomainRequest cdRequest = new CreateDomainRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain);
            try
            {
                this._simpleDBClient.CreateDomain(cdRequest);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(String.Concat(e.Message, "\r\n", e.StackTrace));
                this.VerifyKeys();
            }
            Configuration cfg = WebConfigurationManager.OpenWebConfiguration(HostingEnvironment.ApplicationVirtualPath);
            this._machineKey = (MachineKeySection)cfg.GetSection("system.web/machineKey");
        }

        public override string ResetPassword(string userName, string answer)
        {
            string newPassword = Membership.GeneratePassword(6, 0);
            string passwordAnswer = String.Empty;
            GetAttributesRequest request = new GetAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(userName)
                .WithAttributeName(PasswordStrings);
            GetAttributesResult result = this._simpleDBClient.GetAttributes(request).GetAttributesResult;
            if (result != null)
            {
                foreach (Attribute att in result.Attribute)
                {
                    switch (att.Name)
                    {
                        case "PasswordAnswer":
                        {
                            passwordAnswer = att.Value;
                            break;
                        }
                    }
                }
            }
            else
            {
                throw new MembershipPasswordException("User not found");
            }

            if (this.RequiresQuestionAndAnswer && !this.CheckPassword(answer, passwordAnswer))
            {
                throw new MembershipPasswordException("Incorrect password answer.");
            }

            ReplaceableAttribute replace = new ReplaceableAttribute()
                .WithName(PasswordStrings[0])
                .WithValue(newPassword)
                .WithReplace(true);
            PutAttributesRequest prequest = new PutAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(userName)
                .WithAttribute(replace);
            this._simpleDBClient.PutAttributes(prequest);
            return newPassword;
        }

        public override bool UnlockUser(string userName)
        {
            return true;
        }

        public override void UpdateUser(MembershipUser user)
        {
            List<ReplaceableAttribute> attributes = new List<ReplaceableAttribute> { 
                new ReplaceableAttribute()
                    .WithName("Email")
                    .WithValue(user.Email)
                    .WithReplace(true) 
            };
            PutAttributesRequest request = new PutAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(user.UserName);
            request.Attribute = attributes;
            this._simpleDBClient.PutAttributes(request);
        }

        public override bool ValidateUser(string userName, string password)
        {
            this.VerifyKeys();

            if (String.IsNullOrEmpty(userName) ||
                String.IsNullOrEmpty(password))
            {
                return false;
            }

            bool retval = false;
            string dbpassword = String.Empty;
            GetAttributesRequest request = new GetAttributesRequest()
                .WithDomainName(Settings.Default.AWSMembershipDomain)
                .WithItemName(userName)
                .WithAttributeName(PasswordStrings);
            GetAttributesResult result = this._simpleDBClient.GetAttributes(request).GetAttributesResult;
            if (result != null)
            {
                foreach (Attribute att in result.Attribute)
                {
                    switch (att.Name)
                    {
                        case "Password":
                        {
                            dbpassword = att.Value;
                            break;
                        }
                    }
                }
                retval = dbpassword == password;
            }

            return retval;
        }

        public void VerifyKeys()
        {
            if ((String.IsNullOrEmpty(Settings.Default.AWSAccessKey.Trim()) ||
                String.IsNullOrEmpty(Settings.Default.AWSSecretAccessKey.Trim())) &&
                this.MembershipProviderConfigError != null)
            {
                this.MembershipProviderConfigError(this, new EventArgs());
            }
        }

        #endregion

        #region Private Methods

        private bool CheckPassword(string password, string dbpassword)
        {
            string pass1 = password;
            string pass2 = dbpassword;

            switch (this.PasswordFormat)
            {
                case MembershipPasswordFormat.Encrypted:
                pass2 = this.DecodePassword(dbpassword);
                break;
                case MembershipPasswordFormat.Hashed:
                pass1 = this.EncodePassword(password);
                break;
                default:
                break;
            }

            if (pass1 == pass2)
            {
                return true;
            }

            return false;
        }

        private string EncodePassword(string password)
        {
            string encodedPassword = password;
            switch (this.PasswordFormat)
            {
                case MembershipPasswordFormat.Clear:
                {
                    break;
                }
                case MembershipPasswordFormat.Encrypted:
                {
                    encodedPassword = Convert.ToBase64String(
                        this.EncryptPassword(Encoding.Unicode.GetBytes(password))
                        );
                    break;
                }
                case MembershipPasswordFormat.Hashed:
                {
                    HMACSHA1 hash = new HMACSHA1();
                    hash.Key = this.HexToByte(this._machineKey.ValidationKey);
                    encodedPassword = Convert.ToBase64String(
                        hash.ComputeHash(Encoding.Unicode.GetBytes(password))
                        );
                    break;
                }
            }

            return encodedPassword;
        }

        private byte[] HexToByte(string hexString)
        {
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
            {
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return returnBytes;
        }

        private string DecodePassword(string encodedPassword)
        {
            string password = encodedPassword;
            switch (this.PasswordFormat)
            {
                case MembershipPasswordFormat.Clear:
                {
                    break;
                }
                case MembershipPasswordFormat.Encrypted:
                {
                    password = Encoding.Unicode.GetString(
                        this.DecryptPassword(Convert.FromBase64String(password))
                        );
                    break;
                }
                case MembershipPasswordFormat.Hashed:
                {
                    throw new ProviderException("Cannot unencode a hashed password.");
                }
                default:
                {
                    throw new ProviderException("Unsupported password format.");
                }
            }

            return password;
        }

        #endregion
    }
}