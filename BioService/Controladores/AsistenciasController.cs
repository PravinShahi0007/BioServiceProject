using BioService.Modelos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace BioService.Controladores
{
    public static class AsistenciasController
    {
        public static async Task<bool> Send(List<Asistencia> listaAsistencias)
        {
            BioServiceFactory.http.DefaultRequestHeaders.Accept.Clear();

            var json = JsonConvert.SerializeObject(listaAsistencias);
            HttpContent httpContent = new StringContent(json, Encoding.UTF8);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await BioServiceFactory.http.PostAsync("asistencias", httpContent);
            return response.IsSuccessStatusCode;
        }

    }
}
