﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;

namespace Infostructure.MyBigBro.Web3.Controllers
{
    public class WebCamsController : ApiController
    {
        private IMyBigBroRepository _myBigBroRepository = null;

        public WebCamsController() {}

        public WebCamsController(IMyBigBroRepository myBigBroRepository)
        {
            _myBigBroRepository = myBigBroRepository;
        }

        // GET api/webcam
        public IEnumerable<WebCam> Get()
        {
            if (ModelState.IsValid)
            {
                var result = _myBigBroRepository.Set<WebCam>();
                return result;
            }
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // GET api/webcam/5
        public WebCam Get(int id)
        {
            if (ModelState.IsValid)
            {
                var result = _myBigBroRepository.Set<WebCam>().FirstOrDefault(x => x.Id == id);
                return result;
            }
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        /*
        private void EmailException(Exception ex)
        {
            using (var mail = new System.Net.Mail.SmtpClient())
            {
                mail.Credentials = new System.Net.NetworkCredential("info@mybigbro.tv", "teevee");
                mail.Host = "mail.mybigbro.tv";
                mail.Send(new MailMessage("info@mybigbro.tv", "bernard.oleary@gmail.com", "MyBigBro",
                                          ex.Message + Environment.NewLine + ex.StackTrace));
            }
        }
         * */
    }
}
