using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Project_Managent_System.ViewModels
{
    public class UserProfileViewModel
    {
        // From Main_Users
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }

        // From User_Profiles
        public string EmployeeId { get; set; }
        public string Department { get; set; }
        public string Designation { get; set; }
        public string GitHubUrl { get; set; }
        public string LinkedInUrl { get; set; }
        public string Bio { get; set; }
        public string ProfilePicturePath { get; set; }
    }
}