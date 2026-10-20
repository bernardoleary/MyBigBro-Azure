using System.IO;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    // This class enables mocking the WebResponse class.
    public class WebCamDataRequest : IWebCamDataRequest
    {
        public WebCamWebResponse GetWebCamResponse(string url)
        {
            return new WebCamWebResponse(url);
        }

        public Stream GetWebCamResponseStream(WebCamWebResponse response)
        {
            return response.GetResponseStream();
        }
    }
}
