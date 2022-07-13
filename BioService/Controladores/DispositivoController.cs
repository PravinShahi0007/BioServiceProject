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
        public class ListaDispositivos
        {
            public bool success { get; set; }
            public List<Dispositivo> data { get; set; }
            public string message { get; set; }
        }

        public static async Task<List<Dispositivo>> GetList()
        {
            var response = await BioServiceFactory.http.GetAsync("dispositivos");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ListaDispositivos>(json).data;
        }
    }
}
