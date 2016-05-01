using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.IO;    

namespace SwitchPlus
{
    public class ImageStreamer : IDisposable
    {
        private List<Socket> _Clients = null;
        private Thread _Thread;
        public int port;
        public ImageStreamer(int w, int h, int p) : this( Screen.Snapshots(w, h, true))
        {
            port = p;            
        }
        public ImageStreamer(IEnumerable<Image> imageSource)
        {
            _Clients = new List<Socket>();
            _Thread = null;
            this.ImagesSource = imageSource;
            this.Interval = 30;
        }

        public IEnumerable<Image> ImagesSource { get; set; }
        public int Interval { get; set; }
        public IEnumerable<Socket> Clients { get { return _Clients; } }
        public bool IsRunning { get { return (_Thread != null && _Thread.IsAlive); } }

        public void Start(int port)
        {
            //if (this._Thread.ThreadState.Equals(ThreadState.Suspended))
            //{
            //    _Thread.Resume();
            //}
            //else
            //{
                lock (this)
                {
                    _Thread = new Thread(new ParameterizedThreadStart(ServerThread));
                    _Thread.IsBackground = true;
                    _Thread.Start(port);
                }
            //}
        }
        public void Start()
        {
            this.Start(port);
        }
        public void Stop()
        {
            if (this.IsRunning)
            {
                try
                {
                    _Thread.Suspend();
                    //_Thread.Join();
                    //_Thread.Abort();
                }
                catch
                {
                    MessageBox.Show("Error encountered! Unable to Continue.", "Error - Switch", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }
                finally
                {
                    lock (_Clients)
                        foreach (var s in _Clients)
                        {
                            try
                            {
                                s.Close();
                            }
                            catch { MessageBox.Show("AAA"); }
                        }
                    _Clients.Clear();
                }
                _Thread = null;
            }
        }

        private void ServerThread(object state)
        {
            try
            {
                Socket Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Server.Bind(new IPEndPoint(IPAddress.Any, (int)state));
                Server.Listen(10);

                System.Diagnostics.Debug.WriteLine(string.Format("Server started on port {0}.", state));
                foreach (Socket client in Server.IncommingConnections())
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), client);
            }
            catch { }
            this.Stop();
        }
        private void ClientThread(object client)
        {
            Socket socket = (Socket)client;
            System.Diagnostics.Debug.WriteLine(string.Format("New Client From {0}", socket.RemoteEndPoint.ToString()));
            lock (_Clients)
                _Clients.Add(socket);
            try
            {
                using (MjpegWriter wr = new MjpegWriter(new NetworkStream(socket, true)))
                {
                    wr.WriteHeader();
                    foreach (var imgStream in Screen.Streams(this.ImagesSource))
                    {
                        if (this.Interval > 0)
                            Thread.Sleep(this.Interval);
                        wr.Write(imgStream);
                    }
                }
            }
            catch { }
            finally
            {
                lock (_Clients)
                    _Clients.Remove(socket);
            }
        }

        #region IDisposable Members
        public void Dispose()
        {
            this.Stop();
        }
        #endregion;
    }

    static class SocketExtensions
    {
        public static IEnumerable<Socket> IncommingConnections(this Socket server)
        {
            while (true)
                yield return server.Accept();
        }
    }

    static class Screen
    {
        public static IEnumerable<Image> Snapshots()
        {
            return Screen.Snapshots(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
        }
        public static IEnumerable<Image> Snapshots(int width, int height, bool showCursor)
        {
            Size size = new Size(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
            Bitmap srcImage = new Bitmap(size.Width, size.Height);
            Graphics srcGraphics = Graphics.FromImage(srcImage);
            bool scaled = (width != size.Width || height != size.Height);
            Bitmap dstImage = srcImage;
            Graphics dstGraphics = srcGraphics;
            if (scaled)
            {
                dstImage = new Bitmap(width, height);
                dstGraphics = Graphics.FromImage(dstImage);
            }
            Rectangle src = new Rectangle(0, 0, size.Width, size.Height);
            Rectangle dst = new Rectangle(0, 0, width, height);
            Size curSize = new Size(32, 32);

            while (true)
            {
                srcGraphics.CopyFromScreen(0, 0, 0, 0, size);
                if (showCursor)
                    Cursors.Default.Draw(srcGraphics, new Rectangle(Cursor.Position, curSize));
                if (scaled)
                    dstGraphics.DrawImage(srcImage, dst, src, GraphicsUnit.Pixel);
                yield return dstImage;
            }
            srcGraphics.Dispose();
            dstGraphics.Dispose();
            srcImage.Dispose();
            dstImage.Dispose();
            yield break;
        }

        internal static IEnumerable<MemoryStream> Streams(this IEnumerable<Image> source)
        {
            MemoryStream ms = new MemoryStream();

            foreach (var img in source)
            {
                ms.SetLength(0);
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                yield return ms;
            }
            ms.Close();
            ms = null;
            yield break;
        }
    }

}
