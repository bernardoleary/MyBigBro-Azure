﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.Domain;
using Microsoft.Practices.Unity;

namespace Infostructure.MyBigBro.Web.Controllers
{
    public class LoginController : Controller
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
        public ActionResult Index(Credentials credentials)
        {
            try
            {
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
