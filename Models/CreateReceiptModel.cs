using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hospital_simple.Models
{
    public class CreateReceiptModel
    {
        [Required(ErrorMessage = "You must select a consultation.")]
        [Display(Name = "Consultation")]
        public ulong ConsultationID { get; set; }

        public List<ReceiptMedicineLineItem> Medicines { get; set; }

        public CreateReceiptModel()
        {
            Medicines = new List<ReceiptMedicineLineItem>();
        }
    }
}