namespace Infostructure.MyBigBro.Domain
{
    public interface IGeoMarkerDto : IGeoMarker
    {
        string DeviceName { get; set; }
        // =================================================================
        // Commenting because somehow this addition has broken the solution.
        // TODO: Figure out what the problem is.
        // =================================================================
        //bool TagOnInstagram { get; set; }
    }
}