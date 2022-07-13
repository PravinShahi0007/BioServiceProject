using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zkemkeeper;

namespace BioService.Modelos
{
    public class Dispositivo
    {
        public Int32 id { get; set; }
        public String name { get; set; }
        public String ip { get; set; }
        public String puerto { get; set; }
        public Boolean conectado { get; set; }
        public Boolean asistenciasOK { get; set; } //verficador de gestión completa de asistencias
        public Boolean usuariosOK { get; set; } //verficador de gestión completa de usuarios
        public Boolean StatusOK => conectado && asistenciasOK && usuariosOK;
        public Int32 totalAsistencias { get; set; }
        public CZKEMClass terminalZK { get; set; } = new CZKEMClass();


        public Boolean Conectar()
        {
            var port = Int32.Parse(this.puerto);
            this.conectado = terminalZK.Connect_Net(ip, port);
            return this.conectado;
        }

        Boolean EnableDispositivo(bool habilitar)
        {
            try
            {
                if(terminalZK.EnableDevice(1, habilitar))
                    return true;
                else if (Conectar())
                    return terminalZK.EnableDevice(1, habilitar);

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<Asistencia> GetAsistencias()
        {
            var lAsistencias = new List<Asistencia>();
            
            if (EnableDispositivo(false))
            {
                //datos para el backup de asistencias
                string enrollNumber = ""; //nro credencial(id del cliente)
                int verifyMode = 0, inOutMode = 0;
                int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                int workcode = 0;

                if (terminalZK.ReadGeneralLogData(1))
                {
                    //Recorro la lista de datos del log y grabo cada registro a retornar
                    while (terminalZK.SSR_GetGeneralLogData(1, out enrollNumber, out verifyMode, out inOutMode, out year, out month, out day, out hour, out minute, out second, ref workcode))//get records from the memory
                    {
                        var asistencia = new Asistencia()
                        {
                            credencial = enrollNumber,
                            horario = DateTime.Parse(year + "-" + month + "-" + day + " " + hour + ":" + minute + ":" + second),
                            id = this.id
                        };
                        lAsistencias.Add(asistencia);
                        this.totalAsistencias++;
                    }
                }
                EnableDispositivo(true); 
            }
            return lAsistencias;
        }

        public Boolean ClearAllAsistencias()
        {
            EnableDispositivo(false);
            var result = terminalZK.ClearGLog(1);
            EnableDispositivo(true);
            return result;
        }

        Boolean DisableUsers(List<Usuario> usuarios)
        {
            string sEnrollNumber = "", sName = "", sPassword = "";
            bool bEnabled = false;
            int iPrivilege = 0;
            try
            {
                if (usuarios.Any() && terminalZK.ReadAllUserID(1)) //read all the user information to the memory
                {
                    while (terminalZK.SSR_GetAllUserInfo(1, out sEnrollNumber, out sName, out sPassword, out iPrivilege, out bEnabled))//get all the users' information from the memory
                    {
                        if (iPrivilege == (int)Privilegio.Normal)
                        {
                            var user = usuarios.FirstOrDefault(x => $"{x.credencial}" == sEnrollNumber);
                            if (user == null)
                            {
                                terminalZK.SSR_EnableUser(1, sEnrollNumber, false);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Boolean UpdateOrCreateUsers(List<Usuario> usuarios)
        {
            if (EnableDispositivo(false))
            {
                try
                {
                    if (!DisableUsers(usuarios)) //Deshabilito los usuarios que no vienen en la lista de usuarios habilitados
                        return false;

                    //Comienzo el grabado de datos
                    bool batchUpdate = terminalZK.BeginBatchUpdate(1, 1);
                    foreach (var user in usuarios)
                    {
                        if (!string.IsNullOrEmpty(user.tag))
                            terminalZK.SetStrCardNumber(user.tag);

                        if (terminalZK.SSR_SetUserInfo(1, $"{user.credencial}", user.nombre, string.Empty, (int)user.privilegio, true))
                        {
                            //Segundo grabo las huellas de cada usuario
                            if (user.huellas != null)
                            {
                                foreach (var h in user.huellas)
                                {
                                    terminalZK.SetUserTmpExStr(1, user.credencial.ToString(), h.id, 0, h.codigo);
                                }
                            }
                        }
                    }
                    //Envio los datos
                    if (batchUpdate)
                    {
                        terminalZK.BatchUpdate(1);
                    }
                    terminalZK.RefreshData(1); //Actualizo el dispositivo para que las huellas queden activas

                    EnableDispositivo(true);

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        //public void DeleteUserData(int enrollNumber)
        //{
        //    if (conectado)
        //    {
        //        string nombre, credencial, huella = String.Empty;
        //        int priv, longitud = 0;
        //        bool habilitado;
        //        if (terminalZK.SSR_GetUserInfo(1, $"{enrollNumber}", out nombre, out credencial, out priv, out habilitado))
        //        {
        //            //var s = terminalZK.GetUserTmpStr(1, 1, 0, ref huella, ref longitud);
        //            //var s = terminalZK.SSR_DelUserTmpExt(1, $"{enrollNumber}", 0);
        //        }
        //    }
        //}
    }
}
