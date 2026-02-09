using iTextSharp.text;
using iTextSharp.text.pdf;
using Project_Managent_System.Models;
using Rotativa.MVC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace Project_Managent_System.Controllers
{
    public class ManagerController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        /* ==========================
           AUTHORIZATION CHECK
        =========================== */
        private bool IsManagerLoggedIn()
        {
            return Session["UserId"] != null &&
                   Session["Role"] != null &&
                   Session["Role"].ToString() == "Manager";
        }

        /* ==========================
          MANAGER DASHBOARD
       =========================== */
        public ActionResult ManagerDashboard()
        {
            if (!IsManagerLoggedIn())
                return RedirectToAction("Login", "Signup_Login");

            int managerId = Convert.ToInt32(Session["UserId"]);

            ViewBag.Projects = db.ProjectUsers
                .Where(pu => pu.UserId == managerId)
                .Select(pu => new SelectListItem
                {
                    Value = pu.Project.ProjectId.ToString(),
                    Text = pu.Project.ProjectName
                })
                .Distinct()
                .ToList();

            return View();
        }

        // =========================
        // AJAX DATA SOURCE
        // =========================
        [HttpGet]
        public JsonResult GetDashboardData(int? projectId, DateTime? fromDate, DateTime? toDate)
        {
            int managerId = Convert.ToInt32(Session["UserId"]);

            var tasks = db.Tasks
                .Where(t => t.CreatedByManagerId == managerId);

            if (projectId.HasValue)
                tasks = tasks.Where(t => t.ProjectId == projectId);

            if (fromDate.HasValue)
                tasks = tasks.Where(t => t.CreatedAt >= fromDate);

            if (toDate.HasValue)
                tasks = tasks.Where(t => t.CreatedAt <= toDate);

            var taskList = tasks.ToList();

            return Json(new
            {
                completed = taskList.Count(t => t.Status == "Completed"),
                pending = taskList.Count(t => t.Status == "Pending"),
                inProgress = taskList.Count(t => t.Status == "In Progress"),

                projectNames = taskList
                    .GroupBy(t => t.Project.ProjectName)
                    .Select(g => g.Key)
                    .ToList(),

                tasksPerProject = taskList
                    .GroupBy(t => t.Project.ProjectName)
                    .Select(g => g.Count())
                    .ToList(),

                timelineLabels = taskList
                    .GroupBy(t => t.CreatedAt.Date)
                    .Select(g => g.Key.ToString("dd MMM"))
                    .ToList(),

                timelineData = taskList
                    .GroupBy(t => t.CreatedAt.Date)
                    .Select(g => g.Count())
                    .ToList()

            }, JsonRequestBehavior.AllowGet);
        }



        /* ==========================
           CREATE PROJECT (GET)
        =========================== */
        public ActionResult CreateProject()
        {
            if (!IsManagerLoggedIn())
                return RedirectToAction("Login", "Signup_Login");

            ViewBag.Users = new MultiSelectList(
                db.Main_Users.ToList(),
                "Id",
                "FirstName"
            );

            return View();
        }

        /* ==========================
             CREATE PROJECT (POST)
        =========================== */
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateProject(
            Project project,
            List<int> SelectedUserIds,
            HttpPostedFileBase ProjectDocument)
        {
            try
            {
                if (!IsManagerLoggedIn())
                    return RedirectToAction("Login", "Signup_Login");

                ViewBag.Users = new MultiSelectList(
                    db.Main_Users.ToList(),
                    "Id",
                    "FirstName",
                    SelectedUserIds
                );

                project.Project_Members = Convert.ToString(Session["Name"]);
                project.Status = "Pending";
                //project.StartDate = DateTime.Now;

                if (!ModelState.IsValid)
                    return View(project);

                /* ---------- STRICT FILE VALIDATION ---------- */
                if (ProjectDocument != null && ProjectDocument.ContentLength > 0)
                {
                    // Max size = 3 MB
                    int maxSizeInBytes = 3 * 1024 * 1024;

                    if (ProjectDocument.ContentLength > maxSizeInBytes)
                    {
                        ModelState.AddModelError("", "File size must not exceed 3 MB.");
                        return View(project);
                    }

                    // Validate extension
                    string extension = Path.GetExtension(ProjectDocument.FileName).ToLower();

                    if (extension != ".pdf")
                    {
                        ModelState.AddModelError("", "Only PDF files are allowed.");
                        return View(project);
                    }

                    // Validate MIME type
                    if (ProjectDocument.ContentType != "application/pdf")
                    {
                        ModelState.AddModelError("", "Invalid PDF file.");
                        return View(project);
                    }

                    // Safe file name
                    string fileName = Guid.NewGuid() + ".pdf";
                    string uploadPath = Server.MapPath("~/Uploads/Projects/");

                    if (!Directory.Exists(uploadPath))
                        Directory.CreateDirectory(uploadPath);

                    string fullPath = Path.Combine(uploadPath, fileName);
                    ProjectDocument.SaveAs(fullPath);

                    project.Project_Document = fileName;
                }
                else
                {
                    ModelState.AddModelError("", "Project document is required.");
                    return View(project);
                }

                /* ---------- SAVE PROJECT ---------- */
                db.Projects.Add(project);
                db.SaveChanges();

                /* ---------- SAVE PROJECT MEMBERS ---------- */
                if (SelectedUserIds != null && SelectedUserIds.Any())
                {
                    foreach (var userId in SelectedUserIds)
                    {
                        if (!db.Main_Users.Any(u => u.Id == userId))
                            throw new Exception("Invalid user selected.");

                        db.ProjectUsers.Add(new ProjectUser
                        {
                            ProjectId = project.ProjectId,
                            UserId = userId
                        });
                    }
                    db.SaveChanges();
                }

                TempData["SuccessMessage"] = "Project created successfully.";
                return RedirectToAction("ManagerDashboard");
            }
            catch (Exception ex)
            {
                // Log exception (optional)
                ModelState.AddModelError("", "Error: " + ex.Message);
                return View(project);
            }
        }

        /* ==========================
           DISPLAY LIST OF PROJECTS
        =========================== */
        public ActionResult DisplayProjects()
        {
            if (!IsManagerLoggedIn())
                return RedirectToAction("Login", "Signup_Login");

            try
            {
                string managerName = Session["Name"].ToString();

                var projects = db.Projects
                                 .Where(p => p.Project_Members == managerName)
                                 .OrderByDescending(p => p.ProjectId)
                                 .ToList();

                return View(projects);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading projects: " + ex.Message;
                return RedirectToAction("ManagerDashboard");
            }
        }


        /* ==========================
         PROJECT REPORT (MERGE PDF ONLY)
         =========================== */
        public ActionResult ProjectReport(int id)
        {
            try
            {
                // 1️. Get project
                var project = db.Projects.FirstOrDefault(p => p.ProjectId == id);
                if (project == null)
                    return HttpNotFound();

                // 2️. Generate project details PDF using Rotativa
                var projectPdf = new Rotativa.MVC.ViewAsPdf("ProjectReport", project);
                byte[] projectPdfBytes = projectPdf.BuildPdf(ControllerContext);

                // 3️. Uploaded PDF path
                string uploadedFilePath = string.Empty;

                if (!string.IsNullOrEmpty(project.Project_Document))
                {
                    uploadedFilePath = Server.MapPath("~/Uploads/Projects/" + project.Project_Document);
                }

                // 4️. Merge PDFs
                using (var outputStream = new MemoryStream())
                {
                    using (iTextSharp.text.Document document = new iTextSharp.text.Document())
                    {
                        using (PdfCopy copy = new PdfCopy(document, outputStream))
                        {
                            document.Open();

                            // ➕ Add Project Details PDF
                            using (PdfReader reader = new PdfReader(projectPdfBytes))
                            {
                                for (int i = 1; i <= reader.NumberOfPages; i++)
                                {
                                    copy.AddPage(copy.GetImportedPage(reader, i));
                                }
                            }

                            // ➕ Add Uploaded PDF (if exists)
                            if (!string.IsNullOrEmpty(uploadedFilePath) &&
                                System.IO.File.Exists(uploadedFilePath))
                            {
                                using (PdfReader reader = new PdfReader(uploadedFilePath))
                                {
                                    for (int i = 1; i <= reader.NumberOfPages; i++)
                                    {
                                        copy.AddPage(copy.GetImportedPage(reader, i));
                                    }
                                }
                            }

                            document.Close();
                        }
                    }

                    // 5️. Return merged PDF
                    return File(
                        outputStream.ToArray(),
                        "application/pdf",
                        $"Project_{project.ProjectName}_FullReport.pdf"
                    );
                }
            }
            catch (Exception ex)
            {
                // Optional logging
                TempData["ErrorMessage"] = "Error generating project report: " + ex.Message;
                return RedirectToAction("ManagerDashboard");
            }
        }

        /* ==========================
           DELETE PROJECTS (Cascading Child Records)
        ========================== */
        [HttpPost] // Changed to HttpPost for security
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProject(int id)
        {
            if (!IsManagerLoggedIn())
                return RedirectToAction("Login", "Signup_Login");

            // Find the project
            var project = db.Projects.FirstOrDefault(p => p.ProjectId == id);
            if (project == null)
                return HttpNotFound();

            try
            {
                // 1. Get all Task IDs associated with this project first
                var projectTasksIds = db.Tasks
                                        .Where(t => t.ProjectId == id)
                                        .Select(t => t.TaskId)
                                        .ToList();

                // 2. Remove related Comments 
                // We check for comments linked to the Project OR linked to any of the Project's Tasks
                var relatedComments = db.Comments
                    .Where(c => c.ProjectId == id || projectTasksIds.Contains(c.TaskId))
                    .ToList();

                if (relatedComments.Any())
                {
                    db.Comments.RemoveRange(relatedComments);
                }

                // 3. Remove related Task Assignments
                var relatedAssignments = db.Task_Assignments
                    .Where(ta => projectTasksIds.Contains(ta.TaskId))
                    .ToList();

                if (relatedAssignments.Any())
                {
                    db.Task_Assignments.RemoveRange(relatedAssignments);
                }

                // 4. Remove related ProjectUsers (Project Members)
                var projectUsers = db.ProjectUsers.Where(pu => pu.ProjectId == id).ToList();
                if (projectUsers.Any())
                {
                    db.ProjectUsers.RemoveRange(projectUsers);
                }

                // 5. Remove the Tasks
                var relatedTasks = db.Tasks.Where(t => t.ProjectId == id).ToList();
                if (relatedTasks.Any())
                {
                    db.Tasks.RemoveRange(relatedTasks);
                }

                // 6. Delete the physical PDF file
                if (!string.IsNullOrEmpty(project.Project_Document))
                {
                    string filePath = Server.MapPath("~/Uploads/Projects/" + project.Project_Document);
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }

                // 7. Finally, remove the Project
                db.Projects.Remove(project);
                db.SaveChanges();

                TempData["DeletedMessage"] = "Project and all related data deleted successfully.";
                return RedirectToAction("DisplayProjects");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return RedirectToAction("DisplayProjects");
            }
        }

        /* ==========================
          EDIT PROJECT (GET)
        =========================== */
        public ActionResult EditProject(int id)
        {
            if (!IsManagerLoggedIn())
                return RedirectToAction("Login", "Signup_Login");

            var project = db.Projects.Find(id);
            if (project == null)
                return HttpNotFound();

            ViewBag.CurrentUserName = project.Project_Members; // leader name

            return View(project);
        }

        /*======================
         EDIT PROJECT (POST)
        ========================*/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProject(Project model, HttpPostedFileBase ProjectDocument)
        {
            try
            {
                if (!IsManagerLoggedIn())
                    return RedirectToAction("Login", "Signup_Login");

                var project = db.Projects.Find(model.ProjectId);
                if (project == null)
                    return HttpNotFound();

                if (!ModelState.IsValid)
                    return View(model);

                // ---------- Update fields ----------
                project.ProjectName = model.ProjectName;
                project.Description = model.Description;
                project.StartDate = model.StartDate; // past allowed
                project.EndDate = model.EndDate;
                project.Status = model.Status;

                // ---------- Optional PDF replacement ----------
                if (ProjectDocument != null && ProjectDocument.ContentLength > 0)
                {
                    // size <= 3MB
                    int maxSize = 3 * 1024 * 1024;
                    if (ProjectDocument.ContentLength > maxSize)
                        throw new Exception("PDF size must not exceed 3 MB.");

                    // extension check
                    string extension = Path.GetExtension(ProjectDocument.FileName).ToLower();
                    if (extension != ".pdf")
                        throw new Exception("Only PDF files are allowed.");

                    // MIME check
                    if (ProjectDocument.ContentType != "application/pdf")
                        throw new Exception("Invalid PDF file.");

                    // delete old file (if exists)
                    if (!string.IsNullOrEmpty(project.Project_Document))
                    {
                        string oldPath = Server.MapPath("~/Uploads/Projects/" + project.Project_Document);
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    // save new file
                    string newFileName = Guid.NewGuid() + ".pdf";
                    string uploadPath = Server.MapPath("~/Uploads/Projects/");
                    if (!Directory.Exists(uploadPath))
                        Directory.CreateDirectory(uploadPath);

                    string fullPath = Path.Combine(uploadPath, newFileName);
                    ProjectDocument.SaveAs(fullPath);

                    project.Project_Document = newFileName;
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = "Project updated successfully!";
                return RedirectToAction("DisplayProjects");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        /* ==========================
            FETCH PROJECT COMMENTS
        =========================== */
        //[HttpGet]
        //public PartialViewResult GetProjectComments(int id)
        //{
        //    // Ensure "id" matches the ProjectId in your Comment table
        //    var comments = db.Comments
        //                     .Where(c => c.ProjectId == id)
        //                     .OrderBy(c => c.CreatedAt)
        //                     .ToList();

        //    ViewBag.TargetId = id;
        //    ViewBag.ChatType = "Project";

        //    return PartialView("~/Views/Shared/_CommentThread.cshtml", comments);
        //}

        /* ==========================
            POST A NEW COMMENT
        =========================== */
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult PostComment(int TargetId, string ChatType, string Messaage)
        //{
        //    if (string.IsNullOrWhiteSpace(Messaage))
        //        return Redirect(Request.UrlReferrer.ToString());

        //    var comment = new Comment
        //    {
        //        UserId = Convert.ToInt32(Session["UserId"]),
        //        Messaage = Messaage.Trim(), // Matches your DB model spelling
        //        CreatedAt = DateTime.Now
        //    };

        //    if (ChatType == "Project")
        //        comment.ProjectId = TargetId;
        //    else
        //        comment.TaskId = TargetId;

        //    db.Comments.Add(comment);
        //    db.SaveChanges();

        //    return Redirect(Request.UrlReferrer.ToString());
        //}

    }
}
