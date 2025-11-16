namespace Hospital_simple.Models
{
    public class UserModel
    {
        public required ulong UserID { get; set; }
        public required string Username { get; set; }
        public required string UserType { get; set; }
        public DateTime? LastLogin { get; set; }
        public ulong? DoctorID { get; set; }
    }
}