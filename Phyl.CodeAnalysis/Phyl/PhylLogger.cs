using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Serilog;
using SerilogTimings;
using SerilogTimings.Extensions;
namespace Phyl
{
    public class PhylLogger<T> where T: ILogged
    {
        #region Constructors
        public PhylLogger()
        {
            L = Log.ForContext<T>();
        }
        #endregion

        #region Properties
        ILogger L;
        #endregion

        #region Methods
        [DebuggerStepThrough]
        public void Info(string messageTemplate, params object[] propertyValues)
        {
            L.Information(messageTemplate, propertyValues);
        }

        [DebuggerStepThrough]
        public void Debug(string messageTemplate, params object[] propertyValues)
        {
            L.Debug(messageTemplate, propertyValues);
        }

        [DebuggerStepThrough]
        public void Success(string messageTemplate, params object[] propertyValues)
        {
            L.Information("[SUCCESS] " + messageTemplate, propertyValues);
        }

        [DebuggerStepThrough]
        public void Status(string messageTemplate, params object[] propertyValues)
        {
            L.Information(messageTemplate + "...", propertyValues);
        }

        [DebuggerStepThrough]
        public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (exception.InnerException != null)
            {
                L.Error(exception.InnerException, messageTemplate, propertyValues);
            }
            L.Error(exception, messageTemplate, propertyValues);
        }

        [DebuggerStepThrough]
        public void Error(string messageTemplate, params object[] propertyValues)
        {
            L.Error(messageTemplate, propertyValues);
        }

        [DebuggerStepThrough]
        public Operation Begin(string messageTemplate, params object[] args)
        {
            Debug(messageTemplate + "...", args);
            return L.BeginOperation(messageTemplate, args);
        }
        #endregion
    }
}
