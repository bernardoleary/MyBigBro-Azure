using System.Net;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    public class WebCamWebResponse : WebResponse
    {
        private WebRequest _webRequest = null;

        public WebCamWebResponse(WebRequest webRequest)
        {
            _webRequest = webRequest;
        }

        public override System.IO.Stream GetResponseStream()
        {
            var response = _webRequest.GetResponse();
            return response.GetResponseStream();
        }

        public WebCamWebResponse()
        {
            
        }
    }
}
