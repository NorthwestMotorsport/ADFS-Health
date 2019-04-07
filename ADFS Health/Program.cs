using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Net.Http;

namespace ADFS_Health
{
    class Program
    {
        private string remoteHost = "localhost";
        private int timeout = 5;
        private int interval = 30;
        private bool healthy = false;
        static HttpListener listener;

        static string USAGE = "Usage: adfshealth [remotehost:[hostname]] [interval:[seconds]] [timeout:[seconds]]";

        static int Main(string[] args)
        {
            int RETURN_VALUE = 0;

            if (args.Length > 0 && args[0] == "/?")
            {
                Console.WriteLine(USAGE);
            }
            else
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                foreach (string arg in args)
                {
                    string[] keyAndValue = arg.Split(':');

                    string key = "";
                    string value = "";

                    if (keyAndValue.Length > 0)
                        key = keyAndValue[0];
                    if (keyAndValue.Length > 1)
                        value = keyAndValue[1];

                    parameters.Add(key, value);
                }

                Program instance = new Program(parameters);
            }
            return RETURN_VALUE;
        }

        Program(Dictionary<string, string> parameters)
        {
            // Set parameters
            if (parameters.Count > 0)
            {
                parameters.TryGetValue("remotehost", out this.remoteHost);

                string interval;
                if (parameters.TryGetValue("interval", out interval))
                {
                    Int32.TryParse(interval, out this.interval);
                }

                string timeout;
                if (parameters.TryGetValue("timeout", out timeout))
                {
                    Int32.TryParse(timeout, out this.timeout);
                }

            }

            // Start probing
            //this.Probe();
            Thread probeThread = new Thread(Probe);
            probeThread.Start();


            // Create a listener.
            listener = new HttpListener();
            listener.Prefixes.Add("http://*/adfshealth/");
            //listener.Prefixes.Add("http://" + Environment.MachineName + "/adfshealth/");

            Console.WriteLine("Listening...");
            listener.Start();

            Thread listenerThread = new Thread(new ParameterizedThreadStart(Listen));
            listenerThread.Start();

            probeThread.Join();
            listenerThread.Join();
        }

        private void Probe()
        {
            //this.remoteHost = "aws-usw2a-dc";
            Uri remoteUri = new Uri("http://" + this.remoteHost + "/adfs/probe/");
            while (true)
            {

                try
                {
                    this.healthy = (doProbe(remoteUri).Result == HttpStatusCode.OK);
                }
                catch (AggregateException ae)
                {
                    this.healthy = false;
                }
                

                Console.WriteLine("Healhty? " + this.healthy);
                Console.WriteLine("Wait " + this.interval + " seconds");
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(this.interval));
            }
        }

        private async Task<HttpStatusCode> doProbe(Uri uri)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(this.timeout);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, uri);

            HttpResponseMessage response =
                   await httpClient.SendAsync(request);

            return response.StatusCode;
        }

       


        private void Listen(object s)
        {
            while(true)
            {
                processRequest();
            }   
            
        }

        private void processRequest()
        {
            // Note: The GetContext method blocks while waiting for a request. 
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest request = context.Request;
            // Obtain a response object.
            HttpListenerResponse response = context.Response;

            // Determine status
            if (this.healthy)
                response.StatusCode = 200;
            else
                response.StatusCode = 503;

            response.Close();
        }
    }
}
