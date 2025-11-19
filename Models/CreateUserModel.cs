using System.ComponentModel.DataAnnotations;

namespace Hospital_simple.Models
{
    public class CreateUserModel
    {
        // --- New Doctor Details ---
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
        
        public string? Specialty { get; set; }

        // --- User Account Details ---
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [Display(Name = "User Type")]
        public string UserType { get; set; }  // "doctor" or "admin"
    }
}