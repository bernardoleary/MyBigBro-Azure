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

    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SimpleDB;
    using Amazon.SimpleDB.Model;

    using Model;
    using Properties;
    using Util;

    using Attribute = Amazon.SimpleDB.Model.Attribute;

    public partial class PetDetails : Page
    {
        #region Constants and Fields

        private string _domainName;

        private string _itemName;

        private string _petIdString;

        private string _photoThumbUrl;

        private string _userBucketName;

        private AmazonSimpleDBClient _simpleDBClient;

        #endregion

        #region Methods

        public PetDetails()
        {
            if (_simpleDBClient == null)
            {
                _simpleDBClient = new AmazonSimpleDBClient(Amazon.RegionEndpoint.USWest2);
            }
            System.Threading.Thread.MemoryBarrier();
        }

        ~PetDetails()
        {
            _simpleDBClient.Dispose();
            _simpleDBClient = null;
        }

        protected void CancelEditsButton_Click(object sender, EventArgs e)
        {
            this.Response.Redirect(this.Request.RawUrl);
        }

        protected void DayDropDownList_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        protected void MonthDropDownList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SelectDay();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (this.Session[Settings.Default.FlashSessionKey] != null)
            {
                this.FlashLiteralWrapper.Visible = true;
                this.FlashLiteral.Text = this.Session[Settings.Default.FlashSessionKey].ToString();
                this.Session[Settings.Default.FlashSessionKey] = null;
            }
            else
            {
                this.FlashLiteralWrapper.Visible = false;
            }

            this._petIdString = this.Request.QueryString["petid"];

            if (String.IsNullOrEmpty(this._petIdString))
            {
                this.StatsLiteral.Text = "Add a New Pet";
                this.SaveStatsButton.Text = "Save New Pet";
            }
            else
            {
                this.PhotoPanel.Visible = true;
            }

            this._userBucketName = String.Format(Settings.Default.BucketNameFormat, this.Context.User.Identity.Name, this._petIdString);
            this._itemName = this._petIdString ?? Guid.NewGuid().ToString();
            this._domainName = String.Format(Settings.Default.SimpleDbDomainNameFormat, this.Context.User.Identity.Name);

            if (!this.Page.IsPostBack)
            {
                List<int> years = new List<int>(100);
                for (int i = 0; i < 100; i++)
                {
                    years.Add(DateTime.Now.AddYears(i * -1).Year);
                }
                this.YearDropDownList.DataSource = years.OrderByDescending(y => y);
                this.YearDropDownList.DataBind();
                this.SelectMonth();
                this.SelectDay();

                Pet pet = default(Pet);
                List<string> files = new List<string>();
                if (!String.IsNullOrEmpty(this._petIdString))
                {
                    //
                    // Try to get the requested pet from the user's private domain
                    //
                    DomainHelper.CheckForDomain(this._domainName, _simpleDBClient);

                    GetAttributesRequest getAttributeRequest = new GetAttributesRequest()
                        .WithDomainName(this._domainName)
                        .WithItemName(this._itemName);
                    GetAttributesResponse getAttributeResponse = _simpleDBClient.GetAttributes(getAttributeRequest);
                    List<Attribute> attrs = null;
                    bool showPublic = false;

                    if (getAttributeResponse.IsSetGetAttributesResult())
                    {
                        attrs = getAttributeResponse.GetAttributesResult.Attribute;

                        showPublic = false;

                        //
                        // If we can't find it try the public domain
                        //
                        if (attrs.Count == 0)
                        {
                            showPublic = true;
                        }
                    }

                    if (showPublic)
                    {
                        Response.Redirect(String.Concat("PetProfile.aspx?petid", _petIdString));
                        return;
                    }

                    pet = new Pet
                    {
                        Name = attrs.First(a => a.Name == "Name").Value,
                        Birthdate = attrs.First(a => a.Name == "Birthdate").Value,
                        Sex = attrs.First(a => a.Name == "Sex").Value,
                        Type = attrs.First(a => a.Name == "Type").Value,
                        Breed = attrs.First(a => a.Name == "Breed").Value,
                        Likes = attrs.First(a => a.Name == "Likes").Value,
                        Dislikes = attrs.First(a => a.Name == "Dislikes").Value
                    };

                    this.Public.Checked = bool.Parse(attrs.First(a => a.Name == "Public").Value);

                    using (AmazonS3Client s3Client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2))
                    {
                        BucketHelper.CheckForBucket(this._petIdString, s3Client);
                        ListObjectsRequest listObjectsRequest = new ListObjectsRequest()
                            .WithBucketName(this._userBucketName);
                        using (ListObjectsResponse listObjectsResponse = s3Client.ListObjects(listObjectsRequest))
                        {
                            files = listObjectsResponse.S3Objects.Select(o => String.Format(Settings.Default.S3BucketUrlFormat, this._userBucketName, o.Key)).ToList();
                            string firstPhoto = files.FirstOrDefault();
                            this.PhotoThumbUrl.Value = firstPhoto ?? String.Empty;
                        }
                    }
                }

                if (pet != default(Pet))
                {
                    this.PetNameHeader.Text = pet.Name;
                    this.NameTextBox.Text = pet.Name;
                    this.AnimalDropDownList.SelectedValue = pet.Type;
                    this.BreedTextBox.Text = pet.Breed;
                    this.SexDropDownList.SelectedValue = pet.Sex;
                    if (pet.Birthdate != null)
                    {
                        DateTime birthdate = DateTime.Parse(pet.Birthdate);
                        this.YearDropDownList.SelectedValue = birthdate.Year.ToString();
                        this.MonthDropDownList.SelectedValue = birthdate.Month.ToString();
                        this.DayDropDownList.SelectedValue = birthdate.Day.ToString();
                    }
                    this.LikesTextBox.Text = pet.Likes;
                    this.DislikesTextBox.Text = pet.Dislikes;
                    this.PhotoRepeater.DataSource = files;
                    this.PhotoRepeater.DataBind();
                }
            }
        }

        protected void SaveStatsButton_Click(object sender, EventArgs e)
        {
            this._photoThumbUrl = this.PhotoThumbUrl.Value;
            string birthdate = new DateTime(
                int.Parse(this.YearDropDownList.SelectedValue),
                int.Parse(this.MonthDropDownList.SelectedValue),
                int.Parse(this.DayDropDownList.SelectedValue)
                ).ToString("s");
            Pet p = new Pet 
            { 
                Birthdate = birthdate,
                Breed = this.BreedTextBox.Text,
                Dislikes = this.DislikesTextBox.Text,
                Likes = this.LikesTextBox.Text,
                Name = this.NameTextBox.Text,
                PhotoThumbUrl = this._photoThumbUrl,
                Sex = this.SexDropDownList.SelectedValue,
                Type = this.AnimalDropDownList.SelectedValue
            };
            p.Put(this._domainName, this._itemName, this.Public.Checked, _simpleDBClient);

            this.Session["Flash"] = "Stats updated successfully";
            if (!String.IsNullOrEmpty(this._petIdString))
            {
                this.Response.Redirect(this.Request.RawUrl);
            }
            else
            {
                this.Response.Redirect(String.Concat(this.Request.RawUrl, "?petid=", this._itemName));
            }
        }

        protected void UploadButton_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(this.PhotoUpload.FileName))
            {
                return;
            }
            string bucketName = String.Format(Settings.Default.BucketNameFormat, this.Context.User.Identity.Name, this._itemName);
            using (AmazonS3Client s3Client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2))
            {
                Pet.PutPhoto(this._domainName, this._itemName, bucketName, this.PhotoUpload.FileName, this.PhotoUpload.FileContent, this.Public.Checked, this._simpleDBClient, s3Client);
            }
            this.Session[Settings.Default.FlashSessionKey] = "Photo uploaded successfully";
            this.Response.Redirect(this.Request.RawUrl);
        }

        protected void YearDropDownList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SelectMonth();
        }

        private void SelectDay()
        {
            DateTime now = new DateTime(int.Parse(this.YearDropDownList.SelectedValue), int.Parse(this.MonthDropDownList.SelectedValue), 1);
            List<int> days = new List<int>(31);
            for (int i = 1; i <= DateTime.DaysInMonth(now.Year, now.Month); i++)
            {
                days.Add(i);
            }
            this.DayDropDownList.DataSource = days.OrderBy(d => d);
            this.DayDropDownList.DataBind();
        }

        private void SelectMonth()
        {
            DateTime now = new DateTime(int.Parse(this.YearDropDownList.SelectedValue), 1, 1);
            DateTime startMonth = new DateTime(now.Year, 1, 1);
            List<DateTime> months = new List<DateTime>(12);
            for (int i = 0; i < 12; i++)
            {
                months.Add(startMonth.AddMonths(i));
            }
            this.MonthDropDownList.DataSource = months.Select(m => new { MonthText = m.ToString("MMMM"), m.Month });
            this.MonthDropDownList.DataBind();
            this.SelectDay();
        }

        #endregion

        protected void DeletePetButton_Click(object sender, EventArgs e)
        {
            DeleteAttributesRequest deleteRequest = new DeleteAttributesRequest()
                .WithDomainName(Properties.Settings.Default.PetBoardPublicDomainName)
                .WithItemName(this._itemName);
            _simpleDBClient.DeleteAttributes(deleteRequest);

            if (DomainHelper.DoesDomainExist(this._domainName, _simpleDBClient))
            {
                deleteRequest.DomainName = this._domainName;
                _simpleDBClient.DeleteAttributes(deleteRequest);
            }

            Response.Redirect("~/Default.aspx");
        }
    }
}