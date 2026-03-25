using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Project_Managent_System.ViewModels;
using Project_Managent_System.Models; // Ensure this points to where your .edmx models are

namespace Project_Managent_System.Controllers
{
    public class User_ProfilesController : Controller
    {
        // Replace 'PMS_DatabaseEntities' with the actual name of your DB Context
        private PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        // GET: User_Profiles/MyProfile
        public ActionResult MyProfile()
        {
            if (Session["UserId"] == null) return RedirectToAction("Index", "Home");

            int loggedInUserId = Convert.ToInt32(Session["UserId"]);

            // 1. FETCH DATA: Join Main_Users and User_Profiles using the ViewModel
            var profileData = db.Database.SqlQuery<UserProfileViewModel>(@"
                SELECT 
                    u.Id as UserId, 
                    u.FirstName, 
                    u.LastName, 
                    u.Email, 
                    u.Role,
                    p.EmployeeId, 
                    p.Department, 
                    p.Designation, 
                    p.GitHubUrl, 
                    p.LinkedInUrl, 
                    p.Bio, 
                    p.ProfilePicturePath
                FROM Main_Users u
                LEFT JOIN User_Profiles p ON u.Id = p.UserId
                WHERE u.Id = @p0", loggedInUserId).FirstOrDefault();

            // 2. FALLBACK: If user exists in Main_Users but has no profile row yet
            if (profileData == null)
            {
                profileData = new UserProfileViewModel
                {
                    UserId = loggedInUserId,
                    FirstName = Session["Name"]?.ToString(),
                    Email = Session["Email"]?.ToString(),
                    Role = Session["Role"]?.ToString()
                };
            }

            return View(profileData);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(UserProfileViewModel model, HttpPostedFileBase ProfileImage)
        {
            if (Session["UserId"] == null) return RedirectToAction("Index", "Home");
            int loggedInUserId = Convert.ToInt32(Session["UserId"]);

            // 1. Handle Profile Picture Upload
            string savedPath = model.ProfilePicturePath; // Keep old path by default
            if (ProfileImage != null && ProfileImage.ContentLength > 0)
            {
                string fileName = "User_" + loggedInUserId + Path.GetExtension(ProfileImage.FileName);
                string folderPath = Server.MapPath("~/Uploads/Profiles/");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string physicalPath = Path.Combine(folderPath, fileName);
                ProfileImage.SaveAs(physicalPath);
                savedPath = "/Uploads/Profiles/" + fileName;
            }

            // 2. DATABASE UPSERT: Insert if new, Update if exists
            try
            {
                string sql = @"
                    IF EXISTS (SELECT 1 FROM User_Profiles WHERE UserId = @p0)
                    BEGIN
                        UPDATE User_Profiles SET 
                            EmployeeId = @p1, Department = @p2, Designation = @p3, 
                            GitHubUrl = @p4, Bio = @p5, LinkedInUrl = @p6,
                            ProfilePicturePath = @p7, LastUpdated = GETDATE()
                        WHERE UserId = @p0
                    END
                    ELSE
                    BEGIN
                        INSERT INTO User_Profiles (UserId, EmployeeId, Department, Designation, GitHubUrl, Bio, LinkedInUrl, ProfilePicturePath, LastUpdated)
                        VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, GETDATE())
                    END";

                db.Database.ExecuteSqlCommand(sql,
                    loggedInUserId,
                    model.EmployeeId ?? (object)DBNull.Value,
                    model.Department ?? (object)DBNull.Value,
                    model.Designation ?? (object)DBNull.Value,
                    model.GitHubUrl ?? (object)DBNull.Value,
                    model.Bio ?? (object)DBNull.Value,
                    model.LinkedInUrl ?? (object)DBNull.Value,
                    savedPath ?? (object)DBNull.Value);

                TempData["Message"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Update failed: " + ex.Message;
            }

            return RedirectToAction("MyProfile");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}