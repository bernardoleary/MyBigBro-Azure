using System.IO;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    public interface IWebCamDataRequest
    {
        WebCamWebResponse GetWebCamResponse(string url);
        Stream GetWebCamResponseStream(WebCamWebResponse response);
    }
}