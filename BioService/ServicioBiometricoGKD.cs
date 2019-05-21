using System;
using System.Data;
using System.ServiceProcess;
using zkemkeeper;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Threading;
using System.Security.Permissions;
using System.Threading.Tasks;

namespace WindowsBiometricaService
{
    public partial class ServicioBiometricoGKD : ServiceBase
    {
        CZKEMClass dispositivo = new CZKEMClass(); 
        Timer timer1;
        Timer timer2;
        Timer timer3;
        Boolean Conectado;
        int idMaquina;
        private String direccion = "http://35.185.115.130/api/";
        private ListaDispositivos dispositivosList = new ListaDispositivos();

        public class Huella
        {
            public int id { get; set; }
            public string codigo { get; set; }
            public int user_id { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
            public object deleted_at { get; set; }
        }
        public class User
        {
            public string nombre { get; set; }
            public int credencial { get; set; }
            public List<Huella> huellas { get; set; }
            public string tag { get; set; }
        }
        public class Dispositivo
        {
            public int id { get; set; }
            public string name { get; set; }
            public string ip { get; set; }
            public string puerto { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
            public object deleted_at { get; set; }
        }
        public class ListaDispositivos
        {
            public bool success { get; set; }
            public List<Dispositivo> data { get; set; }
            public string message { get; set; }
        }

        DataTable dt;
        private Boolean datosCapturados;

        public ServicioBiometricoGKD()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                File.AppendAllText(@"C:\logger.log", "---------------------------------------------------------------------------- " + DateTime.Now + Environment.NewLine);
                datosCapturados = false;
                timer1 = new Timer(RegistroPrincipal, null, 2000, Timeout.Infinite); //20"
                timer3 = new Timer(EnviarData, null, 480000, 480000); //8' -repite cada 8' si es que no se envian las asistencias
                timer2 = new Timer(CheckNewUser, null, 600000, Timeout.Infinite); //10'
            }
            catch (Exception e)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR OnStart: " + e.Message + Environment.NewLine);
            }
        }

        protected override void OnStop()
        {
            timer1.Dispose();
            timer2.Dispose();
            timer3.Dispose();
        }

        private void RegistroPrincipal(Object state)
        {
            try
            {
                //Finalizo el timer una vez ejecutado
                timer1.Change(Timeout.Infinite, Timeout.Infinite);
                File.AppendAllText(@"C:\logger.log", "(REGISTRO PRINCIPAL)" + Environment.NewLine);
                //Traigo los dispositivos del server
                var httpWebRequestGet = (HttpWebRequest)WebRequest.Create(direccion + "dispositivos");
                httpWebRequestGet.ContentType = "application/json";
                httpWebRequestGet.Method = "GET";
                var httpResponse = (HttpWebResponse)httpWebRequestGet.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var json = streamReader.ReadToEnd();
                    dispositivosList = JsonConvert.DeserializeObject<ListaDispositivos>(json);
                }
                //Conecto con cada dispositivo y gestiono los datos
                foreach (var disp in dispositivosList.data)
                {
                    Conectar(disp.ip, disp.puerto, disp.id);
                }
            }
            catch (WebException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                if (!Conectado)
                    timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
            catch (ProtocolViolationException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                if (!Conectado)
                    timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + "  REG-ppal: " + ex.Message + Environment.NewLine);
                if (!Conectado)
                    timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
        }

        private void Conectar(string ip, string puerto, int idDisp)
        {
            File.AppendAllText(@"C:\logger.log", "Conectando... " + DateTime.Now + Environment.NewLine);
            Conectado = dispositivo.Connect_Net(ip, Convert.ToInt32(puerto));
            if (Conectado)  //Pruebo conexion con el dispositivo y continuo si se conecta
            {
                File.AppendAllText(@"C:\logger.log", "---------------INICIO OK---------------- " + DateTime.Now + Environment.NewLine);

                dispositivo.EnableDevice(1, false); //deshabilito el dispositivo momentaneamente hasta que se completen las operaciones
                                                    //asigno nuevo id de maquina
                idMaquina = idDisp;
                if (!datosCapturados)
                {
                    //datos para el backup de asistencias
                    string sdwEnrollNumber = ""; //nro credencial(id del cliente)
                    int idwVerifyMode = 0;
                    int idwInOutMode = 0;
                    int idwYear = 0;
                    int idwMonth = 0;
                    int idwDay = 0;
                    int idwHour = 0;
                    int idwMinute = 0;
                    int idwSecond = 0;
                    int idwWorkcode = 0;
                    //Creo una tabla para ir grabando los datos del log de asistencias, solo necesito el id de cliente y la fecha y hora de acceso
                    dt = new DataTable();
                    dt.Columns.Add("credencial", typeof(String));
                    dt.Columns.Add("horario", typeof(String));
                    dt.Columns.Add("id", typeof(String));
                    if (dispositivo.ReadGeneralLogData(idMaquina)) //Leo el log de datos con el numero de dispositivo = 1, si lo logra continua
                    {
                        //Recorro la lista de datos del log y grabo cada registro en la tabla "dt"
                        while (dispositivo.SSR_GetGeneralLogData(idMaquina, out sdwEnrollNumber, out idwVerifyMode,
                                    out idwInOutMode, out idwYear, out idwMonth, out idwDay, out idwHour, out idwMinute, out idwSecond, ref idwWorkcode))//get records from the memory
                        {
                            DataRow dr = dt.NewRow();
                            dr["credencial"] = sdwEnrollNumber;
                            dr["horario"] = idwYear + "-" + idwMonth + "-" + idwDay + " " + idwHour + ":" + idwMinute + ":" + idwSecond;
                            dr["id"] = idDisp;
                            dt.Rows.Add(dr);
                        }
                        datosCapturados = true;
                        try
                        {
                            EnviarData(null);
                        }
                        catch (Exception)
                        {
                            File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR envio de asistencias" + Environment.NewLine);
                            timer3.Change(480000, Timeout.Infinite);
                        }
                    }
                }
                GestionarUsuarios(idDisp); //Mando los usuarios al dispositivo
                dispositivo.EnableDevice(idMaquina, true); //Activo el dispositivo
            }
            else
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Conexion al Dispositivo" + Environment.NewLine);
                timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
        }

