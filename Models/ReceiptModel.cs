namespace Hospital_simple.Models;

public class MedicineModel
{
    public ulong MedicineID { get; set; }
    public string Name { get; set; }
}

public class ReceiptMedicineModel
{
    public ulong ReceiptMedicineID { get; set; }
    public MedicineModel Medicine { get; set; }
    public string Dosage { get; set; }
    public uint Quantity { get; set; } 
}

public class ReceiptModel
{
    public ulong? ConsultationID { get; set; }
    public ulong? ReceiptID { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string Disease { get; set; } = string.Empty;
    public List<ReceiptMedicineModel> Medicines { get; set; } = new List<ReceiptMedicineModel>();
}

