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
    
    public partial class NSERIES_RECEPCIONES
    {
        public int ID_SERIE { get; set; }
        public string NUMERO_SERIE { get; set; }
        public int PALET { get; set; }
        public string ALBARAN { get; set; }
        public Nullable<System.DateTime> F_REGISTRO { get; set; }
    
        public virtual PALET PALET1 { get; set; }
        public virtual RECEPCIONES_CAB RECEPCIONES_CAB { get; set; }
    }
}
