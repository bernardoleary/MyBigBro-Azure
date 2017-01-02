using System.Web.Mvc;
using BootstrapMvcSample.Controllers;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.Web.Controllers
{
    public class LoginController : BootstrapBaseController
    {
        private IAccountService _accountService = null;
        private IFormsAuthenticationService _formsAuthenticationService = null;

        public LoginController() {}

        public LoginController(IAccountService accountService, IFormsAuthenticationService formsAuthenticationService)
        {
            _accountService = accountService;
            _formsAuthenticationService = formsAuthenticationService;
        }

        //
        // GET: /Login/

        public ActionResult Index()
        {
            return View();          
        }

        //
        // POST: /Login/

        [HttpPost]
        public ActionResult Index(FormCollection formCollection)
        {
            try
            {
                var credentials = new Credentials(formCollection.Get("username"), formCollection.Get("password"));
                if (_accountService.ValidateUser(credentials) != null)
                {
                    _formsAuthenticationService.SignIn(credentials.UserName, true);
                    return RedirectToAction("Index", "Home");
                }
                else
                    return RedirectToAction("Index", new { failed = true });
            }
            catch
            {
                throw;
            }
        }
    }
}
