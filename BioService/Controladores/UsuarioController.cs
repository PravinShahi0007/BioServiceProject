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
        public static async Task<List<Usuario>> GetList(int idMachine)
        {
            var lUsuarios = new List<Usuario>();

            var response = await BioServiceFactory.http.GetAsync("staff");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var lista = JsonConvert.DeserializeObject<List<Usuario>>(json);
            lUsuarios.AddRange(lista);
            lUsuarios.ForEach(x => x.privilegio = Privilegio.SuperAdminstrador);

            response = await BioServiceFactory.http.GetAsync($"dispositivos/id/{idMachine}");
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync();
            lista = JsonConvert.DeserializeObject<List<Usuario>>(json);
            lUsuarios.AddRange(lista);

            return lUsuarios;
        }
    }
}
