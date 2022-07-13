using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BioService
{
    public static class BioServiceFactory
    {
        public static readonly HttpClient http;

        static BioServiceFactory()
        {
            http = new HttpClient
            {
                BaseAddress = new Uri(Properties.Settings.Default.ConectionURL),
            };
        }
    }
}
