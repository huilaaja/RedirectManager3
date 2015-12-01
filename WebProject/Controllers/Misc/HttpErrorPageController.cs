﻿using System.Web.Mvc;
using EPiServer;
using EPiServer.Core;
using WebProject.Utils;
using WebProject.Redirects;

namespace WebProject.Controllers.Misc
{
    public class HttpErrorPageController : Controller
    {
        private readonly RedirectService _redirectService;

        public HttpErrorPageController(RedirectService redirectService)
        {
            _redirectService = redirectService;
        }

        [Route("error/http404")]
        public ActionResult Http404()
        {
            Uri originalUrl = IisErrorUrlParser.GetOriginalUrl(System.Web.HttpContext.Current.Request.Url, 404);
            string redirectTo = _redirectService.GetPrimaryRedirectUrlOrDefault(originalUrl.PathAndQuery);
            if (redirectTo != null)
            {
                return RedirectPermanent(redirectTo);
            }
            Response.Clear();
            Response.StatusCode = (int)HttpStatusCode.NotFound;
						return Content("404 Not found");
        }
    }
}