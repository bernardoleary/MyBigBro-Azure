﻿using System.ComponentModel.DataAnnotations;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("WebCam", Schema = "dbo")]
    public class WebCam : IWebCam
    {
        [Key]
        public int Id
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public double XCoord
        {
            get;
            set;
        }

        public double YCoord
        {
            get;
            set;
        }

        public double RadiusOfVisibility
        {
            get;
            set;
        }

        public string Url
        {
            get;
            set;
        }
    }
}
