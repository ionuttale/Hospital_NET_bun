namespace Hospital_simple.Models;

public class MedicineModel
{
    public required ulong MedicineID { get; set; }
    public required string Name { get; set; }
}

public class ReceiptMedicineModel
{
    public required ulong ReceiptMedicineID { get; set; }
    public required MedicineModel Medicine { get; set; }
    public required string Dosage { get; set; }
    public required uint Quantity { get; set; } 
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

