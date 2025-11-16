namespace Hospital_simple.Models;

public class DoctorModel{
    public required ulong DoctorID{get; set;}
    public required string FirstName{get; set;}
    public required string LastName{get; set;}
    public required string Email{get; set;}
    public required string Specialization{get; set;}
}