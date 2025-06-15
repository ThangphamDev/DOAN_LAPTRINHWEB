using Microsoft.AspNetCore.Http;
using System;
using System.Text;

namespace DOAN_LAPTRINHWEB.Extensions
{
    public static class SessionExtensions
    {
        public static string GetString(this ISession session, string key)
        {
            var data = session.Get(key);
            if (data == null)
            {
                return null;
            }
            return Encoding.UTF8.GetString(data);
        }

        public static void SetString(this ISession session, string key, string value)
        {
            session.Set(key, Encoding.UTF8.GetBytes(value));
        }

        public static bool TryGetString(this ISession session, string key, out string value)
        {
            value = null;
            var data = session.Get(key);
            if (data == null)
            {
                return false;
            }
            value = Encoding.UTF8.GetString(data);
            return true;
        }
    }
}