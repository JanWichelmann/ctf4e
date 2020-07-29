using System;

namespace Ctf4e.Server.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime Max(DateTime d1, DateTime d2)
        {
            return d1 > d2 ? d1 : d2;
        }
    }
}