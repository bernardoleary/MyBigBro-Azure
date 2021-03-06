﻿using System;
using System.ComponentModel.DataAnnotations;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("CapturedImage", Schema = "dbo")]
    public class CapturedImage : ICapturedImage
    {
        [Key]
        public int Id
        {
            get;
            set;
        }

        public string Url
        {
            get;
            set;
        }

        public int WebCamId
        {
            get;
            set;
        }

        public DateTime DateTimeCaptured
        {
            get;
            set;
        }
        
        public bool IsThumbnailed 
        { 
            get; 
            set; 
        }

        public string Key
        {
            get; 
            set;
        }
    }
}
