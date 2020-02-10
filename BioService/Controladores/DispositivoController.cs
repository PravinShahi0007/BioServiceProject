using BioService.Modelos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BioService.Controladores
{
    public static class DispositivoController
    {
        public static String direccion = Properties.Settings.Default.ConectionURL;
        public class ListaDispositivos
        {
            public bool success { get; set; }
            public List<Dispositivo> data { get; set; }
            public string message { get; set; }
        }

        public static List<Dispositivo> Lista()
        {
            var httpWebRequestGet = (HttpWebRequest)WebRequest.Create(direccion + "dispositivos");
            httpWebRequestGet.ContentType = "application/json";
            httpWebRequestGet.Method = "GET";
            var httpResponse = (HttpWebResponse)httpWebRequestGet.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var json = streamReader.ReadToEnd();
                var dispositivosList = JsonConvert.DeserializeObject<ListaDispositivos>(json);
                return dispositivosList.data;
            }
        }
    }
}
