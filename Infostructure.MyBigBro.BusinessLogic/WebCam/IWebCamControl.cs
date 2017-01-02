using System.Collections.Generic;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.Domain;
using Infostructure.MyBigBro.ImageStorageServiceAgent;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    public interface IWebCamControl
    {
        IMyBigBroRepository MyBigBroRepository { get; set; }
        IWebCam WebCam { get; set; }
        IStorageServiceAgent StorageServiceAgent { get; set; }
        IWebCamImage WebCamImage { get; set; }
        ICapturedImage CapturedImage { get; set; }
        void CaptureCurrentImage(IWebCamDataRequest webCamDataRequest);
        int StoreCapturedImage();
        IWebCamExtendedInfo GetNearestWebCam(IGeoMarker geoMarker);
        IEnumerable<IWebCamExtendedInfo> GetNearestWebCams(int top, IGeoMarker geoMarker);
    }
}