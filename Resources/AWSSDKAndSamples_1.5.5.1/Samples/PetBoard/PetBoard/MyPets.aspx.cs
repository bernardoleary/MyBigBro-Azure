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
    using System.Web.UI;
    using System.Web.UI.WebControls;

    using Amazon.SimpleDB;
    using Amazon.SimpleDB.Model;

    using Model;
    using Properties;
    using Util;

    public partial class MyPets : Page
    {
        #region Constants and Fields

        private string _domainName;
        private static readonly string detailPageUrlFormat = "{0}.aspx?petid={1}";

        #endregion

        #region Methods

        protected void Page_Load(object sender, EventArgs e)
        {
            this._domainName = String.Format(Settings.Default.SimpleDbDomainNameFormat, this.Context.User.Identity.Name);

            if (!this.Page.IsPostBack)
            {
                using (AmazonSimpleDBClient sdbClient = new AmazonSimpleDBClient(Amazon.RegionEndpoint.USWest2))
                {
                    DomainHelper.CheckForDomain(this._domainName, sdbClient);
                    SelectRequest selectRequest = new SelectRequest().WithSelectExpression(String.Format("select * from `{0}`", this._domainName));
                    SelectResponse selectResponse = sdbClient.Select(selectRequest);
                    List<Item> items = selectResponse.SelectResult.Item;
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

            string detailPage = Context.User.Identity.IsAuthenticated ? "PetDetails" : "PetProfile";

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

        #endregion
    }
}