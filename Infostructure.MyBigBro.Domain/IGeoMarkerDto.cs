namespace Infostructure.MyBigBro.Domain
{
    public interface IGeoMarkerDto : IGeoMarker
    {
        string DeviceName { get; set; }
        bool TagOnInstagram { get; set; }
    }
}