using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SwitchPlus
{
    public class MicroServerCore
    {
        private Thread serverThread;
        TcpListener listener;

        /// <summary>
        /// Initializes a new instance of the "MicroServerCore" class.
        /// </summary>
        /// <param name="ipAddr">The ip address</param>
        /// <param name="port">The port number</param>
        public MicroServerCore(IPAddress ipAddr, int port)
        {
            listener = new TcpListener(ipAddr, port);
            try
            {
            //launch a thread to listen to the port
            serverThread = new Thread(() =>
            {
                listener.Start();
                while (true)
                {
                    Socket s = listener.AcceptSocket();
                    //Raw data received.
                    NetworkStream ns = new NetworkStream(s);
                    //Stream reader to 'interpert' it
                    StreamReader sr = new StreamReader(ns);
                    //Extracts data needed to construct an HttpReqeust object
                    HttpRequest req = new HttpRequest(sr);
                    //Determines to render a page or to execute command
                    HttpResponse resp = ProcessRequest(req);
                    StreamWriter sw = new StreamWriter(ns);
                    //Write response stream
                    sw.WriteLine("HTTP/1.1 {0}", resp.StatusText); 
                    sw.WriteLine("Content-Type: " + resp.ContentType);
                    sw.WriteLine("Content-Length: {0}", resp.Data.Length);
                    //Prevents the Ajax request being cached
                    sw.WriteLine("Cache-Control: no-cache");
                    sw.WriteLine();
                    sw.Flush();
                    s.Send(resp.Data);
                    //Close the connection
                    s.Shutdown(SocketShutdown.Both);
                    ns.Close();
                }
            });
            serverThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Remoteller Server terminated! ", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }



        /// <summary>
        /// Processes the request.
        /// </summary>
        /// <param name="req">The req.</param>
        /// <returns></returns>
        private HttpResponse ProcessRequest(HttpRequest req)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (req.Url.EndsWith("down")) //If you click the 'down' button, then it sends a GET request like 'http://192.168.0.1:1688/down'
                {
                    PPTAction.ControlPPT(ActionType.DOWN);
                }
                else if (req.Url.EndsWith("up"))
                {
                    PPTAction.ControlPPT(ActionType.UP);
                }
                else if (req.Url.EndsWith("left"))
                {
                    PPTAction.ControlPPT(ActionType.LEFT);
                }
                else if (req.Url.EndsWith("right"))
                {
                    PPTAction.ControlPPT(ActionType.RIGHT);
                }
                else if (req.Url.EndsWith("esc"))
                {
                    PPTAction.ControlPPT(ActionType.ESC);
                }
                else if (req.Url.EndsWith("backspace"))
                {
                    PPTAction.ControlPPT(ActionType.BACKSPACE);
                }
                else if (req.Url.EndsWith("home"))
                {
                    PPTAction.ControlPPT(ActionType.HOME);
                }
                else if (req.Url.EndsWith("tab"))
                {
                    PPTAction.ControlPPT(ActionType.TAB);
                }
                else if (req.Url.EndsWith("delete"))
                {
                    PPTAction.ControlPPT(ActionType.DELETE);
                }
                else if (req.Url.EndsWith("end"))
                {
                    PPTAction.ControlPPT(ActionType.END);
                }
                else if (req.Url.EndsWith("enter"))
                {
                    PPTAction.ControlPPT(ActionType.ENTER);
                }
                else
                {
                    // if not requests like this, renders the page
                    Assembly _assembly = Assembly.GetExecutingAssembly();
                    StreamReader sr = new StreamReader(_assembly.GetManifestResourceStream("SwitchPlus.Remoteller.txt"));
                    string tempString = string.Empty;

                    while (!string.IsNullOrEmpty(tempString = sr.ReadLine()))
                    {
                        sb.Append(tempString);
                    }
                }
                return new HttpResponse()
                {
                    ContentType = "text/html",
                    Data = Encoding.UTF8.GetBytes(sb.ToString())
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show("Request not completed! " + ex.ToString(), "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new HttpResponse()
                {
                    ContentType = "text/html",
                    Data = Encoding.UTF8.GetBytes("")
                };
            }
        }

        public void Stop()
        {
            try
            {
                listener.Stop();
                serverThread.Abort();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Remoteller server stopped! " + ex.ToString(), "Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                listener.Start();
                serverThread.Resume();
            }
        }
    }
}
