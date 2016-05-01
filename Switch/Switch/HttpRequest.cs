using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SwitchPlus
{
    public class HttpRequest
    {
        public string Method
        {
            get;
            private set;
        }
        public string Url
        {
            get;
            private set;
        }

        public string Protocol
        {
            get;
            private set;
        }

        public HttpRequest(StreamReader sr)
        {
            var s = sr.ReadLine();
            string[] ss = s.Split(' ');
            Method = ss[0];
            Url = (ss.Length > 1) ? ss[1] : "NA";
            Protocol = (ss.Length > 2) ? ss[2] : "NA";
        }
    }
}
