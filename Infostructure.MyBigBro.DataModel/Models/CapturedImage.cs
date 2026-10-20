using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("captured_image", Schema = "public")]
    public class CapturedImage : ICapturedImage
    {
        [Key]
        [Column("id")]
        public int Id
        {
            get;
            set;
        }

        [Column("url")]
        public string Url
        {
            get;
            set;
        }

        [Column("web_cam_id")]
        public int WebCamId
        {
            get;
            set;
        }

        [Column("date_time_captured")]
        public DateTime DateTimeCaptured
        {
            get;
            set;
        }
        
        [Column("is_thumbnailed")]
        public bool IsThumbnailed 
        { 
            get; 
            set; 
        }

        [Column("key")]
        public string Key
        {
            get; 
            set;
        }
    }
}
