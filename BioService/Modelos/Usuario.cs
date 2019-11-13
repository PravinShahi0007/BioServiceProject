using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioService.Modelos
{
    public class Usuario
    {
        public String nombre { get; set; }
        public Int32 credencial { get; set; }
        public List<Huella> huellas { get; set; }
        public String tag { get; set; }
    }
}
