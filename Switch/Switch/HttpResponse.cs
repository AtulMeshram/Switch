using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SwitchPlus
{
    public class HttpResponse
    {
        public string StatusText
        {
            get;
            set;
        }
        public string ContentType
        {
            get;
            set;
        }
        public byte[] Data
        {
            get;
            set;
        }
        public HttpResponse()
        {
            StatusText = "200 OK";
            ContentType = "text/plain";
            Data = new byte[] { };
        }
    }
}
