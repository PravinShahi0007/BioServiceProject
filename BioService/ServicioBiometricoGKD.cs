using System;
using System.Data;
using System.ServiceProcess;
using zkemkeeper;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Security.Permissions;
using BioService.Modelos;
using BioService.Controladores;
using System.Timers;
using System.Threading.Tasks;

namespace WindowsBiometricaService
{
    public partial class ServicioBiometricoGKD : ServiceBase
    {
        Timer timerMain;
        List<Dispositivo> lDispositivos = new List<Dispositivo>();

        public ServicioBiometricoGKD()
        {
            InitializeComponent();
        }

        internal void TestStartupAndStop(string[] args)
        {
            this.OnStart(args);
            Console.ReadLine();
            this.OnStop();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                SetTimers();
                timerMain.Start();
            }
            catch (Exception e)
            {
                File.AppendAllText(@"C:\logger.log", $"{DateTime.Now} ERROR FATAL en OnStart: {e.Message}\n{e.InnerException}\n");
                Environment.Exit(0);
            }
        }

        protected override void OnStop()
        {
            if (timerMain != null)
            {
                timerMain.Stop();
                timerMain.Dispose();
            }
        }

        private void SetTimers()
        {
            timerMain = new Timer();
            timerMain.Elapsed += async (e, sender) => await TimerMain_Elapsed();
            //timerMain.Interval = TimeSpan.FromSeconds(15).TotalMilliseconds;
            timerMain.Interval = TimeSpan.FromMinutes(15).TotalMilliseconds;
        }

