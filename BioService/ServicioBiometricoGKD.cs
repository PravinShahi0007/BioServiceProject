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
using BioService.Modelos;
using BioService.Controladores;

namespace WindowsBiometricaService
{
    public partial class ServicioBiometricoGKD : ServiceBase
    {
        CZKEMClass terminalZK = new CZKEMClass(); 
        Timer timer1;
        Timer timer2;
        Timer timer3;
        Boolean Conectado;
        int idMaquina;
        private String direccion = "http://34.95.241.213/api/";
        List<Dispositivo> listaDispositivos = new List<Dispositivo>();
        List<Usuario> listaUsuarios = new List<Usuario>();
        List<Usuario> listaUsuariosStaff = new List<Usuario>();

        public class UsuariosIngresados
        {
            public int ingresados { get; set; }
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
                datosCapturados = false;
                timer1 = new Timer(Inicio, null, Timeout.Infinite, Timeout.Infinite); 
                timer3 = new Timer(EnviarData, null, Timeout.Infinite, Timeout.Infinite); 
                timer2 = new Timer(CheckNewUser, null, Timeout.Infinite, Timeout.Infinite); 

                timer1.Change(2000, Timeout.Infinite); //20"
                timer3.Change(480000, 480000); //8' -repite cada 8' si es que no se envian las asistencias
                timer2.Change(600000, Timeout.Infinite); //10'
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

