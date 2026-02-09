
using Project_Managent_System.Models;
using Project_Managent_System.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Project_Managent_System.Controllers
{
    public class TasksController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        // ===============================
        // 🔐 AUTH HELPERS
        // ===============================
        private bool IsManagerLoggedIn()
        {
            return Session["UserId"] != null &&
                   Session["Role"] != null &&
                   Session["Role"].ToString() == "Manager";
        }

        private ActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] = "Session expired. Please login again.";
            return RedirectToAction("Login", "Signup_Login");
        }

        //==================================
        // METHOD TO FETCH LIST OF PROJECTS
        //==================================
        private void PopulateProjects(string managerName, int? selectedProjectId = null)
        {
            // Logic matches AssignTask: Filter projects where the manager is a member
            var managerProjects = db.Projects
                                    .Where(p => p.Project_Members.Contains(managerName))
                                    .ToList();

            ViewBag.ProjectId = new SelectList(
                managerProjects,
                "ProjectId",
                "ProjectName",
                selectedProjectId
            );
        }

        //==================================
        //GET : CREATE TASK
        //==================================

        [HttpGet]
        public ActionResult CreateTask()
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            try
            {
                // Get name from session to match the Project_Members string
                string managerName = Session["Name"].ToString();
                int managerId = Convert.ToInt32(Session["UserId"]);

                PopulateProjects(managerName);

                var model = new Task
                {
                    CreatedByManagerId = managerId
                };

                return View(model);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Unable to load Create Task page.";
                return RedirectToAction("ManagerDashboard", "Manager");
            }
        }

        //==================================
        // POST : CREATE TASK
        //==================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTask(Task task)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            string managerName = Session["Name"].ToString();

            try
            {
                task.CreatedByManagerId = Convert.ToInt32(Session["UserId"]);
                task.CreatedAt = DateTime.Now;
                task.UpdatedAt = DateTime.Now;

                if (!ModelState.IsValid)
                {
                    // Re-populate with filtered list on validation failure
                    PopulateProjects(managerName, task.ProjectId);
                    return View(task);
                }

                db.Tasks.Add(task);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Task created successfully!";
                return RedirectToAction("ManagerDashboard", "Manager");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                PopulateProjects(managerName, task.ProjectId);
                return View(task);
            }
        }

        // ===============================
        // POST: Tasks/Delete/5
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            // Use Include to bring in related data if needed, or find the task
            var task = db.Tasks.Find(id);

            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("DisplayTaskList");
            }

            int managerId = Convert.ToInt32(Session["UserId"]);
            if (task.CreatedByManagerId != managerId)
            {
                TempData["ErrorMessage"] = "You are not authorized to delete this task.";
                return RedirectToAction("DisplayTaskList");
            }

            try
            {
                // 1. Remove related Comments first
                var relatedComments = db.Comments.Where(c => c.TaskId == id).ToList();
                if (relatedComments.Any())
                {
                    db.Comments.RemoveRange(relatedComments);
                }

                // 2. Remove related Task Assignments (to prevent FK issues there too)
                var relatedAssignments = db.Task_Assignments.Where(a => a.TaskId == id).ToList();
                if (relatedAssignments.Any())
                {
                    db.Task_Assignments.RemoveRange(relatedAssignments);
                }

                // 3. Finally, remove the Task itself
                db.Tasks.Remove(task);

                // Commit all changes in a single transaction
                db.SaveChanges();

                TempData["SuccessMessage"] = "Task and all related data deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error while deleting: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction("DisplayTaskList");
        }

        // ===============================
        // GET: Tasks/TaskList
        // ===============================
        [HttpGet]
        public ActionResult DisplayTaskList(int? projectId = null)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            try
            {
                int managerId = Convert.ToInt32(Session["UserId"]);

                var tasksQuery = db.Tasks
                    .Where(t => t.CreatedByManagerId == managerId);

                if (projectId.HasValue)
                    tasksQuery = tasksQuery.Where(t => t.ProjectId == projectId.Value);

                var tasksList = tasksQuery
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                ViewBag.ProjectId = new SelectList(
                    db.Projects.ToList(),
                    "ProjectId",
                    "ProjectName",
                    projectId
                );

                return View(tasksList);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    ex.InnerException?.InnerException?.Message ?? ex.Message;
                return RedirectToAction("ManagerDashboard", "Manager");
            }
        }

        // ===============================
        // GET: Tasks/EditTask/5
        // ===============================
        [HttpGet]
        public ActionResult EditTask(int id)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            int managerId = Convert.ToInt32(Session["UserId"]);

            var task = db.Tasks
                         .Include("Project")
                         .FirstOrDefault(t => t.TaskId == id);

            if (task == null || task.CreatedByManagerId != managerId)
            {
                TempData["ErrorMessage"] = "Unauthorized or task not found.";
                return RedirectToAction("DisplayTaskList");
            }

            ViewBag.ProjectName = task.Project?.ProjectName;

            ViewBag.PriorityList = new SelectList(
                new[] { "Low", "Medium", "High" },
                task.Priority
            );

            ViewBag.StatusList = new SelectList(
                new[] { "Pending", "In Progress", "Completed" },
                task.Status
            );

            return View(task);
        }

        // ===============================
        // POST: Tasks/EditTask
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTask(Task task)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            int managerId = Convert.ToInt32(Session["UserId"]);

            var existingTask = db.Tasks
                                 .Include("Project")
                                 .FirstOrDefault(t => t.TaskId == task.TaskId);

            if (existingTask == null || existingTask.CreatedByManagerId != managerId)
            {
                TempData["ErrorMessage"] = "Unauthorized update attempt.";
                return RedirectToAction("DisplayTaskList");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectName = existingTask.Project?.ProjectName;

                ViewBag.PriorityList = new SelectList(
                    new[] { "Low", "Medium", "High" },
                    task.Priority
                );

                ViewBag.StatusList = new SelectList(
                    new[] { "Pending", "In Progress", "Completed" },
                    task.Status
                );

                return View(existingTask);
            }

            existingTask.Priority = task.Priority;
            existingTask.Status = task.Status;
            existingTask.DueDate = task.DueDate;
            existingTask.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Task updated successfully!";
            return RedirectToAction("DisplayTaskList");
        }

        /* ===============================
           TASK REPORT 
        =============================== */

        [HttpGet]
        public ActionResult TaskReport(int id)
        {
            var task = db.Tasks
                         .Include(t => t.Main_Users)
                         .Include(t => t.Project)
                         .FirstOrDefault(t => t.TaskId == id);

            if (task == null)
                return HttpNotFound();

            return View(task);
        }

        /* ===============================
           TASK ASSIGN 
        =============================== */


        // GET: Tasks/AssignTask

        [HttpGet]
        public ActionResult AssignTask()
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            try
            {
                string managerName = Session["Name"].ToString();

                var vm = new TaskAssignViewModel
                {
                    // 🔹 Projects where manager is a MEMBER
                    Projects = db.Projects
                                 .Where(p => p.Project_Members.Contains(managerName))
                                 .Select(p => new SelectListItem
                                 {
                                     Value = p.ProjectId.ToString(),
                                     Text = p.ProjectName
                                 }).ToList(),

                    // 🔹 Tasks loaded dynamically
                    Tasks = new List<SelectListItem>(),

                    // 🔹 Assignable users
                    Users = db.Main_Users
                              .Where(u => u.Role == "User")
                              .Select(u => new SelectListItem
                              {
                                  Value = u.Id.ToString(),
                                  Text = u.FirstName + " " + u.LastName
                              }).ToList()
                };

                return View(vm);
            }
            catch
            {
                TempData["ErrorMessage"] = "Error loading Assign Task page.";
                return RedirectToAction("ManagerDashboard", "Manager");
            }
        }

        //  AJAX : TASK LOADER

        [HttpGet]
        public JsonResult GetTasksByProject(int projectId)
        {
            try
            {
                string managerName = Session["Name"].ToString();

                // 🔒 Authorization check
                bool hasAccess = db.Projects.Any(p =>
                    p.ProjectId == projectId &&
                    p.Project_Members.Contains(managerName));

                if (!hasAccess)
                    return Json(new { error = "Unauthorized access" },
                                JsonRequestBehavior.AllowGet);

                var tasks = db.Tasks
                              .Where(t => t.ProjectId == projectId)
                              .Select(t => new
                              {
                                  t.TaskId,
                                  t.TaskTitle
                              }).ToList();

                return Json(tasks, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message },
                            JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Tasks/AssignTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignTask(TaskAssignViewModel model)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            try
            {
                string managerName = Session["Name"].ToString();

                // 🔒 Validate project access via task
                var task = db.Tasks
                             .Join(db.Projects,
                                   t => t.ProjectId,
                                   p => p.ProjectId,
                                   (t, p) => new { Task = t, Project = p })
                             .FirstOrDefault(x =>
                                 x.Task.TaskId == model.TaskId &&
                                 x.Project.Project_Members.Contains(managerName))
                             ?.Task;

                if (task == null)
                    throw new Exception("Invalid or unauthorized task selection.");

                if (model.SelectedUserIds == null || !model.SelectedUserIds.Any())
                    throw new Exception("Please select at least one user.");

                foreach (var userId in model.SelectedUserIds)
                {
                    bool exists = db.Task_Assignments.Any(a =>
                        a.TaskId == task.TaskId &&
                        a.UserId == userId);

                    if (!exists)
                    {
                        db.Task_Assignments.Add(new Task_Assignments
                        {
                            TaskId = task.TaskId,
                            UserId = userId,
                            AssignedAt = DateTime.Now,
                            TaskStatus = "Pending"
                        });
                    }
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = "Task assigned successfully.";
                return RedirectToAction("AssignTask");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssignTask");
            }
        }
        // ===============================
        // GET: Tasks/ReAssignTask
        // ===============================
        [HttpGet]
        public ActionResult ReAssignTask(int? taskId)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            try
            {
                int managerId = Convert.ToInt32(Session["UserId"]);

                var vm = new TaskReAssignViewModel
                {
                    Tasks = new List<SelectListItem>(),
                    Users = new List<SelectListItem>(),
                    SelectedUserIds = new List<int>()
                };

                // 🔹 Load task dropdown (manager-created tasks only)
                vm.Tasks = db.Tasks
                             .Where(t => t.CreatedByManagerId == managerId)
                             .Select(t => new SelectListItem
                             {
                                 Value = t.TaskId.ToString(),
                                 Text = t.TaskTitle,
                                 Selected = (taskId.HasValue && t.TaskId == taskId.Value)
                             })
                             .ToList();

                // 🔹 No task selected → show only dropdown
                if (!taskId.HasValue)
                    return View(vm);

                // 🔹 Load selected task with assignments
                var task = db.Tasks
                             .Include(t => t.Task_Assignments)
                             .FirstOrDefault(t =>
                                 t.TaskId == taskId.Value &&
                                 t.CreatedByManagerId == managerId);

                if (task == null)
                {
                    TempData["ErrorMessage"] = "Invalid or unauthorized task selection.";
                    return RedirectToAction("ReAssignTask");
                }

                var assignedUserIds = task.Task_Assignments
                                          .Select(a => a.UserId)
                                          .ToList();

                vm.TaskId = task.TaskId;
                vm.TaskTitle = task.TaskTitle;
                vm.SelectedUserIds = assignedUserIds;

                // 🔹 Load users with pre-selection
                vm.Users = db.Main_Users
                             .Where(u => u.Role == "User")
                             .Select(u => new SelectListItem
                             {
                                 Value = u.Id.ToString(),
                                 Text = u.FirstName + " " + u.LastName,
                                 Selected = assignedUserIds.Contains(u.Id)
                             })
                             .ToList();

                return View(vm);
            }
            catch (Exception ex)
            {
                // 🔴 Log ex here if logging is enabled
                TempData["ErrorMessage"] = "Something went wrong while loading task details.";
                return RedirectToAction("ReAssignTask");
            }
        }

        // ===============================
        // POST: Tasks/ReAssignTask
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReAssignTask(TaskReAssignViewModel model)
        {
            if (!IsManagerLoggedIn())
                return RedirectToLogin();

            try
            {
                int managerId = Convert.ToInt32(Session["UserId"]);

                if (model == null || model.TaskId <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid task submission.";
                    return RedirectToAction("ReAssignTask");
                }

                var task = db.Tasks
                             .Include(t => t.Task_Assignments)
                             .FirstOrDefault(t =>
                                 t.TaskId == model.TaskId &&
                                 t.CreatedByManagerId == managerId);

                if (task == null)
                {
                    TempData["ErrorMessage"] = "Unauthorized task access.";
                    return RedirectToAction("ReAssignTask");
                }

                model.SelectedUserIds = model.SelectedUserIds ?? new List<int>();

                // 🔴 Remove unselected users
                var removeList = task.Task_Assignments
                                     .Where(a => !model.SelectedUserIds.Contains(a.UserId))
                                     .ToList();

                if (removeList.Any())
                    db.Task_Assignments.RemoveRange(removeList);

                // 🟢 Add newly selected users
                var existingUserIds = task.Task_Assignments
                                          .Select(a => a.UserId)
                                          .ToList();

                foreach (var userId in model.SelectedUserIds)
                {
                    if (!existingUserIds.Contains(userId))
                    {
                        db.Task_Assignments.Add(new Task_Assignments
                        {
                            TaskId = task.TaskId,
                            UserId = userId,
                            AssignedAt = DateTime.Now,
                            TaskStatus = "Pending"
                        });
                    }
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = "Task reassigned successfully!";
                return RedirectToAction("ReAssignTask", new { taskId = model.TaskId });
            }
            catch (Exception ex)
            {
                // 🔴 Log ex here
                TempData["ErrorMessage"] = "An error occurred while reassigning the task.";
                return RedirectToAction("ReAssignTask", new { taskId = model?.TaskId });
            }
        }

        /* ===============================
            💬 COMMENT METHODS
        =============================== */
        //[HttpGet]
        //public PartialViewResult GetTaskComments(int id)
        //{
        //    var comments = db.Comments
        //                     .Where(c => c.TaskId == id)
        //                     .OrderBy(c => c.CreatedAt)
        //                     .ToList();

        //    ViewBag.TargetId = id;
        //    ViewBag.ChatType = "Task";

        //    return PartialView("~/Views/Shared/_CommentThread.cshtml", comments);
        //}

        // Note: This uses the same logic as the Manager PostComment
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult PostComment(int TargetId, string ChatType, string Message) // Changed 'Messaage' to 'Message' to match the Form
        //{
        //    try
        //    {
        //        if (Session["UserId"] == null)
        //            return Json(new { success = false, message = "Session expired." });

        //        if (string.IsNullOrWhiteSpace(Message))
        //            return Json(new { success = false, message = "Message cannot be empty." });

        //        var task = db.Tasks.Find(TargetId);
        //        if (task == null)
        //            return Json(new { success = false, message = "Task not found." });

        //        var comment = new Comment
        //        {
        //            UserId = Convert.ToInt32(Session["UserId"]),
        //            Messaage = Message.Trim(), // DB field is 'Messaage', but input is 'Message'
        //            CreatedAt = DateTime.Now,
        //            TaskId = TargetId,
        //            ProjectId = task.ProjectId
        //        };

        //        db.Comments.Add(comment);
        //        db.SaveChanges();

        //        // RETURN JSON instead of Redirect for AJAX to work
        //        return Json(new { success = true });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = ex.Message });
        //    }
        //}

    }
}
