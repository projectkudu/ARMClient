//------------------------------------------------------------------------------
// <copyright file="JwtHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.Utilities
{
    public class JwtHelper
    {
        public static JObject Parse(string jwtToken)
        {
            var claims = Base64UrlEncoder.Decode(jwtToken.Split('.')[1]);
            return JObject.Parse(claims);
        }

        static class Base64UrlEncoder
        {
            private static char base64PadCharacter = '=';
            private static string doubleBase64PadCharacter = string.Format("{0}{0}", Base64UrlEncoder.base64PadCharacter);
            private static char base64Character62 = '+';
            private static char base64Character63 = '/';
            private static char base64UrlCharacter62 = '-';
            private static char base64UrlCharacter63 = '_';

            public static string Encode(string arg)
            {
                if (arg == null)
                {
                    throw new ArgumentNullException(arg);
                }
                return Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(arg));
            }

            public static string Encode(byte[] arg)
            {
                if (arg == null)
                {
                    throw new ArgumentNullException("arg");
                }
                string text = Convert.ToBase64String(arg);
                text = text.Split(new[] { Base64UrlEncoder.base64PadCharacter })[0];
                text = text.Replace(Base64UrlEncoder.base64Character62, Base64UrlEncoder.base64UrlCharacter62);
                return text.Replace(Base64UrlEncoder.base64Character63, Base64UrlEncoder.base64UrlCharacter63);
            }

            public static byte[] DecodeBytes(string str)
            {
                if (str == null)
                {
                    throw new ArgumentNullException("str");
                }
                str = str.Replace(Base64UrlEncoder.base64UrlCharacter62, Base64UrlEncoder.base64Character62);
                str = str.Replace(Base64UrlEncoder.base64UrlCharacter63, Base64UrlEncoder.base64Character63);
                switch (str.Length % 4)
                {
                    case 0:
                        break;
                    case 2:
                        str += Base64UrlEncoder.doubleBase64PadCharacter;
                        break;
                    case 3:
                        str += Base64UrlEncoder.base64PadCharacter;
                        break;
                    default:
                        throw new InvalidOperationException(string.Format("JwtHelper: Unable to decode: '{0}' as Base64url encoded string.", str));
                }

                return Convert.FromBase64String(str);
            }

            public static string Decode(string str)
            {
                return Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(str));
            }
        }
    }
}