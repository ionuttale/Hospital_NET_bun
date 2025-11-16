using System.ComponentModel.DataAnnotations;

namespace Hospital_simple.Models
{
    public class CreateDoctorUserModel
    {
        // Doctor Properties
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
        
        public string Specialization { get; set; }

        // User Properties
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}