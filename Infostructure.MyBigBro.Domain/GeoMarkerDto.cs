using System;

namespace Infostructure.MyBigBro.Domain
{
    public class GeoMarkerDto : IGeoMarkerDto
    {
        public int Id { get; set; }
        public DateTime MarkerDateTime { get; set; }
        public double XCoord { get; set; }
        public double YCoord { get; set; }
        public int DeviceId { get; set; }
        public string DeviceName { get; set; }
        // =================================================================
        // Commenting because somehow this addition has broken the solution.
        // TODO: Figure out what the problem is.
        // =================================================================
        //public bool TagOnInstagram { get; set; }
    }
}
