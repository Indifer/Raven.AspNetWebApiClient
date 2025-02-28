﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raven.Rpc.HttpProtocol.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    internal static class ExceptionOptimize
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        internal static Exception Filter(Exception ex)
        {
            var aggrExc = ex as AggregateException;
            if (aggrExc != null)
            {
                if (aggrExc.InnerException is TaskCanceledException || aggrExc.InnerExceptions.Any(x => x is TaskCanceledException))
                {
                    return new InvokeTimeoutException();
                }

            }
            else if (ex is TaskCanceledException)
            {
                return new InvokeTimeoutException();
            }
            return ex;
        }
    }
}
