using Project_Managent_System.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity; // This enables Lambda support for .Include()

namespace Project_Managent_System.Controllers
{
    public class AdminController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        // ===============================
        // 🔐 SECURITY: Admin Authorization
        // ===============================
        private bool IsAdminAuthorized()
        {
            return Session["UserId"] != null && Session["Role"]?.ToString() == "Admin";
        }

        private ActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] = "Unauthorized access. Admins only.";
            return RedirectToAction("Login", "Signup_Login");
        }

        // ===============================
        // 👥 ADMIN DASHBOARD
        // ===============================
        public ActionResult AdminDashboard()
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();

            var allUsers = db.Main_Users.ToList();
            var projects = db.Projects.Include("Tasks").ToList();
            var tasks = db.Tasks.Include("Project").Include("Main_Users").ToList();

            // Data for Charts
            ViewBag.ProjectStatusCounts = projects.GroupBy(p => p.Status)
                                                  .Select(g => new { Status = g.Key ?? "Active", Count = g.Count() }).ToList();
            ViewBag.TaskPriorityCounts = tasks.GroupBy(t => t.Priority)
                                              .Select(g => new { Priority = g.Key, Count = g.Count() }).ToList();

            // User Stats
            ViewBag.AdminCount = allUsers.Count(u => u.Role == "Admin");
            ViewBag.ManagerCount = allUsers.Count(u => u.Role == "Manager");
            ViewBag.EmployeeCount = allUsers.Count(u => u.Role == "User");

            return View(new Tuple<List<Project>, List<Task>>(projects, tasks));
        }

        // ===============================
        // 👥 USER MANAGEMENT: ManageUsers
        // ===============================

        // GET: Admin/ManageUsers
        [HttpGet]
        public ActionResult AddUsers()
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();

            // Passing a new empty model to the View for the form
            return View(new Main_Users());
        }

        // POST: Admin/ManageUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddUsers(Main_Users newUser)
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Check for Duplicate Email
                    bool emailExists = db.Main_Users.Any(u => u.Email == newUser.Email);
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "This professional email is already assigned to another account.");
                        return View(newUser);
                    }

                    // 2. Attempt Database Save
                    db.Main_Users.Add(newUser);
                    db.SaveChanges();

                    // 3. Success Redirect
                    TempData["SuccessMessage"] = "Account for " + newUser.FirstName + " has been created successfully!";
                    return RedirectToAction("ManageUsers");
                }
                catch (Exception ex)
                {
                    // 4. Handle Database Exceptions (e.g. Connection issues)
                    ModelState.AddModelError("", "Database Error: " + ex.Message);
                }
            }
            else
            {
                ModelState.AddModelError("", "Please ensure all required fields are filled correctly.");
            }

            // Return to view with current data if anything fails
            return View(newUser);
        }

        //MANAGE USERS
        // GET: Admin/ManageUsers
        [HttpGet]
        public ActionResult ManageUsers()
        {
            // Check if the admin is logged in before fetching data
            if (!IsAdminAuthorized()) return RedirectToLogin();

            try
            {
                // Fetch all users from the Main_Users table
                var userList = db.Main_Users.ToList();

                // Pass the list to the view
                return View(userList);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error fetching users: " + ex.Message;
                return View(new List<Main_Users>());
            }
        }

        // ==========================================
        // 🗑️ DELETE: Delete Employee with Math Challenge
        // ==========================================

        // GET: Admin/DeleteUser/5
        [HttpGet]
        public ActionResult DeleteUser(int? id)
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();
            if (id == null) return RedirectToAction("ManageUsers");

            var user = db.Main_Users.Find(id);
            if (user == null) return HttpNotFound();

            // Generate Math Challenge
            Random rand = new Random();
            int num1 = rand.Next(1, 10);
            int num2 = rand.Next(1, 10);

            // Store the correct answer in Session for verification during POST
            Session["DeleteCaptcha"] = num1 + num2;

            ViewBag.Num1 = num1;
            ViewBag.Num2 = num2;

            return View(user);
        }

        // POST: Admin/DeleteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUser(int id, int captchaAnswer)
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();

            // 1. Verify Math Challenge Answer
            int? correctAnswer = Session["DeleteCaptcha"] as int?;
            if (correctAnswer == null || captchaAnswer != correctAnswer)
            {
                TempData["ErrorMessage"] = "Incorrect math answer. Deletion cancelled for security.";
                return RedirectToAction("DeleteUser", new { id = id });
            }

            try
            {
                var user = db.Main_Users.Find(id);
                if (user != null)
                {
                    // 2. Cascading Delete: Handle related records manually
                    // Delete related Comments
                    var comments = db.Comments.Where(c => c.UserId == id);
                    db.Comments.RemoveRange(comments);

                    // Delete Task Assignments linked to this user
                    var assignments = db.Task_Assignments.Where(a => a.UserId == id);
                    db.Task_Assignments.RemoveRange(assignments);

                    // Delete Project Assignments/Links
                    var projectLinks = db.ProjectUsers.Where(pu => pu.UserId == id);
                    db.ProjectUsers.RemoveRange(projectLinks);

                    // Finally, delete the User
                    db.Main_Users.Remove(user);
                    db.SaveChanges();

                    // Clear captcha session
                    Session["DeleteCaptcha"] = null;

                    TempData["SuccessMessage"] = "Employee and all related records deleted successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error during deletion: " + ex.Message;
            }

            return RedirectToAction("ManageUsers");
        }

        // ===============================
        // 👥 PROJECT MANAGEMENT: 
        // ===============================
        // GET: Admin/ManageProject
        [HttpGet]
        public ActionResult ManageProjects()
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();

            try
            {
                // Now the lambda expression will work without CS1660
                var projects = db.Projects
                                 
                                 .OrderByDescending(p => p.StartDate)
                                 .ToList();

                return View(projects);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading projects: " + ex.Message;
                return View(new List<Project>());
            }
        }

        // ===============================
        // 👥 TASK MANAGEMENT: 
        // ===============================
        // GET: Admin/ManageTasks
        [HttpGet]
        public ActionResult ManageTasks()
        {
            if (!IsAdminAuthorized()) return RedirectToLogin();

            try
            {
                // Now the lambda expression will work without CS1660
                var tasks = db.Tasks

                                 .OrderByDescending(p => p.StartDate)
                                 .ToList();

                return View(tasks);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading projects: " + ex.Message;
                return View(new List<Project>());
            }
        }


        // ===============================
        // 👥 COMMENTS MANAGEMENT: 
        // ===============================

        // 1. Fetch comments to display in the drawer
        // Fetch comments for a Project
        //public ActionResult GetProjectComments(int id)
        //{
        //    var comments = db.Comments
        //                     .Where(c => c.ProjectId == id)
        //                     .OrderBy(c => c.CreatedAt)
        //                     .ToList();

        //    ViewBag.TargetId = id;
        //    ViewBag.ChatType = "Project";

        //    return PartialView("_CommentThread", comments ?? new List<Comment>());
        //}

        // Fetch comments for a Task
        //public ActionResult GetTaskComments(int id)
        //{
        //    var comments = db.Comments
        //                     .Where(c => c.TaskId == id)
        //                     .OrderBy(c => c.CreatedAt)
        //                     .ToList();

        //    ViewBag.TargetId = id;
        //    ViewBag.ChatType = "Task";

        //    return PartialView("_CommentThread", comments ?? new List<Comment>());
        //}



        // 2. Save the new comment posted from the drawer
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult PostComment(int TargetId, string Messaage, string ChatType)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(Messaage))
        //            return Content("Message is required");

        //        if (Session["UserId"] == null)
        //            return Content("Session expired. Please login again.");

        //        int userId;
        //        if (!int.TryParse(Session["UserId"].ToString(), out userId))
        //            return Content("Invalid user session");

        //        var comment = new Comment
        //        {
        //            Messaage = Messaage.Trim(),   // ✅ FIXED NAME
        //            ProjectId = TargetId,
        //            UserId = userId,
        //            CreatedAt = DateTime.Now
        //        };

        //        db.Comments.Add(comment);
        //        db.SaveChanges();

        //        var comments = db.Comments
        //                         .Where(c => c.ProjectId == TargetId)
        //                         .OrderBy(c => c.CreatedAt)
        //                         .ToList();

        //        ViewBag.TargetId = TargetId;
        //        ViewBag.ChatType = ChatType;

        //        return PartialView("_CommentThread", comments);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Content("Error posting comment: " + ex.Message);
        //    }
        //}



    }
}