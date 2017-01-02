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
    using System.IO;
    using System.Web;

    using Model;
    using Properties;

    using Amazon.SimpleDB;
    using Amazon.S3;

    public static class SampleHelper
    {
        #region Public Members 

        public static List<Pet> pets = new List<Pet>
        {
            new Pet
                {
                    Name = "Dasha",
                    Type = "Bird",
                    Breed = "Cockatoo",
                    Sex = "",
                    Birthdate = DateTime.Now.AddYears(0 * -1).ToString("s"),
                    Likes = "C#, Java, Clouds",
                    Dislikes = "Sundays"
                },
            new Pet
                {
                    Name = "Sydney",
                    Type = "Dog",
                    Breed = "Golden Retriever",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(0 * -1).ToString("s"),
                    Likes = "Tennis balls, food",
                    Dislikes = "Yappy dogs, mailmen"
                },
            new Pet
                {
                    Name = "Isabella Rosalina",
                    Type = "Dog",
                    Breed = "King Charles Cavalier Spaniel, Japanese Chin",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(2 * -1).ToString("s"),
                    Likes = "To lay next to you in a chair, even if that means you have no room to breathe.",
                    Dislikes = "Afraid of anything that moves outside after dark…especially leaves."
                },
            new Pet
                {
                    Name = "Calli",
                    Type = "Dog",
                    Breed = "Border Collie Mix",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(12 * -1).ToString("s"),
                    Likes = "Chin scratches, tennis balls, Frisbees, any food that you have",
                    Dislikes = "Things that might fall over on her, things that look like they might fall over on her, things that look like things that might fall over on other things that might in turn fall over on her, bugs, the word \"bug\", the spelling of the word \"bug.\""
                },
            new Pet
                {
                    Name = "Captian and Alanna",
                    Type = "Standard Poodle",
                    Breed = "",
                    Sex = "Female, Male",
                    Birthdate = DateTime.Now.AddYears(7 * -1).ToString("s"),
                    Likes = "To smile",
                    Dislikes = "Fruit"
                },
            new Pet
                {
                    Name = "Sky",
                    Type = "Dog",
                    Breed = "Mutt",
                    Sex = "Male",
                    Birthdate = DateTime.Now.AddYears(1 * -1).ToString("s"),
                    Likes = "Biting",
                    Dislikes = "Moving off the couch"
                },
            new Pet
                {
                    Name = "Baron",
                    Type = "Dog",
                    Breed = "Shetland Sheepdog",
                    Sex = "Male",
                    Birthdate = DateTime.Now.AddYears(15 * -1).ToString("s"),
                    Likes = "Playing fetch, dog next door",
                    Dislikes = "UPS"
                },
            new Pet
                {
                    Name = "Tully",
                    Type = "Dog",
                    Breed = "Chocolate Lab, Doberman",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(2 * -1).ToString("s"),
                    Likes = "Food, tennis balls, squirrels",
                    Dislikes = "Other dogs on leash, vegetables"
                },
            new Pet
                {
                    Name = "Frodo",
                    Type = "Dog",
                    Breed = "English Bulldog",
                    Sex = "Male",
                    Birthdate = DateTime.Now.AddYears(5 * -1).ToString("s"),
                    Likes = "Chewing",
                    Dislikes = "Bathing"
                },
            new Pet
                {
                    Name = "Sophi",
                    Type = "Dog",
                    Breed = "Jack Russell Terrier",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(9 * -1).ToString("s"),
                    Likes = "Bacon",
                    Dislikes = "A lack of bacon"
                },
            new Pet
                {
                    Name = "Rossi",
                    Type = "Dog",
                    Breed = "Chocolate Labradoodle",
                    Sex = "Male",
                    Birthdate = DateTime.Now.AddYears(1 * -1).ToString("s"),
                    Likes = "Playing with other dogs, bread products",
                    Dislikes = "Getting caught eating bread products from the counter"
                },
            new Pet
                {
                    Name = "Schroder",
                    Type = "Cat",
                    Breed = "",
                    Sex = "Male",
                    Birthdate = DateTime.Now.AddYears(12 * -1).ToString("s"),
                    Likes = "Catnip, photo shoots, ice cream",
                    Dislikes = "Vacuums"
                },
            new Pet
                {
                    Name = "Parker",
                    Type = "Cat",
                    Breed = "Manx",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(8 * -1).ToString("s"),
                    Likes = "Eating grass, catnip",
                    Dislikes = "Strangers, vacuum cleaners",
                },
            new Pet
                {
                    Name = "Burbut von Dinner Biscuit",
                    Type = "Cat",
                    Breed = "Short-haired domestic",
                    Sex = "Female",
                    Birthdate = DateTime.Now.AddYears(8 * -1).ToString("s"),
                    Likes = "Exercise, being curious",
                    Dislikes = "Exercise, being curious",
                },
            new Pet
                {
                    Name = "Horatio Zee",
                    Type = "Dog",
                    Breed = "Yorkshire Terrier",
                    Sex = "Make",
                    Birthdate = DateTime.Now.AddYears(1 * -1).ToString("s"),
                    Likes = "Being King",
                    Dislikes = "Not being a King",
                },
        };

        public static void AddSampleData()
        {
            string domainName = String.Format(Settings.Default.SimpleDbDomainNameFormat, HttpContext.Current.User.Identity.Name);

            using (AmazonSimpleDBClient sdbClient = new AmazonSimpleDBClient(Amazon.RegionEndpoint.USWest2))
            {
                using (AmazonS3Client s3Client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2))
                {

                    foreach (Pet pet in pets)
                    {
                        string itemName = Guid.NewGuid().ToString();
                        pet.Put(domainName, itemName, true, sdbClient);
                        string path = HttpContext.Current.Server.MapPath("~/SampleData");
                        FileInfo file = new FileInfo(Path.Combine(path, String.Concat(pet.Name.ToLowerInvariant().Replace(' ', '-'), ".jpg")));
                        string bucketName = String.Format(Settings.Default.BucketNameFormat, HttpContext.Current.User.Identity.Name, itemName);
                        Pet.PutPhoto(domainName, itemName, bucketName, file.Name, file.OpenRead(), true, sdbClient, s3Client);
                    }
                }
            }
        }

        #endregion
    }
}