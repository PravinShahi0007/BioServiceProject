using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioService.Modelos
{
    public class Asistencia
    {
        public String credencial { get; set; }
        public DateTime horario { get; set; }
        public Int32 id { get; set; } //idMaquina
    }
}
