//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ICP.BBDD
{
    using System;
    using System.Collections.Generic;
    
    public partial class PICKING_LOG
    {
        public int ID { get; set; }
        public string PEDIDO_ID { get; set; }
        public int LINEA_ID { get; set; }
        public int CANTIDAD_PICADA { get; set; }
        public string UBICACION { get; set; }
        public Nullable<System.DateTime> FECHA_PICADA { get; set; }
        public string USUARIO { get; set; }
        public string ESTADO { get; set; }
        public string PETICION { get; set; }
        public string REFERENCIA { get; set; }
    }
}
