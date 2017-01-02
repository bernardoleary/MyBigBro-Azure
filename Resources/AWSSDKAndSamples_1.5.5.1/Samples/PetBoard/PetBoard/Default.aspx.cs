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
    using System.Web;
    using System.Web.Security;
    using System.Web.UI;
    using System.Web.UI.WebControls;

    using Amazon.SimpleDB;
    using Amazon.SimpleDB.Model;

    using Model;
    using Properties;
    using Util;

    public partial class Default : Page
    {
        #region Private Members

        private AmazonSimpleDBClient _simpleDBClient;
        private static readonly string detailPageUrlFormat = "{0}.aspx?petid={1}";

        #endregion

        #region Methods

        public Default()
        {
            if (_simpleDBClient == null)
            {
                _simpleDBClient = new AmazonSimpleDBClient(Amazon.RegionEndpoint.USWest2);
            }
            System.Threading.Thread.MemoryBarrier();
        }

        ~Default()
        {
            _simpleDBClient.Dispose();
            _simpleDBClient = null;
        }

        protected void NoSampleButton_Click(object sender, EventArgs e)
        {
            this.Response.SetCookie(new HttpCookie("UseSampleData", false.ToString()));

            if (PostSignInMessage.Visible)
            {
                this.Response.Redirect("~/PetDetails.aspx");
            }
            else
            {
                this.Response.Redirect(this.Request.RawUrl);
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!this.Page.IsPostBack)
            {
                List<Item> items = null;
                DomainHelper.CheckForDomain(Settings.Default.PetBoardPublicDomainName, _simpleDBClient);
                SelectRequest selectRequest = new SelectRequest()
                    .WithSelectExpression(String.Format("select * from `{0}`", Settings.Default.PetBoardPublicDomainName));
                SelectResponse selectResponse = _simpleDBClient.Select(selectRequest);
                items = selectResponse.SelectResult.Item;

                bool noSampledData = (this.Request.Cookies["UseSampleData"] != null &&
                    "false".Equals(this.Request.Cookies["UseSampleData"].Value));
                if (items.Count == 0 &&
                    noSampledData == false)
                {
                    this.SampleDataPromptPanel.Visible = true;

                    if (User.Identity.IsAuthenticated)
                    {
                        PostSignInMessage.Visible = true;
                    }
                    else
                    {
                        PreSignInMessage.Visible = true;
                    }
                }
                else
                {
                    List<Pet> pets = items.Select(l => new Pet
                        {
                            Id = l.Name,
                            PhotoThumbUrl = l.Attribute.First(a => a.Name == "PhotoThumbUrl").Value,
                            Name = l.Attribute.First(a => a.Name == "Name").Value,
                            Birthdate = l.Attribute.First(a => a.Name == "Birthdate").Value,
                            Sex = l.Attribute.First(a => a.Name == "Sex").Value,
                            Type = l.Attribute.First(a => a.Name == "Type").Value,
                            Breed = l.Attribute.First(a => a.Name == "Breed").Value
                        }).ToList();
                    this.PetRepeater.DataSource = pets;
                    this.PetRepeater.DataBind();
                }
            }
        }

        protected void PetRepeater_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            Control imagePanel = e.Item.FindControl("ImagePanel");
            Pet pet = e.Item.DataItem as Pet;
            if (imagePanel != null &&
                pet != null &&
                String.IsNullOrEmpty(pet.PhotoThumbUrl))
            {
                imagePanel.Visible = false;
            }

            string detailPage = "PetProfile";

            HyperLink petImageHyperLink = e.Item.FindControl("PetImageHyperLink") as HyperLink;
            if (petImageHyperLink != null)
            {
                petImageHyperLink.NavigateUrl = String.Format(detailPageUrlFormat, detailPage, pet.Id);
            }

            HyperLink petTextHyperLink = e.Item.FindControl("PetTextHyperLink") as HyperLink;
            if (petTextHyperLink != null)
            {
                petTextHyperLink.NavigateUrl = String.Format(detailPageUrlFormat, detailPage, pet.Id);
            }
        }

        protected void YesSampleButton_Click(object sender, EventArgs e)
        {
            if (!this.Context.User.Identity.IsAuthenticated)
            {
                FormsAuthentication.RedirectToLoginPage();
            }
            else
            {
                this.Response.SetCookie(new HttpCookie("UseSampleData", true.ToString()));
                SampleHelper.AddSampleData();
                this.Response.Redirect(this.Request.RawUrl);
            }
        }

        #endregion
    }
}