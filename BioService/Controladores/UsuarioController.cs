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
        public static String direccion = Properties.Settings.Default.ConectionURL;

        public static List<Usuario> Lista(string usersDir)
        {
            var consultaStaff = (HttpWebRequest)WebRequest.Create(direccion + usersDir);
            consultaStaff.ContentType = "application/json";
            consultaStaff.Method = "GET";
            var respuestaHTTP = (HttpWebResponse)consultaStaff.GetResponse();
            using (var streamReader = new StreamReader(respuestaHTTP.GetResponseStream()))
            {
                var json = streamReader.ReadToEnd();
                var userList = JsonConvert.DeserializeObject<List<Usuario>>(json);
                return userList;
            }
        }
    }
}