        private async Task TimerMain_Elapsed()
        {
            try
            {
                timerMain.Stop();
                lDispositivos = await DispositivoController.GetList();

                foreach (var device in lDispositivos.FindAll(x => !x.StatusOK))
                {
                    if (device.Conectar())
                    {
                        try
                        {
                            var atts = device.GetAsistencias();
                            if (atts.Count > 0)
                            {
                                var sendOK = await AsistenciasController.Send(atts);
                                if (sendOK)
                                    device.asistenciasOK = device.ClearAllAsistencias();
                            }
                            else
                                device.asistenciasOK = true;
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(@"C:\logger.log", $"{DateTime.Now} ERROR en Main: No se pudo completar la gestión de asistencias del equipo: {device.name} (ip: {device.ip})\n{ex.Message}\n{ex.InnerException} ");
                            continue;
                        }

                        try
                        {
                            var users = await UsuarioController.GetList(device.id);
                            if (users.Count > 0)
                                device.usuariosOK = device.UpdateOrCreateUsers(users);
                            else
                                device.usuariosOK = true;
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(@"C:\logger.log", $"{DateTime.Now} ERROR en Main: No se pudo completar la gestión de usuarios del equipo: {device.name} (ip: {device.ip})\n{ex.Message}\n{ex.InnerException} ");
                        }
                    }
                    else
                    {
                        File.AppendAllText(@"C:\logger.log", $"{DateTime.Now} ERROR en Main: No se pudo conectar con el equipo: {device.name} (ip: {device.ip})\n");
                    }
                }

                if (lDispositivos.TrueForAll(x => x.StatusOK))
                {
                    timerMain.Stop();
                    timerMain.Dispose();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\logger.log", $"{DateTime.Now} ERROR en Main: {ex.Message}\n{ex.InnerException}\n");
            }
        }

        private void CheckNewUser(Object state)
        {
            //try
            //{
            //    var httpWebRequestGet = (HttpWebRequest)WebRequest.Create(direccion + "usuariosNuevos");
            //    httpWebRequestGet.ContentType = "application/json";
            //    httpWebRequestGet.Method = "GET";
            //    var httpResponse = (HttpWebResponse)httpWebRequestGet.GetResponse();
            //    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            //    {
            //        var json = streamReader.ReadToEnd();
            //        var userList = JsonConvert.DeserializeObject<List<Usuario>>(json);
            //        if (userList.Count > 0)
            //        {
            //            File.AppendAllText(@"C:\logger.log", "Capturado - " + DateTime.Now + Environment.NewLine);
            //            foreach (var disp in lDispositivos)
            //            {
            //                if (terminalZK.Connect_Net(disp.ip, Convert.ToInt32(disp.puerto)))
            //                {
            //                    File.AppendAllText(@"C:\logger.log", "Conectado - " + DateTime.Now + Environment.NewLine);
            //                    bool batchUpdate = false;
            //                    terminalZK.EnableDevice(idMaquina, false); //deshabilito el dispositivo momentaneamente hasta que se completen las operaciones
            //                    try
            //                    {
            //                        batchUpdate = terminalZK.BeginBatchUpdate(idMaquina, 1);
            //                    }
            //                    catch (Exception e)
            //                    {
            //                        File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR begin batch: " + e.Message + Environment.NewLine);
            //                    }
            //                    for (int i = 0; i < userList.Count; i++)
            //                    {
            //                        //Registro las tarjetas(tags) por usuario
            //                        if (!string.IsNullOrEmpty(userList[i].tag))
            //                            terminalZK.SetStrCardNumber(userList[i].tag);
            //                        //Primero debo registrar los datos del usuario
            //                        if (terminalZK.SSR_SetUserInfo(idMaquina, userList[i].credencial.ToString(), userList[i].nombre, string.Empty, 0, true))
            //                        {
            //                            //Segundo grabo las huellas de cada usuario
            //                            if (userList[i].huellas != null)
            //                                foreach (var h in userList[i].huellas)
            //                                {
            //                                    terminalZK.SetUserTmpExStr(idMaquina, userList[i].credencial.ToString(), h.id, 0, h.codigo);
            //                                    //dispositivo.SSR_SetUserTmpStr(1, userList[i].credencial.ToString(), h.id, h.codigo);
            //                                }
            //                        }
            //                        else
            //                            File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de carga nuevo usuario" + Environment.NewLine);
            //                    }
            //                    //Envio los datos
            //                    if (batchUpdate)
            //                    {
            //                        terminalZK.BatchUpdate(idMaquina);
            //                    }
            //                    terminalZK.RefreshData(idMaquina); //Actualizo el dispositivo para que las huellas queden activas
            //                    terminalZK.EnableDevice(idMaquina, true); //habilitar dispositivo
            //                    File.AppendAllText(@"C:\logger.log", "Habilitado - " + DateTime.Now + Environment.NewLine);
            //                }
            //            }
            //        }
            //    }
            //    timer2.Change(10000, Timeout.Infinite);
            //}
            //catch (WebException ex)
            //{
            //    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Red 1: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            //    timer2.Change(10000, Timeout.Infinite);
            //}
            //catch (ProtocolViolationException ex)
            //{
            //    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR de Red 2: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            //    timer2.Change(10000, Timeout.Infinite);
            //}
            //catch (Exception ex)
            //{
            //    File.AppendAllText(@"C:\logger.log", DateTime.Now + " NEW-user 3: " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            //    timer2.Change(10000, Timeout.Infinite);
            //}
        }

        private void EnviarTotalUsuarios(int idDispositivo)
        {
            //try
            //{
            //    string sEnrollNumber = "";
            //    bool bEnabled = false;
            //    string sName = "";
            //    string sPassword = "";
            //    int iPrivilege = 0;
            //    terminalZK.EnableDevice(idMaquina, false);
            //    terminalZK.ReadAllUserID(idMaquina);//read all the user information to the memory
            //    var usuariosIng = new UsuariosIngresados { ingresados = 0 };
            //    int totalStaff = 0;
            //    while (terminalZK.SSR_GetAllUserInfo(idMaquina, out sEnrollNumber, out sName, out sPassword, out iPrivilege, out bEnabled))//get all the users' information from the memory
            //    {
            //        usuariosIng.ingresados++;
            //        if (iPrivilege == 3)
            //            totalStaff++;
            //    }
            //    File.AppendAllText(@"C:\logger.log", "(EVIAR TOTAL USUARIOS)" + Environment.NewLine);
            //    //Genero una request al servidor para hacer un POST de los datos recien guardados
            //    var httpWebRequest = (HttpWebRequest)WebRequest.Create($"{direccion}dispositivos/{idDispositivo}/ingresados");
            //    httpWebRequest.ContentType = "application/json";
            //    httpWebRequest.Method = "POST";
            //    //Envio los datos
            //    var reqStream = httpWebRequest.GetRequestStream();
            //    using (var streamWriter = new StreamWriter(reqStream))
            //    {
            //        string json = JsonConvert.SerializeObject(usuariosIng); //Convierto los datos de la tabla "dt" a un JSON, descargar la libreria desde nuget (Newtonsoft.Json) y ponerla como referencia
            //        streamWriter.Write(json);
            //        streamWriter.Flush();
            //        streamWriter.Close();
            //    }
            //    var r = httpWebRequest.GetResponse();
            //    if (((HttpWebResponse)r).StatusCode == HttpStatusCode.OK)
            //    {
            //        File.AppendAllText(@"C:\logger.log", "Total de usuarios enviados = " + usuariosIng.ingresados + "  staff = " + totalStaff + " " + DateTime.Now + Environment.NewLine);

            //        using (Stream dataStream = r.GetResponseStream())
            //        {
            //            // Open the stream using a StreamReader for easy access.  
            //            StreamReader reader = new StreamReader(dataStream);
            //            // Read the content.  
            //            string responseFromServer = reader.ReadToEnd();
            //            // Display the content.  
            //            File.AppendAllText(@"C:\logger.log", "response = " + responseFromServer + "  " + DateTime.Now + Environment.NewLine);
            //        }
            //    }
            //}
            //catch (WebException ex)
            //{
            //    File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red(enviar total usuarios): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            //}
            //catch (ProtocolViolationException ex)
            //{
            //    File.AppendAllText(@"C:\logger.log", DateTime.Now + "  ERROR de Red(enviar total usuarios): " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            //}
            //catch (Exception ex)
            //{
            //    File.AppendAllText(@"C:\logger.log", DateTime.Now + " ERROR POST(enviar total usuarios):" + ex.Message + Environment.NewLine);
            //}

        }
    }
}
