using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioService.Modelos
{
    public class Dispositivo
    {
        public Int32 id { get; set; }
        public String name { get; set; }
        public String ip { get; set; }
        public String puerto { get; set; }
        public String created_at { get; set; }
        public String updated_at { get; set; }
        public Object deleted_at { get; set; }
    }
}
