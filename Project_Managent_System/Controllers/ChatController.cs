using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Project_Managent_System.Models;
using Project_Managent_System.ViewModels;
using System.Data.Entity;

namespace Project_Managent_System.Controllers
{
    public class ChatController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        [HttpGet]
        public ActionResult GetThread(int id, string type)
        {
            try
            {
                if (Session["UserId"] == null)
                    return Content("<div class='alert alert-warning py-2 small'>Session expired.</div>");

                int currentUserId = Convert.ToInt32(Session["UserId"]);
                string userRole = Session["Role"]?.ToString();

                // Permission Check
                if (!UserHasAccess(id, type, currentUserId, userRole))
                {
                    return Content("<div class='p-4 text-center text-muted small'>" +
                                   "<i class='bi bi-shield-lock fs-2'></i><br>Access Denied.</div>");
                }

                var comments = db.Comments
                    .Where(c => (type == "Project" && c.ProjectId == id && c.TaskId == null) ||
                                (type == "Task" && c.TaskId == id))
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new CommentItem
                    {
                        UserId = c.UserId,
                        UserName = c.Main_Users.FirstName,
                        Role = c.Main_Users.Role,
                        Message = c.Message, // Fixed: Use DB spelling 'Messaage'
                        CreatedAt = c.CreatedAt
                    }).ToList();

                ViewBag.TargetId = id;
                ViewBag.ChatType = type;

                return PartialView("~/Views/Shared/_CommentThread.cshtml", comments);
            }
            catch (Exception ex)
            {
                return Content("Error: " + ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PostComment(int TargetId, string Message, string ChatType)
        {
            if (Session["UserId"] == null) return Content("Unauthorized");

            int currentUserId = Convert.ToInt32(Session["UserId"]);
            string userRole = Session["Role"]?.ToString();

            if (!UserHasAccess(TargetId, ChatType, currentUserId, userRole))
                return Content("Access Denied.");

            var comment = new Comment
            {
                UserId = currentUserId,
                Message = Message.Trim(), // Fixed: Use DB spelling 'Messaage'
                CreatedAt = DateTime.Now
            };

            if (ChatType == "Project")
            {
                comment.ProjectId = TargetId;
                // If TaskId is required (not nullable) in your DB, 
                // you must either make it nullable or provide a valid Task ID.
                comment.TaskId = null;
            }
            else
            {
                comment.TaskId = TargetId;
                var task = db.Tasks.Find(TargetId);
                if (task != null) comment.ProjectId = task.ProjectId;
            }

            try
            {
                db.Comments.Add(comment);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // This will show you exactly why the database rejected the save
                var inner = ex.InnerException?.InnerException?.Message ?? ex.Message;
                return Content("<div class='alert alert-danger small'>DB Error: " + inner + "</div>");
            }

            return GetThread(TargetId, ChatType);
        }

        private bool UserHasAccess(int id, string type, int userId, string role)
        {
            if (role == "Admin" || role == "Manager") return true;

            if (type == "Project")
            {
                return db.ProjectUsers.Any(pu => pu.ProjectId == id && pu.UserId == userId);
            }
            else if (type == "Task")
            {
                var task = db.Tasks.Include(t => t.Project.ProjectUsers)
                                   .Include(t => t.Task_Assignments)
                                   .FirstOrDefault(t => t.TaskId == id);

                if (task == null) return false;

                bool isTaskCreator = task.CreatedByManagerId == userId;
                bool isAssigned = task.Task_Assignments.Any(ta => ta.UserId == userId);
                bool isProjectMember = task.Project.ProjectUsers.Any(pu => pu.UserId == userId);

                return isTaskCreator || isAssigned || isProjectMember;
            }
            return false;
        }
    }
}