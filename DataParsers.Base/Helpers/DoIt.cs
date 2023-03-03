using System;
using System.Threading;

namespace DataParsers.Base.Helpers;

public class DoIt
{
    public static T TryOrDefault<T>(Func<T> action, int retries = 0, T value = default, int timeout = 0)
    {
        for(var i = 0; i <= retries; i++)
            try
            {
                return action.Invoke();
            }
            catch(Exception e)
            {
                if(timeout > 0)
                    Thread.Sleep(timeout);
            }

        return value;
    }
}
