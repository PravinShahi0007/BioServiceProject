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
        int retries = 0;

        enum Commands
        {
            InsertNewUser = 130, //ExecuteCommand only accepts integers: 128-256, anything under 128 is system reserved
        }

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
            timerMain.Interval = TimeSpan.FromSeconds(15).TotalMilliseconds;
            //timerMain.Interval = TimeSpan.FromMinutes(15).TotalMilliseconds;
        }

        private async Task TimerMain_Elapsed()
        {
            try
            {
                timerMain.Stop();
                File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} Obteniendo Lista de Dispositivos.... (intento n° {++retries})");
                lDispositivos = await DispositivoController.GetList();

                foreach (var device in lDispositivos.FindAll(x => !x.StatusOK))
                {
                    if (device.Conectar())
                    {
                        try
                        {
                            var atts = device.GetAsistencias();
                            bool sendOK = false;
                            if (atts.Count > 0)
                            {
                                sendOK = await AsistenciasController.Send(atts);
                                if (sendOK)
                                    device.asistenciasOK = device.ClearAllAsistencias();
                                else
                                    device.asistenciasOK = false;
                            }
                            else
                                device.asistenciasOK = true;

                            File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} Total asistencias ({atts.Count})\n-----envíadas ({(sendOK ? atts.Count : 0)})\n-----eliminadas ({(device.asistenciasOK ? atts.Count: 0)})");
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} ERROR en Main: No se pudo completar la gestión de asistencias del equipo: {device.name} (ip: {device.ip} - id: {device.id})\n{ex.Message}\n{ex.InnerException} ");
                            continue;
                        }

                        try
                        {
                            var users = await UsuarioController.GetList(device.id);
                            if (users.Count > 0)
                                device.usuariosOK = device.UpdateOrCreateUsers(users);
                            else
                                device.usuariosOK = true;

                            File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} Total usuarios ({users.Count})\n-----creados ({(device.usuariosOK ? users.Count : 0)})");
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} ERROR en Main: No se pudo completar la gestión de usuarios del equipo: {device.name} (ip: {device.ip})\n{ex.Message}\n{ex.InnerException} ");
                        }
                    }
                    else
                    {
                        File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} ERROR en Main: No se pudo conectar con el equipo: {device.name} (ip: {device.ip})\n");
                    }
                }

                if (lDispositivos.Count > 0 && lDispositivos.TrueForAll(x => x.StatusOK))
                {
                    timerMain.Stop();
                    timerMain.Dispose();
                    File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} - FIN (todo ok)");
                }
                else
                {
                    timerMain.Start(); //starts after the interval defined
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\loggerRed.log", $"{DateTime.Now} ERROR en Main: método GetList(). \n{ex.StackTrace}\n{ex.Message}\n{ex.InnerException}\n");
                timerMain.Start();
            }
        }

        protected override async void OnCustomCommand(int command)
        {
            base.OnCustomCommand(command);
            if(command == (int)Commands.InsertNewUser)
            {
                var lista = await UsuarioController.GetNewUsers();
                if (lista.Count > 0)
                {
                    bool newOK = false;
                    foreach (var device in lDispositivos)
                    {
                        File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} Conectando Dispositivo: {device.name}.");
                        if (!device.conectado)
                            device.Conectar();

                        if (device.conectado)
                        {
                            File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} Enviando usuarios al dispositivo: {device.name}....");
                            newOK = device.UpdateOrCreateUsers(lista);
                            if (!newOK)
                            {
                                File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} ERROR en Insertar Nuevo Usuario: no se pudo crear el usuario en el dispositivo.");
                                continue;
                            }
                        }
                        else
                        {
                            File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} ERROR en Insertar Nuevo Usuario: no se pudo conectar al dispositivo.");
                        }
                    }
                    File.AppendAllText(@"C:\logger.log", $"\n{DateTime.Now} Total usuarios nuevos insertados ({(newOK ? lista.Count : 0)})");
                }
            }
        }
    }
}
