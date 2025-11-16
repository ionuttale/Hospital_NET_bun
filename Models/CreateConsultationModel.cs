using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hospital_simple.Models
{
    /// <summary>
    /// Helper class for the medicine lines in the form
    /// </summary>
    public class ReceiptMedicineLineItem
    {
        public ulong MedicineID { get; set; } // Will be from a dropdown
        public uint Quantity { get; set; }
        public string Dosage { get; set; }
    }

    /// <summary>
    /// The main view model for the Create Consultation form
    /// </summary>
    public class CreateConsultationViewModel
    {
        // --- Patient Selection ---
        public bool IsNewPatient { get; set; }
        
        [Display(Name = "Existing Patient")]
        public ulong? ExistingPatientID { get; set; }
        
        // This will be used if IsNewPatient is true
        public PatientModel NewPatient { get; set; }

        // --- Consultation Details ---
        [Required]
        public string Disease { get; set; }
        public string Observation { get; set; }

        // --- Receipt Details ---
        // A list to hold all the medicines for the new receipt.
        public List<ReceiptMedicineLineItem> Medicines { get; set; }

        // Constructor to initialize the lists
        public CreateConsultationViewModel()
        {
            NewPatient = new PatientModel();
            Medicines = new List<ReceiptMedicineLineItem>();
        }
    }
}