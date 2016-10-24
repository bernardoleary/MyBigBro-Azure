using System;
using System.ComponentModel.DataAnnotations;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("GeoMarker", Schema = "dbo")]
    public class GeoMarker : IGeoMarker
    {
        [Key]
        public int Id
        {
            get;
            set;
        }

        public DateTime MarkerDateTime
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

        public int DeviceId
        {
            get;
            set;
        }

        public bool TagOnInstagram
        {
            get;
            set;
        }

        //[ForeignKey("User.Id")]
        //public int UserId
        //{
        //    get;
        //    set;
        //}
    }
}
