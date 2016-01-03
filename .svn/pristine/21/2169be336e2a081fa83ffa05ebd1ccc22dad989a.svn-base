using System.Collections.Generic;
using System.Net;
using System.Web.Http;
using System.Web.Security;

namespace Infostructure.MyBigBro.Web.ApiExceptions
{
    public static class WebApiExceptionConfig
    {
        public static void RegisterExceptions(HttpConfiguration config)
        {

            /* /// this is the easiest way to shove the exception messages into the httperror message property for ALL unhandled exceptions
            
            config.Filters.Add(new GlobalApiExceptionFilterAttribute(catchUnfilteredExceptions: true));
            
            */

            config.Filters.Add(new GlobalApiExceptionFilterAttribute(new List<GlobalApiExceptionDefinition>
                                                                         {

                                                                             /* /// Example 1 -- setting the error code and reference properties
                new GlobalApiExceptionDefinition(typeof(NotImplementedException)) { ErrorCode = "123456.cows", ErrorReference = "http://www.google.com?q=cows" },
                */

                                                                             /* /// Example 2 -- using the friendly message string overload
                new GlobalApiExceptionDefinition(typeof(NotImplementedException), "This method is really wonky", HttpStatusCode.NotAcceptable) { ErrorCode = "123456.cows", ErrorReference = "http://www.google.com?q=cows" },
                */

                                                                             /* /// Example 3 -- using the friendly message predicate overload
                new GlobalApiExceptionDefinition(typeof(MembershipCreateUserException), (ex) => MembershipHelper.MembershipCreateStatusToString((ex as MembershipCreateUserException).StatusCode), HttpStatusCode.Conflict)
                */

                                                                             new GlobalApiExceptionDefinition(
                                                                                 typeof (MembershipCreateUserException))
                                                                                 {
                                                                                     Handle =
                                                                                         (ex) =>
                                                                                         // we want to make sure the server error status codes are respected - we want to send back a 500
                                                                                             {
                                                                                                 if (
                                                                                                     ex is
                                                                                                     MembershipCreateUserException)
                                                                                                 {
                                                                                                     var mex =
                                                                                                         ex as
                                                                                                         MembershipCreateUserException;
                                                                                                     switch (
                                                                                                         mex.StatusCode)
                                                                                                     {
                                                                                                         case
                                                                                                             MembershipCreateStatus
                                                                                                                 .
                                                                                                                 DuplicateProviderUserKey
                                                                                                             :
                                                                                                         case
                                                                                                             MembershipCreateStatus
                                                                                                                 .
                                                                                                                 InvalidProviderUserKey
                                                                                                             :
                                                                                                         case
                                                                                                             MembershipCreateStatus
                                                                                                                 .
                                                                                                                 ProviderError
                                                                                                             :
                                                                                                             return true;
                                                                                                         default:
                                                                                                             break;
                                                                                                     }
                                                                                                 }
                                                                                                 return false;
                                                                                             }
                                                                                 },
                                                                             new GlobalApiExceptionDefinition(
                                                                                 typeof (MembershipCreateUserException),
                                                                                 statusCode: HttpStatusCode.Conflict)
                                                                             // this will send back a 409, for all other types of membership create user exceptions
                                                                         }, catchUnfilteredExceptions: true));
        }
    }
}

