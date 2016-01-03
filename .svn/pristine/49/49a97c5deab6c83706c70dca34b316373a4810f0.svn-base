using System.Collections.Generic;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.BusinessLogic.GeoSpatial
{
    public interface ILocation
    {
        int AppendGeoMarker(IGeoMarker geoMarker);
        IMyBigBroRepository MyBigBroRepository { get; set; }
        IList<IWebCam> GetWebCamsWithinWebCamRadiusOfVisibility(Point point, bool isDegrees = true);
        bool IsPointWithinWebCamRadiusOfVisibility(Point point, bool isDegrees = true);
        IList<IWebCam> GetWebCams();
        int MapCapturedImageToGeoMarker(int capturedImageId, int geoMarkerId);
    }
}