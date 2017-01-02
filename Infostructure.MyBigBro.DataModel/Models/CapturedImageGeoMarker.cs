﻿using System.ComponentModel.DataAnnotations;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("CapturedImageGeoMarker", Schema = "dbo")]
    public class CapturedImageGeoMarker : ICapturedImageGeoMarker
    {
        [Key]
        public int Id
        {
            get;
            set;
        }

        public int CapturedImageId
        {
            get;
            set;
        }

        public int GeoMarkerId
        {
            get;
            set;
        }
    }
}
