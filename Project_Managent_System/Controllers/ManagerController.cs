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
            if (Session["UserId"] == null || Session["Role"] == null)
                return false;

            string role = Session["Role"].ToString();

            return role == "Manager" || role == "Admin";
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

            var tasksQuery = db.Tasks.Where(t => t.CreatedByManagerId == managerId);

            if (projectId.HasValue)
                tasksQuery = tasksQuery.Where(t => t.ProjectId == projectId);

            if (fromDate.HasValue)
                tasksQuery = tasksQuery.Where(t => t.CreatedAt >= fromDate);

            if (toDate.HasValue)
                tasksQuery = tasksQuery.Where(t => t.CreatedAt <= toDate);

            var taskList = tasksQuery.ToList();

            // Prepare data for Google Gantt Chart
            var ganttData = taskList.Select(t => new
            {
                TaskId = t.TaskId.ToString(),
                TaskName = t.TaskTitle,
                Resource = t.Project.ProjectName,
                
                StartDate = t.CreatedAt.ToString("yyyy-MM-dd"),
                EndDate = (t.DueDate != default(DateTime))
               ? t.DueDate.ToString("yyyy-MM-dd")
               : t.CreatedAt.AddDays(7).ToString("yyyy-MM-dd"),
                PercentDone = t.Status == "Completed" ? 100 : (t.Status == "In Progress" ? 50 : 0)
            }).ToList();

            return Json(new
            {
                completed = taskList.Count(t => t.Status == "Completed"),
                pending = taskList.Count(t => t.Status == "Pending"),
                inProgress = taskList.Count(t => t.Status == "In Progress"),
                projectNames = taskList.GroupBy(t => t.Project.ProjectName).Select(g => g.Key).ToList(),
                tasksPerProject = taskList.GroupBy(t => t.Project.ProjectName).Select(g => g.Count()).ToList(),
                ganttData = ganttData
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
        public ActionResult CreateProject(Project project, List<int> SelectedUserIds, HttpPostedFileBase ProjectDocument)
        {
            try
            {
                if (!IsManagerLoggedIn())
                    return RedirectToAction("Login", "Signup_Login");

                // Repopulate ViewBag for the View in case of validation failure
                ViewBag.Users = new MultiSelectList(db.Main_Users.ToList(), "Id", "FirstName", SelectedUserIds);

                project.Project_Members = Convert.ToString(Session["Name"]);
                project.Status = "Pending";

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

                    string extension = Path.GetExtension(ProjectDocument.FileName).ToLower();
                    if (extension != ".pdf")
                    {
                        ModelState.AddModelError("", "Only PDF files are allowed.");
                        return View(project);
                    }

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
                        db.ProjectUsers.Add(new ProjectUser
                        {
                            ProjectId = project.ProjectId,
                            UserId = userId
                        });
                    }
                    db.SaveChanges();
                }

                TempData["SuccessMessage"] = "Project created successfully.";

                // --- DYNAMIC REDIRECTION LOGIC ---
                if (Session["Role"]?.ToString() == "Admin")
                {
                    return RedirectToAction("ManageProjects", "Admin");
                }

                return RedirectToAction("ManagerDashboard");
            }
            catch (Exception ex)
            {
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
        // GET: Admin/DeleteProject/5
        [HttpGet]
        public ActionResult DeleteProject(int? id)
        {
            // Authorization check for Admin or Manager
            if (Session["Role"]?.ToString() != "Admin" && Session["Role"]?.ToString() != "Manager")
                return RedirectToAction("Login", "Signup_Login");

            if (id == null) return RedirectToAction("ManageProjects", "Admin");

            var project = db.Projects.Find(id);
            if (project == null) return HttpNotFound();

            // Generate Math Challenge
            Random rand = new Random();
            int num1 = rand.Next(1, 10);
            int num2 = rand.Next(1, 10);

            // Store correct answer in Session for verification
            Session["ProjectDeleteCaptcha"] = num1 + num2;

            ViewBag.Num1 = num1;
            ViewBag.Num2 = num2;

            return View(project);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProject(int id, int captchaAnswer)
        {
            // 1. Authorization check
            if (!IsManagerLoggedIn())
                return RedirectToAction("Login", "Signup_Login");

            // 2. Verify Math Challenge Answer
            int? correctAnswer = Session["ProjectDeleteCaptcha"] as int?;

            // Check if the answer is wrong
            if (correctAnswer == null || captchaAnswer != correctAnswer)
            {
                TempData["ErrorMessage"] = "Incorrect math answer. Deletion cancelled for security.";
                // IMPORTANT: We return immediately here to stop the deletion logic from running
                return RedirectToAction("DeleteProject", new { id = id });
            }

            // 3. Find the project
            var project = db.Projects.FirstOrDefault(p => p.ProjectId == id);
            if (project == null) return HttpNotFound();

            try
            {
                // --- START CASCADING DELETE LOGIC ---
                // This only runs if the captcha check above was successful

                var projectTasksIds = db.Tasks.Where(t => t.ProjectId == id).Select(t => t.TaskId).ToList();

                // Remove related Comments
                var relatedComments = db.Comments.Where(c => c.ProjectId == id ||
                    (c.TaskId.HasValue && projectTasksIds.Contains(c.TaskId.Value))).ToList();
                if (relatedComments.Any()) db.Comments.RemoveRange(relatedComments);

                // Remove Task Assignments
                var relatedAssignments = db.Task_Assignments.Where(ta => projectTasksIds.Contains(ta.TaskId)).ToList();
                if (relatedAssignments.Any()) db.Task_Assignments.RemoveRange(relatedAssignments);

                // Remove ProjectUsers
                var projectUsers = db.ProjectUsers.Where(pu => pu.ProjectId == id).ToList();
                if (projectUsers.Any()) db.ProjectUsers.RemoveRange(projectUsers);

                // Remove Tasks
                var relatedTasks = db.Tasks.Where(t => t.ProjectId == id).ToList();
                if (relatedTasks.Any()) db.Tasks.RemoveRange(relatedTasks);

                // Delete Associated Files
                if (!string.IsNullOrEmpty(project.Project_Document))
                {
                    string filePath = Server.MapPath("~/Uploads/Projects/" + project.Project_Document);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }

                // 4. Finally remove the Project
                db.Projects.Remove(project);
                db.SaveChanges();
                // --- END CASCADING DELETE LOGIC ---

                // 5. Clear captcha session
                Session["ProjectDeleteCaptcha"] = null;

                TempData["SuccessMessage"] = "Project and all related data deleted successfully.";

                // 6. Redirect based on role
                if (Session["Role"]?.ToString() == "Admin")
                {
                    return RedirectToAction("ManageProjects", "Admin");
                }
                else
                {
                    return RedirectToAction("DisplayProjects", "Manager");
                }
            }
            catch (Exception ex)
            {
                string innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                TempData["ErrorMessage"] = "Database Error: " + innerMsg;
                return RedirectToAction("DeleteProject", new { id = id });
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
                if (Session["Role"]?.ToString() == "Admin")
                {
                    return RedirectToAction("ManageProjects", "Admin");
                }
                else
                {
                    return RedirectToAction("DisplayProjects", "Manager");
                }
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
