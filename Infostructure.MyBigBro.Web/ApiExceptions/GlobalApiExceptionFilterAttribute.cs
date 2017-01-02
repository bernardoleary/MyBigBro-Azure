using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Filters;

namespace Infostructure.MyBigBro.Web.ApiExceptions
{
    public class GlobalApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        private const string ERROR_CODE_KEY = "ErrorCode";
        private const string ERROR_REFERENCE_KEY = "ErrorReference";

        private List<GlobalApiExceptionDefinition> exceptionHandlers;
        private bool catchUnfilteredExceptions;

        public GlobalApiExceptionFilterAttribute(
            List<GlobalApiExceptionDefinition> exceptionHandlers = null, bool catchUnfilteredExceptions = false)
        {
            this.exceptionHandlers = exceptionHandlers ?? new List<GlobalApiExceptionDefinition>();
            this.catchUnfilteredExceptions = catchUnfilteredExceptions;
        }

        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            var exception = actionExecutedContext.Exception;
            GlobalApiExceptionDefinition globalExceptionDefinition = null;
            HttpStatusCode statusCode = HttpStatusCode.InternalServerError;

            if (LookupException(actionExecutedContext.Exception, out globalExceptionDefinition) ||
                catchUnfilteredExceptions)
            {
                // set the friendly message
                string friendlyMessage = globalExceptionDefinition != null
                                             ? globalExceptionDefinition.FriendlyMessage(exception)
                                             : exception.Message;

                // create the friendly http error
                var friendlyHttpError = new HttpError(friendlyMessage);

                // if we found a globalExceptionDefinition then set properties of our friendly httpError object accordingly
                if (globalExceptionDefinition != null)
                {

                    // set the status code
                    statusCode = globalExceptionDefinition.StatusCode;

                    // add optional error code
                    if (!string.IsNullOrEmpty(globalExceptionDefinition.ErrorCode))
                    {
                        friendlyHttpError[ERROR_CODE_KEY] = globalExceptionDefinition.ErrorCode;
                    }

                    // add optional error reference
                    if (!string.IsNullOrEmpty(globalExceptionDefinition.ErrorReference))
                    {
                        friendlyHttpError[ERROR_REFERENCE_KEY] = globalExceptionDefinition.ErrorReference;
                    }

                }

                // set the response to our friendly http error
                actionExecutedContext.Response = actionExecutedContext.Request.CreateErrorResponse(statusCode,
                                                                                                   friendlyHttpError);

            }

            // flow through to the base
            base.OnException(actionExecutedContext);
        }

        private bool LookupException(Exception exception, out GlobalApiExceptionDefinition exceptionMatch)
        {
            exceptionMatch = null;

            var possibleMatches = exceptionHandlers.Where(e => e.ExceptionType == exception.GetType());
            foreach (var possibleMatch in possibleMatches)
            {
                if (possibleMatch.Handle == null || possibleMatch.Handle(exception))
                {
                    exceptionMatch = possibleMatch;

                    return true;
                }
            }

            return false;
        }

    }
}