        private void GestionarUsuarios(int idD)
        {
            try
            {
                File.AppendAllText(@"C:\logger.log", "(GESTIONAR USUARIOS)" + Environment.NewLine);
                //Genero request para hacer un GET de los Usuarios que son staff y van como administrador en el dispositivo
                var consultaStaff = (HttpWebRequest)WebRequest.Create(direccion + "staff");
                consultaStaff.ContentType = "application/json";
                consultaStaff.Method = "GET";
                var responseStaff = (HttpWebResponse)consultaStaff.GetResponse();
                CrearUsers(responseStaff, 3); //Creo los usuarios que son staff 0:Normal; 1:Enrolador; 2:Admin; 3:SuperAdmin;
                //Genero otra request para hacer un GET de los datos de usuario a ser grabados en el dispositivo
                var consultaGet = (HttpWebRequest)WebRequest.Create(direccion + "dispositivos/id/" + idD);
                consultaGet.ContentType = "application/json";
                consultaGet.Method = "GET";
                var responseGET = (HttpWebResponse)consultaGet.GetResponse();
                CrearUsers(responseGET, 0);
            }
            catch (WebException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Red(gestionar usuarios): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                //timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
            catch (ProtocolViolationException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Red(gestionar usuarios): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                //timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " Error Registro-ppal(gestionar usuarios): " + ex.Message + Environment.NewLine);
                //timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
        }

        private void CrearUsers(HttpWebResponse respuestaHTTP, int privilegio)
        {
            File.AppendAllText(@"C:\logger.log", "Inicio envio de huellas..." + DateTime.Now + Environment.NewLine);
            var userList = new List<User>();
            using (var streamReader = new StreamReader(respuestaHTTP.GetResponseStream()))
            {
                var json = streamReader.ReadToEnd();
                userList = JsonConvert.DeserializeObject<List<User>>(json); //Convierto el json contenedor de los usuarios a una List<User>, con "User" objeto definido en la clase
            }
            //Comienzo el grabado de datos
            bool batchUpdate = dispositivo.BeginBatchUpdate(idMaquina, 1);
            for (int i = 0; i < userList.Count; i++)
            {
                //Registro las tarjetas(tags) por usuario
                if (!string.IsNullOrEmpty(userList[i].tag))
                    dispositivo.SetStrCardNumber(userList[i].tag);
                //Primero debo registrar los datos del usuario
                if (dispositivo.SSR_SetUserInfo(idMaquina, userList[i].credencial.ToString(), userList[i].nombre, string.Empty, privilegio, true))
                {
                    //Segundo grabo las huellas de cada usuario
                    if (userList[i].huellas != null)
                        foreach (var h in userList[i].huellas)
                        {
                            dispositivo.SetUserTmpExStr(idMaquina, userList[i].credencial.ToString(), h.id, 0, h.codigo);
                        }
                }
                else
                {
                    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de carga usuario" + Environment.NewLine);
                }
                //if (userList[i].credencial == 2 || userList[i].credencial == 3)
                //    dispositivo.EmptyCard(idMaquina);
            }
            //Envio los datos
            if (batchUpdate)
            {
                dispositivo.BatchUpdate(idMaquina);
            }
            dispositivo.RefreshData(idMaquina); //Actualizo el dispositivo para que las huellas queden activas
            File.AppendAllText(@"C:\logger.log", "Fin envio de huellas..." + DateTime.Now + Environment.NewLine);
        }

        private void CheckNewUser(Object state)
        {
            try
            {
                var httpWebRequestGet = (HttpWebRequest)WebRequest.Create(direccion + "usuariosNuevos");
                httpWebRequestGet.ContentType = "application/json";
                httpWebRequestGet.Method = "GET";
                var httpResponse = (HttpWebResponse)httpWebRequestGet.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var json = streamReader.ReadToEnd();
                    var userList = JsonConvert.DeserializeObject<List<User>>(json);
                    if (userList.Count > 0)
                    {
                        File.AppendAllText(@"C:\logger.log", "Capturado - " + DateTime.Now + Environment.NewLine);
                        foreach (var disp in dispositivosList.data)
                        {
                            if (dispositivo.Connect_Net(disp.ip, Convert.ToInt32(disp.puerto)))
                            {
                                File.AppendAllText(@"C:\logger.log", "Conectado - " + DateTime.Now + Environment.NewLine);
                                bool batchUpdate = false;
                                dispositivo.EnableDevice(idMaquina, false); //deshabilito el dispositivo momentaneamente hasta que se completen las operaciones
                                try
                                {
                                    batchUpdate = dispositivo.BeginBatchUpdate(idMaquina, 1);
                                }
                                catch (Exception e)
                                {
                                    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR begin batch: " + e.Message + Environment.NewLine);
                                }
                                for (int i = 0; i < userList.Count; i++)
                                {
                                    //Registro las tarjetas(tags) por usuario
                                    if (!string.IsNullOrEmpty(userList[i].tag))
                                        dispositivo.SetStrCardNumber(userList[i].tag);
                                    //Primero debo registrar los datos del usuario
                                    if (dispositivo.SSR_SetUserInfo(idMaquina, userList[i].credencial.ToString(), userList[i].nombre, string.Empty, 0, true))
                                    {
                                        //Segundo grabo las huellas de cada usuario
                                        if (userList[i].huellas != null)
                                            foreach (var h in userList[i].huellas)
                                            {
                                                dispositivo.SetUserTmpExStr(idMaquina, userList[i].credencial.ToString(), h.id, 0, h.codigo);
                                                //dispositivo.SSR_SetUserTmpStr(1, userList[i].credencial.ToString(), h.id, h.codigo);
                                            }
                                    }
                                    else
                                        File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de carga nuevo usuario" + Environment.NewLine);
                                }
                                //Envio los datos
                                if (batchUpdate)
                                {
                                    dispositivo.BatchUpdate(idMaquina);
                                }
                                dispositivo.RefreshData(idMaquina); //Actualizo el dispositivo para que las huellas queden activas
                                dispositivo.EnableDevice(idMaquina, true); //habilitar dispositivo
                                File.AppendAllText(@"C:\logger.log", "Habilitado - " + DateTime.Now + Environment.NewLine);
                            }
                        }
                    }
                }
                timer2.Change(10000, Timeout.Infinite);
            }
            catch (WebException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Red 1: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                timer2.Change(10000, Timeout.Infinite);
            }
            catch (ProtocolViolationException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Red 2: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                timer2.Change(10000, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " NEW-user 3: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                timer2.Change(10000, Timeout.Infinite);
            }
        }

        private void EnviarData(Object state)
        {
            if (datosCapturados)
            {
                try
                {
                    File.AppendAllText(@"C:\logger.log", "(EVIAR ASISTENCIAS)" + Environment.NewLine);
                    File.AppendAllText(@"C:\logger.log", "Inicio envio de asistencias..." + DateTime.Now + Environment.NewLine);
                    //Genero una request al servidor para hacer un POST de los datos recien guardados
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(direccion + "asistencias");
                    httpWebRequest.ContentType = "application/json";
                    httpWebRequest.Method = "POST";
                    //Envio los datos
                    var reqStream = httpWebRequest.GetRequestStream();
                    using (var streamWriter = new StreamWriter(reqStream))
                    {
                        string json = JsonConvert.SerializeObject(dt); //Convierto los datos de la tabla "dt" a un JSON, descargar la libreria desde nuget (Newtonsoft.Json) y ponerla como referencia
                        streamWriter.Write(json);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }
                    var r = httpWebRequest.GetResponse();
                    if (((HttpWebResponse)r).StatusCode == HttpStatusCode.OK)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            File.AppendAllText("AsistanceGral.txt", row[0] + "\t" + row[1] + "\t" + row[2] + Environment.NewLine);
                        }
                        dispositivo.ClearKeeperData(idMaquina); //Borro todos los datos(usuarios y asistencias) del dispositivo
                        File.AppendAllText(@"C:\logger.log", "Envio de asistencias OK   " + DateTime.Now + Environment.NewLine);
                        datosCapturados = false; //Reseteo por si falla algun otro metodo
                        timer3.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
                catch (WebException ex)
                {
                    File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red(enviar data): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                    timer3.Change(480000, Timeout.Infinite); //re intento a los 8 minutos
                }
                catch (ProtocolViolationException ex)
                {
                    File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red(enviar data): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                    timer3.Change(480000, Timeout.Infinite); //re intento a los 8 minutos
                }
                catch (Exception ex)
                {
                    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR POST(enviar data):" + ex.Message + Environment.NewLine);
                    timer3.Change(480000, Timeout.Infinite); //re intento a los 8 minutos
                }
            }
        }
    }
}
