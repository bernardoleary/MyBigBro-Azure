using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Filters;

namespace Infostructure.MyBigBro.Web.ApiExceptions
{
    public class GlobalApiExceptionDefinition
    {
        private const string ARGUMENT_NULL_EXCEPTION_FMT = "Argument '{0}' cannot be null.";
        private const string ARGUMENT_MUST_INHERIT_FROM_FMT = "Type must inherit from {0}.";

        public Type ExceptionType { get; private set; }
        public Func<Exception, string> FriendlyMessage { get; private set; }

        public Func<Exception, bool> Handle { get; set; }
        public HttpStatusCode StatusCode { get; set; }

        public string ErrorCode { get; set; }
        public string ErrorReference { get; set; }

        public GlobalApiExceptionDefinition(Type exceptionType, string friendlyMessage = null,
                                            HttpStatusCode statusCode = HttpStatusCode.InternalServerError) :
            this(exceptionType, (ex) => friendlyMessage ?? ex.Message, statusCode)
        {
        }

        public GlobalApiExceptionDefinition(Type exceptionType, Func<Exception, string> friendlyMessage,
                                            HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        {

            AssertParameterIsNotNull(friendlyMessage, "friendlyMessage");
            AssertParameterIsNotNull(exceptionType, "exceptionType");
            AssertParameterInheritsFrom(exceptionType, typeof(Exception), "exceptionType");

            ExceptionType = exceptionType;
            FriendlyMessage = friendlyMessage;
            StatusCode = statusCode;
        }

        #region "Argument Assertions"

        private static void AssertParameterInheritsFrom(Type type, Type inheritedType, string name)
        {
            if (!type.IsSubclassOf(inheritedType))
            {
                throw new ArgumentException(string.Format(ARGUMENT_MUST_INHERIT_FROM_FMT, inheritedType.Name), name);
            }
        }

        private static void AssertParameterIsNotNull(object parameter, string name)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(name, string.Format(ARGUMENT_NULL_EXCEPTION_FMT, name));
            }
        }

        #endregion

    }
}