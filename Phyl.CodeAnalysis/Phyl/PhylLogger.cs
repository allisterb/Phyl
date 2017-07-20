using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;
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
        public void Info(string messageTemplate, params object[] propertyValues)
        {
            L.Information(messageTemplate, propertyValues);
        }
        public void Success(string messageTemplate, params object[] propertyValues)
        {
            L.Information("[SUCCESS] " + messageTemplate, propertyValues);
        }

        public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (exception.InnerException != null)
            {
                L.Error(exception.InnerException, messageTemplate, propertyValues);
            }
            L.Error(exception, messageTemplate, propertyValues);
        }

        public void Error(string messageTemplate, params object[] propertyValues)
        {
            L.Error(messageTemplate, propertyValues);
        }
        #endregion
    }
}
