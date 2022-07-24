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
    public static class UsuarioController
    {
        public static async Task<List<Usuario>> GetList(int idDevice)
        {
            var lUsuarios = new List<Usuario>();

            var response = await BioServiceFactory.http.GetAsync("staff");
            if (response.IsSuccessStatusCode)
            {
                var jsonS = await response.Content.ReadAsStringAsync();
                var listaS = JsonConvert.DeserializeObject<List<Usuario>>(jsonS);
                lUsuarios.AddRange(listaS);
                lUsuarios.ForEach(x => x.privilegio = Privilegio.SuperAdminstrador);
            }

            response = await BioServiceFactory.http.GetAsync($"dispositivos/id/{idDevice}");
            if (response.IsSuccessStatusCode)
            {
                var jsonU = await response.Content.ReadAsStringAsync();
                var listaU = JsonConvert.DeserializeObject<List<Usuario>>(jsonU);
                lUsuarios.AddRange(listaU);
            }

            return lUsuarios;
        }

        public static async Task<List<Usuario>> GetNewUsers()
        {
            var lUsuarios = new List<Usuario>();

            var response = await BioServiceFactory.http.GetAsync("usuariosNuevos");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var lista = JsonConvert.DeserializeObject<List<Usuario>>(json);
                lUsuarios.AddRange(lista);
            }
            return lUsuarios;
        }
    }
}