        private void Inicio(Object state)
        {
            try
            {
                //Finalizo el timer una vez ejecutado
                timer1.Change(Timeout.Infinite, Timeout.Infinite);
                File.AppendAllText(@"C:\logger.log", Environment.NewLine + "---------------------------------------------------------------------------- " + DateTime.Now + Environment.NewLine);
                File.AppendAllText(@"C:\logger.log", "(INICIO)" + Environment.NewLine);
                //Traigo los dispositivos del server
                listaDispositivos = DispositivoController.Lista();
                //Conecto con cada dispositivo y gestiono los datos
                foreach (var d in listaDispositivos)
                {
                    Conectar(d);
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

        private void Conectar(Dispositivo disp)
        {
            File.AppendAllText(@"C:\logger.log", "Conectando... " + DateTime.Now + Environment.NewLine);
            //Intento conectarme al terminal de zk por tcp
            if (!Conectado)
                Conectado = terminalZK.Connect_Net(disp.ip, Convert.ToInt32(disp.puerto));
            if (Conectado)
            {
                File.AppendAllText(@"C:\logger.log", "---------------CONECTADO---------------- " + DateTime.Now + Environment.NewLine);
                //asigno nuevo id de maquina
                idMaquina = disp.id;
                //Traigo los usuarios staff y comunes(clientes) con sus huellas del server
                listaUsuariosStaff = UsuarioController.Lista("staff");
                listaUsuarios = UsuarioController.Lista($"dispositivos/id/{idMaquina}");
                //deshabilito el terminal momentaneamente hasta que se completen las operaciones
                terminalZK.EnableDevice(idMaquina, false);
                GestionarAsistencias();
                GestionarUsuarios();
                terminalZK.EnableDevice(idMaquina, true); //Habilito el dispositivo
            }
            else
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Conexion al Dispositivo" + Environment.NewLine);
                timer1.Change(300000, Timeout.Infinite); //Re intento a los 5 minutos
            }
        }

        private void GestionarAsistencias()
        {
            if (!datosCapturados)
            {
                //datos para el backup de asistencias
                string enrollNumber = ""; //nro credencial(id del cliente)
                int verifyMode = 0;
                int inOutMode = 0;
                int year = 0;
                int month = 0;
                int day = 0;
                int hour = 0;
                int minute = 0;
                int second = 0;
                int workcode = 0;
                //Creo una tabla para ir grabando los datos del log de asistencias, solo necesito el id de cliente y la fecha y hora de acceso
                dt = new DataTable();
                dt.Columns.Add("credencial", typeof(String));
                dt.Columns.Add("horario", typeof(String));
                dt.Columns.Add("id", typeof(String));
                //Leo el log de datos con el numero de dispositivo (idMaquina), si lo logra continua
                if (terminalZK.ReadGeneralLogData(idMaquina))
                {
                    //Recorro la lista de datos del log y grabo cada registro en la tabla "dt"
                    while (terminalZK.SSR_GetGeneralLogData(idMaquina, out enrollNumber, out verifyMode, out inOutMode, out year, out month, out day, out hour, out minute, out second, ref workcode))//get records from the memory
                    {
                        DataRow dr = dt.NewRow();
                        dr["credencial"] = enrollNumber;
                        dr["horario"] = year + "-" + month + "-" + day + " " + hour + ":" + minute + ":" + second;
                        dr["id"] = idMaquina;
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
        }

        private void GestionarUsuarios()
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
                var consultaGet = (HttpWebRequest)WebRequest.Create(direccion + "dispositivos/id/" + idMaquina);
                consultaGet.ContentType = "application/json";
                consultaGet.Method = "GET";
                var responseGET = (HttpWebResponse)consultaGet.GetResponse();
                CrearUsers(responseGET, 0);
                EnviarTotalUsuarios(idMaquina); //Recupero y envio el total de usuarios registrados en el aparato
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
            var userList = new List<Usuario>();
            using (var streamReader = new StreamReader(respuestaHTTP.GetResponseStream()))
            {
                var json = streamReader.ReadToEnd();
                userList = JsonConvert.DeserializeObject<List<Usuario>>(json); //Convierto el json contenedor de los usuarios a una List<User>, con "User" objeto definido en la clase
            }
            //Comienzo el grabado de datos
            bool batchUpdate = terminalZK.BeginBatchUpdate(idMaquina, 1);
            for (int i = 0; i < userList.Count; i++)
            {
                //Registro las tarjetas(tags) por usuario
                if (!string.IsNullOrEmpty(userList[i].tag))
                    terminalZK.SetStrCardNumber(userList[i].tag);
                //Primero debo registrar los datos del usuario
                if (terminalZK.SSR_SetUserInfo(idMaquina, userList[i].credencial.ToString(), userList[i].nombre, string.Empty, privilegio, true))
                {
                    //Segundo grabo las huellas de cada usuario
                    if (userList[i].huellas != null)
                        foreach (var h in userList[i].huellas)
                        {
                            terminalZK.SetUserTmpExStr(idMaquina, userList[i].credencial.ToString(), h.id, 0, h.codigo);
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
                terminalZK.BatchUpdate(idMaquina);
            }
            terminalZK.RefreshData(idMaquina); //Actualizo el dispositivo para que las huellas queden activas
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
                    var userList = JsonConvert.DeserializeObject<List<Usuario>>(json);
                    if (userList.Count > 0)
                    {
                        File.AppendAllText(@"C:\logger.log", "Capturado - " + DateTime.Now + Environment.NewLine);
                        foreach (var disp in listaDispositivos)
                        {
                            if (terminalZK.Connect_Net(disp.ip, Convert.ToInt32(disp.puerto)))
                            {
                                File.AppendAllText(@"C:\logger.log", "Conectado - " + DateTime.Now + Environment.NewLine);
                                bool batchUpdate = false;
                                terminalZK.EnableDevice(idMaquina, false); //deshabilito el dispositivo momentaneamente hasta que se completen las operaciones
                                try
                                {
                                    batchUpdate = terminalZK.BeginBatchUpdate(idMaquina, 1);
                                }
                                catch (Exception e)
                                {
                                    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR begin batch: " + e.Message + Environment.NewLine);
                                }
                                for (int i = 0; i < userList.Count; i++)
                                {
                                    //Registro las tarjetas(tags) por usuario
                                    if (!string.IsNullOrEmpty(userList[i].tag))
                                        terminalZK.SetStrCardNumber(userList[i].tag);
                                    //Primero debo registrar los datos del usuario
                                    if (terminalZK.SSR_SetUserInfo(idMaquina, userList[i].credencial.ToString(), userList[i].nombre, string.Empty, 0, true))
                                    {
                                        //Segundo grabo las huellas de cada usuario
                                        if (userList[i].huellas != null)
                                            foreach (var h in userList[i].huellas)
                                            {
                                                terminalZK.SetUserTmpExStr(idMaquina, userList[i].credencial.ToString(), h.id, 0, h.codigo);
                                                //dispositivo.SSR_SetUserTmpStr(1, userList[i].credencial.ToString(), h.id, h.codigo);
                                            }
                                    }
                                    else
                                        File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de carga nuevo usuario" + Environment.NewLine);
                                }
                                //Envio los datos
                                if (batchUpdate)
                                {
                                    terminalZK.BatchUpdate(idMaquina);
                                }
                                terminalZK.RefreshData(idMaquina); //Actualizo el dispositivo para que las huellas queden activas
                                terminalZK.EnableDevice(idMaquina, true); //habilitar dispositivo
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
                        terminalZK.ClearKeeperData(idMaquina); //Borro todos los datos(usuarios y asistencias) del dispositivo
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

        private void EnviarTotalUsuarios(int idDispositivo)
        {
            try
            {
                string sEnrollNumber = "";
                bool bEnabled = false;
                string sName = "";
                string sPassword = "";
                int iPrivilege = 0;
                terminalZK.EnableDevice(idMaquina, false);
                terminalZK.ReadAllUserID(idMaquina);//read all the user information to the memory
                var usuariosIng = new UsuariosIngresados { ingresados = 0 };
                int totalStaff = 0;
                while (terminalZK.SSR_GetAllUserInfo(idMaquina, out sEnrollNumber, out sName, out sPassword, out iPrivilege, out bEnabled))//get all the users' information from the memory
                {
                    usuariosIng.ingresados++;
                    if (iPrivilege == 3)
                        totalStaff++;
                }
                File.AppendAllText(@"C:\logger.log", "(EVIAR TOTAL USUARIOS)" + Environment.NewLine);
                //Genero una request al servidor para hacer un POST de los datos recien guardados
                var httpWebRequest = (HttpWebRequest)WebRequest.Create($"{direccion}dispositivos/{idDispositivo}/ingresados");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                //Envio los datos
                var reqStream = httpWebRequest.GetRequestStream();
                using (var streamWriter = new StreamWriter(reqStream))
                {
                    string json = JsonConvert.SerializeObject(usuariosIng); //Convierto los datos de la tabla "dt" a un JSON, descargar la libreria desde nuget (Newtonsoft.Json) y ponerla como referencia
                    streamWriter.Write(json);
                    streamWriter.Flush();
                    streamWriter.Close();
                }
                var r = httpWebRequest.GetResponse();
                if (((HttpWebResponse)r).StatusCode == HttpStatusCode.OK)
                {
                    File.AppendAllText(@"C:\logger.log", "Total de usuarios enviados = " + usuariosIng.ingresados + "  staff = " + totalStaff + " " + DateTime.Now + Environment.NewLine);

                    using (Stream dataStream = r.GetResponseStream())
                    {
                        // Open the stream using a StreamReader for easy access.  
                        StreamReader reader = new StreamReader(dataStream);
                        // Read the content.  
                        string responseFromServer = reader.ReadToEnd();
                        // Display the content.  
                        File.AppendAllText(@"C:\logger.log", "response = " + responseFromServer + "  " + DateTime.Now + Environment.NewLine);
                    }
                }
            }
            catch (WebException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red(enviar total usuarios): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            }
            catch (ProtocolViolationException ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red(enviar total usuarios): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR POST(enviar total usuarios):" + ex.Message + Environment.NewLine);
            }

        }
    }
}
