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

    using Amazon.S3;
    using Amazon.SimpleDB;
    using Amazon.SimpleDB.Model;

    using Model;
    using Properties;
    using Util;

    using Attribute = Amazon.SimpleDB.Model.Attribute;

    public partial class PetProfile : Page
    {
        #region Constants and Fields

        private string _domainName;

        private string _itemName;

        private string _petIdString;

        private AmazonSimpleDBClient _simpleDBClient;

        #endregion

        #region Methods

        public PetProfile()
        {
            if (_simpleDBClient == null)
            {
                _simpleDBClient = new AmazonSimpleDBClient(Amazon.RegionEndpoint.USWest2);
            }
            System.Threading.Thread.MemoryBarrier();
        }

        ~PetProfile()
        {
            _simpleDBClient.Dispose();
            _simpleDBClient = null;
        }

        protected void LikeButton_Click(object sender, EventArgs e)
        {
            if (!Context.User.Identity.IsAuthenticated)
            {
                FormsAuthentication.RedirectToLoginPage();
                return;
            }

            DomainHelper.CheckForDomain(Settings.Default.PetBoardFriendsDomain, _simpleDBClient);
            string ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!String.IsNullOrEmpty(ip))
            {
                string[] ipRange = ip.Split(',');
                int le = ipRange.Length - 1;
                string trueIP = ipRange[le];
            }
            else
            {
                ip = Request.ServerVariables["REMOTE_ADDR"];
            }

            PutAttributesRequest request = new PutAttributesRequest()
                .WithDomainName(Settings.Default.PetBoardFriendsDomain)
                .WithItemName(ip)
                .WithAttribute(
                    new ReplaceableAttribute
                    {
                        Name = "Member",
                        Value = this.Context.User.Identity.Name,
                        Replace = true
                    },
                    new ReplaceableAttribute
                    {
                        Name = "Pet",
                        Value = this._petIdString,
                        Replace = true
                    }
                );

            _simpleDBClient.PutAttributes(request);
            this.Response.Redirect(this.Request.RawUrl);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            this._petIdString = this.Request.QueryString["petid"];

            if (!String.IsNullOrEmpty(this._petIdString))
            {
                this._itemName = this._petIdString ?? Guid.NewGuid().ToString();
                this._domainName = String.Format(Settings.Default.SimpleDbDomainNameFormat, this.Context.User.Identity.Name);

                UpdatePageWithPetInformation(RetrievePetFromPrivateDomain());

                DomainHelper.CheckForDomain(Properties.Settings.Default.PetBoardFriendsDomain, _simpleDBClient);
                SelectRequest selectRequest = new SelectRequest()
                    .WithSelectExpression(String.Format("select * from `{0}` where `Pet` = '{1}'", Properties.Settings.Default.PetBoardFriendsDomain, this._petIdString));
                SelectResponse selectResponse = _simpleDBClient.Select(selectRequest);
                LikesCount.Text = selectResponse.SelectResult.Item.Count().ToString();
            }
        }

        private void UpdatePageWithPetInformation(Pet pet)
        {
            if (pet != null)
            {
                this.PetNameHeader.Text = pet.Name;
                this.PetPhoto.Src = pet.PhotoThumbUrl;
                this.TypeLiteral.Text = pet.Type;
                this.BreedLiteral.Text = pet.Breed;
                this.SexLiteral.Text = pet.Sex;
                this.LikesLiteral.Text = pet.Likes;
                this.DislikesLiteral.Text = pet.Dislikes;
            }
        }

        private Pet RetrievePetFromPrivateDomain()
        {
            string domainToTry = this._domainName;
            GetAttributesRequest getAttributeRequest = null;
            GetAttributesResult result;
            List<Attribute> attrs = null;
            if (DomainHelper.DoesDomainExist(this._domainName, _simpleDBClient))
            {
                //
                // Try to get the requested pet from the user's private domain
                //
                getAttributeRequest = new GetAttributesRequest()
                    .WithDomainName(this._domainName)
                    .WithItemName(this._itemName);
                result = _simpleDBClient.GetAttributes(getAttributeRequest).GetAttributesResult;

                if (result != null)
                {
                    if (result.Attribute.Count > 0)
                    {
                        attrs = result.Attribute;
                    }
                    else
                    {
                        // If we can't find it try the public domain
                        getAttributeRequest.WithDomainName(Properties.Settings.Default.PetBoardPublicDomainName);
                        result = _simpleDBClient.GetAttributes(getAttributeRequest).GetAttributesResult;
                        if (result != null)
                        {
                            attrs = result.Attribute;
                        }
                    }
                }
            }
            else if (DomainHelper.DoesDomainExist(Settings.Default.PetBoardPublicDomainName, _simpleDBClient))
            {
                getAttributeRequest = new GetAttributesRequest()
                    .WithDomainName(Properties.Settings.Default.PetBoardPublicDomainName)
                    .WithItemName(this._itemName);
                result = _simpleDBClient.GetAttributes(getAttributeRequest).GetAttributesResult;
                if (result != null)
                {
                    attrs = result.Attribute;
                }
            }

            // If results is null, attrs is going to be null as well
            if (attrs != null)
            {
                return new Pet
                {
                    PhotoThumbUrl = attrs.First(a => a.Name == "PhotoThumbUrl").Value,
                    Name = attrs.First(a => a.Name == "Name").Value,
                    Birthdate = attrs.First(a => a.Name == "Birthdate").Value,
                    Sex = attrs.First(a => a.Name == "Sex").Value,
                    Type = attrs.First(a => a.Name == "Type").Value,
                    Breed = attrs.First(a => a.Name == "Breed").Value,
                    Likes = attrs.First(a => a.Name == "Likes").Value,
                    Dislikes = attrs.First(a => a.Name == "Dislikes").Value
                };
            }
            else
            {
                return null;
            }
        }

        #endregion
    }
}