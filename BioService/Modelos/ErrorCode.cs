using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BioService.Modelos
{
    public class ErrorCode
    {
        [Description("Conexión exitosa")]
        public const int ConnectSuccess = 0;

        [Description("Fallo al invocar la interfáz")]
        public const int InterfaceFail = -1;

        [Description("Fallo el inicializar")]
        public const int InitFail = -2;

        [Description("Fallo al inicializar parámetros")]
        public const int InitParaFail = -3;

        [Description("Error en el modo de lectura de datos")]
        public const int ReadDataModeFail = -5;

        [Description("Contraseña incorrecta")]
        public const int PasswordError = -6;

        [Description("Error de respuesta")]
        public const int ReplyError = -7;

        [Description("Fallo al recibir tiempo de espera")]
        public const int ReceiveTimeout = -8;

        [Description("Tiempo de espera agotado (connection timeout)")]
        public const int ConnTimeout = -307;

        public static string GetErrMsg(int errCode)
        {
            foreach (FieldInfo item in typeof(ErrorCode).GetFields())
            {
                if (Convert.ToInt32(item.GetValue(typeof(Int32))).Equals(errCode))
                {
                    return ((DescriptionAttribute)item.GetCustomAttribute(typeof(DescriptionAttribute))).Description;
                }
            }
            return "NULL";
        }
    }
}
