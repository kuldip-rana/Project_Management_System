using Project_Managent_System.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Rotativa.MVC;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Project_Managent_System.Controllers
{
    public class UserController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        // ===============================
        // 🔐 AUTHORIZATION HELPERS
        // ===============================
        private bool IsUserAuthorized()
        {
            return Session["UserId"] != null &&
                   Session["Role"] != null &&
                   Session["Role"].ToString() == "User";
        }

        private ActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] = "Unauthorized access. Please login as an Employee.";
            return RedirectToAction("Login", "Signup_Login");
        }

        // ===============================
        // 🏠 USER DASHBOARD (Overview)
        // ===============================
        public ActionResult UserDashboard()
        {
            if (!IsUserAuthorized()) return RedirectToLogin();

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);

                var myAssignments = db.Task_Assignments
                    .Include(a => a.Task)
                    .Where(a => a.UserId == userId)
                    .ToList() ?? new List<Task_Assignments>();

                // Stats for Dashboard UI
                ViewBag.PendingCount = myAssignments.Count(a => a.TaskStatus == "Pending");
                ViewBag.InProgressCount = myAssignments.Count(a => a.TaskStatus == "In Progress");
                ViewBag.CompletedCount = myAssignments.Count(a => a.TaskStatus == "Completed");

                // Data for Chart.js
                ViewBag.StatusLabels = new[] { "Pending", "In Progress", "Completed" };
                ViewBag.StatusData = new[] { ViewBag.PendingCount, ViewBag.InProgressCount, ViewBag.CompletedCount };

                var upcomingTasks = myAssignments
                    .Where(a => a.Task != null &&
                                a.TaskStatus != "Completed" &&
                                a.Task.DueDate >= DateTime.Now)
                    .Select(a => a.Task)
                    .OrderBy(t => t.DueDate)
                    .Take(5)
                    .ToList();

                return View(upcomingTasks);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Dashboard Error: " + ex.Message;
                return View(new List<Task>());
            }
        }

        // ===============================
        // 📁 MY PROJECTS (Explicit Join)
        // ===============================
        public ActionResult MyProjects()
        {
            if (!IsUserAuthorized()) return RedirectToLogin();

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);

                // Fetching projects strictly linked to tasks assigned to this user
                var projects = (from assignment in db.Task_Assignments
                                join task in db.Tasks on assignment.TaskId equals task.TaskId
                                join project in db.Projects on task.ProjectId equals project.ProjectId
                                where assignment.UserId == userId
                                select project)
                               .Distinct()
                               .OrderByDescending(p => p.StartDate)
                               .ToList();

                if (!projects.Any())
                {
                    ViewBag.DebugMessage = "No active project assignments found.";
                }

                return View(projects);
            }
            catch (Exception ex)
            {
                ViewBag.DebugMessage = "Error loading projects: " + (ex.InnerException?.Message ?? ex.Message);
                return View(new List<Project>());
            }
        }

        // ===============================
        // 📁 DOWNLOAD PROJECT + TASK REPORT
        // ===============================
        [HttpGet]
        public ActionResult DownloadFullProjectReport(int id)
        {
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "User")
            {
                return RedirectToAction("Login", "Signup_Login");
            }

            try
            {
                // 1. Load project data
                var project = db.Projects.FirstOrDefault(p => p.ProjectId == id);
                if (project == null) return HttpNotFound();

                // 2. Load tasks and assignments manually (EF6 safe)
                project.Tasks = db.Tasks
                                  .Where(t => t.ProjectId == project.ProjectId)
                                  .ToList();

                foreach (var task in project.Tasks)
                {
                    task.Task_Assignments = db.Task_Assignments
                                              .Where(a => a.TaskId == task.TaskId)
                                              .Include(a => a.Main_Users)
                                              .ToList();
                }

                // 3. Generate the dynamic report bytes using Rotativa
                var projectPdf = new ViewAsPdf("ProjectReport", project);
                byte[] projectPdfBytes = projectPdf.BuildPdf(ControllerContext);

                // 4. Merge the generated PDF with the uploaded Project_Document
                using (var outputStream = new MemoryStream())
                {
                    using (var document = new iTextSharp.text.Document())
                    {
                        using (var copy = new iTextSharp.text.pdf.PdfCopy(document, outputStream))
                        {
                            document.Open();

                            // Add dynamic report pages
                            using (var reader = new iTextSharp.text.pdf.PdfReader(projectPdfBytes))
                            {
                                for (int i = 1; i <= reader.NumberOfPages; i++)
                                {
                                    copy.AddPage(copy.GetImportedPage(reader, i));
                                }
                            }

                            // Add existing uploaded document if it exists
                            if (!string.IsNullOrEmpty(project.Project_Document))
                            {
                                string filePath = Server.MapPath("~/Uploads/Projects/" + project.Project_Document);
                                if (System.IO.File.Exists(filePath))
                                {
                                    using (var reader = new iTextSharp.text.pdf.PdfReader(filePath))
                                    {
                                        for (int i = 1; i <= reader.NumberOfPages; i++)
                                        {
                                            copy.AddPage(copy.GetImportedPage(reader, i));
                                        }
                                    }
                                }
                            }
                            document.Close();
                        }
                    }

                    // 5. Return the final combined PDF
                    return File(
                        outputStream.ToArray(),
                        "application/pdf",
                        $"Project_{project.ProjectName}_FullReport.pdf"
                    );
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "PDF generation failed: " + ex.Message;
                return RedirectToAction("MyProjects");
            }
        }

        // ===============================
        // 📝 MY TASKS (List)
        // ===============================
        public ActionResult MyTasks()
        {
            if (!IsUserAuthorized()) return RedirectToLogin();

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);

                var assignments = db.Task_Assignments
                    .Include(a => a.Task)
                    .Include(a => a.Task.Project)
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.AssignedAt)
                    .ToList();

                return View(assignments);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading tasks: " + ex.Message;
                return RedirectToAction("UserDashboard");
            }
        }

        // ===============================
        // 🔄 UPDATE TASK STATUS
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateTaskStatus(int assignmentId, string newStatus)
        {
            if (!IsUserAuthorized()) return RedirectToLogin();

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);
                var assignment = db.Task_Assignments.FirstOrDefault(a => a.Id == assignmentId && a.UserId == userId);

                if (assignment != null)
                {
                    assignment.TaskStatus = newStatus;
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Status updated!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to update status: " + ex.Message;
            }

            return Redirect(Request.UrlReferrer.ToString());
        }

        // ==========================================
        // 📄 VIEW: Full Task Details (Deep Dive)
        // ==========================================
        public ActionResult ViewTaskDetails(int? id)
        {
            // 1. Security Check
            if (!IsUserAuthorized()) return RedirectToLogin();

            // 2. ID Check: Redirect if ID is missing to prevent conversion errors
            if (id == null)
            {
                TempData["ErrorMessage"] = "Invalid Task selection.";
                return RedirectToAction("MyTasks");
            }

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);

                // 3. Fetch specific assignment including Task and Project details
                // We verify userId to ensure an employee can't "peek" at someone else's task ID
                var assignment = db.Task_Assignments
                    .Include(a => a.Task)
                    .Include(a => a.Task.Project)
                    .FirstOrDefault(a => a.TaskId == id && a.UserId == userId);

                if (assignment == null)
                {
                    TempData["ErrorMessage"] = "Task details not found or access denied.";
                    return RedirectToAction("MyTasks");
                }

                return View(assignment);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading task details: " + ex.Message;
                return RedirectToAction("MyTasks");
            }
        }

        /* ===============================
            💬 USER COMMUNICATION METHODS
         ================================ */

        // 🔹 Fetch conversation for a specific task
        //[HttpGet]
        //public ActionResult GetTaskComments(int? id)
        //{
        //    if (id == null) return Content("Task ID missing.");

        //    try
        //    {
        //        if (Session["UserId"] == null)
        //            return Content("<div class='alert alert-warning p-2 small'>Session expired.</div>");

        //        var comments = db.Comments
        //            .Include("Main_Users")
        //            .Where(c => c.TaskId == id)
        //            .OrderBy(c => c.CreatedAt)
        //            .ToList();

        //        ViewBag.TargetId = id;
        //        return PartialView("~/Views/Shared/_CommentThread.cshtml", comments);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Content("<div class='alert alert-danger p-2 small'>Error: " + ex.Message + "</div>");
        //    }
        //}

        // 🔹 Submit a new message (AJAX)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult PostComment(int TargetId, string Message)
        //{
        //    try
        //    {
        //        // 1. Validation Checks
        //        if (Session["UserId"] == null)
        //            return Json(new { success = false, message = "Session expired. Please refresh the page." });

        //        if (string.IsNullOrWhiteSpace(Message))
        //            return Json(new { success = false, message = "Message cannot be empty." });

        //        var task = db.Tasks.Find(TargetId);
        //        if (task == null)
        //            return Json(new { success = false, message = "Task not found." });

        //        // 2. Map and Save
        //        var comment = new Comment
        //        {
        //            UserId = Convert.ToInt32(Session["UserId"]),
        //            Messaage = Message.Trim(), // Keep your spelling 'Messaage' if that's what's in the DB
        //            CreatedAt = DateTime.Now,
        //            TaskId = TargetId,
        //            ProjectId = task.ProjectId
        //        };

        //        db.Comments.Add(comment);
        //        db.SaveChanges();

        //        return Json(new { success = true });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = "Database Error: " + ex.Message });
        //    }
        //}


        /* ===============================
            💬 USER TASK METHODS
         ================================ */

        public ActionResult UserTaskReport()
        {
            if (Session["UserId"] == null) return RedirectToAction("Login", "Account");

            int userId = Convert.ToInt32(Session["UserId"]);

            // Fetch assignments with Task and Project data
            var reports = db.Task_Assignments
                            .Include(a => a.Task)
                            .Include(a => a.Task.Project)
                            .Where(a => a.UserId == userId)
                            .ToList();

            // Pass stats to ViewBag for the top cards
            ViewBag.TotalTasks = reports.Count;
            ViewBag.CompletedTasks = reports.Count(t => t.TaskStatus == "Completed");
            ViewBag.PendingTasks = reports.Count(t => t.TaskStatus != "Completed");

            // Calculate Completion Percentage
            ViewBag.CompletionRate = reports.Count > 0
                ? (int)((double)ViewBag.CompletedTasks / ViewBag.TotalTasks * 100)
                : 0;

            return View(reports);
        }
    }
}