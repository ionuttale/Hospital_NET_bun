namespace Hospital_simple.Models;

public class ForgotPasswordModel
{
    public required int RequestID{get; set;}
    public required ulong UserID{get; set;}
    public required string Token{get; set;}
    public required DateTime RequestTime{get; set;}
    public required string Email{get; set;}
    public required string Password{get; set;}
}