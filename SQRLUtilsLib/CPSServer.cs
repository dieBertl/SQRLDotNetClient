﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Web;

namespace SQRLUtilsLib
{
    public class CPSServer
    {
        private Thread _serverThread;
        private HttpListener _listener;
        private int Port { get; } =25519;
        public BlockingCollection<Uri> cpsBC;
        public string Nut { get; set; }
        public Uri Can { get; set; }

        public bool PendingResponse = false;
        public CPSServer()
        {
            this.cpsBC = new BlockingCollection<Uri>();
            this.Initialize();

        }

        private void Initialize()
        {
            
            _serverThread = new Thread(this.Listen);
            _serverThread.Start();
        }

        private void Listen()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + this.Port.ToString() + "/");
            _listener.Start();
            while (true)
            {
                try
                {
                    Console.WriteLine("Http Listening");
                    HttpListenerContext context = _listener.GetContext();
                    Process(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with CPS: {ex}");
                }
            }
        }


        private void Process(HttpListenerContext context)
        {
            Console.WriteLine("Processing Request");
            string filename = context.Request.Url.AbsolutePath;
            
            string extension = Path.GetExtension(filename);
            if(extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Got Gif request");
                RespondWithGif(context);
                Console.WriteLine($"Responded to Gif request");
            }
            else
            {
                this.PendingResponse = true;
                
                RespondWithCPS(context);
                this.PendingResponse = false;
            }
        }

        private void RespondWithCPS(HttpListenerContext context)
        {
            Console.WriteLine($"Got CPS Request");
            string data = context.Request.Url.AbsolutePath.Substring(1);
            Sodium.SodiumCore.Init();
            Uri cpsData = new Uri(Encoding.UTF8.GetString(Sodium.Utilities.Base64ToBinary(data, "", Sodium.Utilities.Base64Variant.UrlSafeNoPadding)));
            var nvC= HttpUtility.ParseQueryString(cpsData.Query);
            if(nvC["nut"]!=null)
            {
                this.Nut = nvC["nut"];
            }
            if(nvC["can"]!=null)
            {
                this.Can = new Uri(Encoding.UTF8.GetString(Sodium.Utilities.Base64ToBinary(nvC["can"],string.Empty,Sodium.Utilities.Base64Variant.UrlSafeNoPadding)));
            }

            Console.WriteLine($"Holding here till CPS is Ready");
            foreach (var x in cpsBC.GetConsumingEnumerable())
            {
                Console.WriteLine($"Redirecting To: {x}");
                context.Response.Redirect(x.ToString());
                context.Response.StatusCode = (int)HttpStatusCode.Redirect;
                context.Response.Close();
               break;
            }
            Console.WriteLine($"Done with Request");

        }

        private void RespondWithGif(HttpListenerContext context)
        {
            try
            {
                
                var gif = GenerateGif();
                //Adding permanent http response headers
                context.Response.ContentType = "image/gif";
                context.Response.ContentLength64 = gif.Length;
                context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                context.Response.AddHeader("Last-Modified", DateTime.Now.ToString("r"));
                context.Response.OutputStream.Write(gif, 0, gif.Length);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Flush();
                context.Response.Close();
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        private byte[] GenerateGif()
        {
            //do tracking stuff ....

            //return empty gif
            const string clearGif1X1 = "R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==";
            return Convert.FromBase64String(clearGif1X1);
        }
    }
}
