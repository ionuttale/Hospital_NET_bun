using System.ComponentModel.DataAnnotations;

namespace Hospital_simple.Models
{
    public class CreateUserModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [Display(Name = "User Type")]
        public string UserType { get; set; } // "admin" or "client"

        [Display(Name = "Link to Doctor (Optional)")]
        public ulong? DoctorID { get; set; }
    }
}