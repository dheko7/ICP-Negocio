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
    
    public partial class RECEPCIONES_CAB
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public RECEPCIONES_CAB()
        {
            this.NSERIES_RECEPCIONES = new HashSet<NSERIES_RECEPCIONES>();
            this.PALETS = new HashSet<PALET>();
            this.RECEPCIONES_LIN = new HashSet<RECEPCIONES_LIN>();
        }
    
        public string ALBARAN { get; set; }
        public string PROVEEDOR { get; set; }
        public Nullable<System.DateTime> F_CREACION { get; set; }
        public int ESTATUS_RECEPCION { get; set; }
        public Nullable<System.DateTime> F_CONFIRMACION { get; set; }
        public string CODIGO_CLIENTE { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<NSERIES_RECEPCIONES> NSERIES_RECEPCIONES { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<PALET> PALETS { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<RECEPCIONES_LIN> RECEPCIONES_LIN { get; set; }
    }
}
