namespace Hospital_simple.Models;

public class ConsultationModels
{
    public  ulong ConsultationID { get; set; }
    public ulong PatientID { get; set; }
    public string? PatientName { get; set; }   // New
    public ulong DoctorID { get; set; }
    public string? DoctorName { get; set; }    // New
    public DateTime ConsultationDate { get; set; }
    public string? Disease { get; set; }
    public string? Observation { get; set; }
    public ulong? ReceiptID { get; set; }
}
