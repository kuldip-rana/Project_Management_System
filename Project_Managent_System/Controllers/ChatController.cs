using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Project_Managent_System.Models;
using Project_Managent_System.ViewModels;
using System.Data.Entity;
using Microsoft.AspNet.SignalR;

namespace Project_Managent_System.Controllers
{
    public class ChatController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        // ==========================================
        // 🔹 GET THREAD
        // ==========================================
        // GET: Chat/GetThread
        [HttpGet]
        public ActionResult GetThread(int id, string type)
        {
            try
            {
                var comments = db.Comments
                    .Where(c => (type == "Project" && c.ProjectId == id && c.TaskId == null) ||
                                (type == "Task" && c.TaskId == id))
                    .OrderBy(c => c.CreatedAt)
                    .ToList() // Fetch from DB first
                    .Select(c => new Project_Managent_System.ViewModels.CommentItem
                    {
                        UserId = c.UserId,
                        UserName = c.Main_Users.FirstName,
                        Role = c.Main_Users.Role,
                        Message = c.Message, // 💡 Maps DB 'Messaage' to ViewModel 'Message'
                        CreatedAt = c.CreatedAt
                    }).ToList();

                ViewBag.TargetId = id;
                ViewBag.ChatType = type;

                return PartialView("~/Views/Shared/_CommentThread.cshtml", comments);
            }
            catch (Exception ex)
            {
                return Content("<div class='alert alert-danger'>Error: " + ex.Message + "</div>");
            }
        }

        // ==========================================
        // 🔹 POST COMMENT
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PostComment(int TargetId, string Message, string ChatType)
        {
            if (Session["UserId"] == null)
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(Message))
                return Json(new { success = false, message = "Message content is required." });

            try
            {
                int currentUserId = Convert.ToInt32(Session["UserId"]);
                string userRole = Session["Role"]?.ToString();
                string userName = Session["UserName"]?.ToString() ?? "User"; // Ensure you have Name in Session

                // 🔒 Security Verification
                if (!UserHasAccess(TargetId, ChatType, currentUserId, userRole))
                    return Json(new { success = false, message = "You do not have access to this thread." });

                // 1. Prepare and Save to Database
                var comment = new Comment
                {
                    UserId = currentUserId,
                    Message = Message.Trim(),
                    CreatedAt = DateTime.Now
                };

                if (ChatType == "Project")
                {
                    comment.ProjectId = TargetId;
                    comment.TaskId = null;
                }
                else
                {
                    comment.TaskId = TargetId;
                    var task = db.Tasks.Find(TargetId);
                    if (task != null) comment.ProjectId = task.ProjectId;
                }

                db.Comments.Add(comment);
                db.SaveChanges();

                // 2. 🚀 BROADCAST VIA SIGNALR HUB
                // Get the Hub Context
                var hubContext = GlobalHost.ConnectionManager.GetHubContext<Project_Managent_System.Hubs.ChatHub>();

                // Define a unique group name for this specific chat (e.g., "Project_15" or "Task_22")
                string groupName = ChatType + "_" + TargetId;

                // Send to everyone in the group
                hubContext.Clients.Group(groupName).addNewMessageToPage(new
                {
                    UserId = currentUserId,
                    UserName = userName,
                    Role = userRole,
                    Message = comment.Message,
                    CreatedAt = comment.CreatedAt.ToString("hh:mm tt")
                });

                // 3. Return JSON Success (The client-side JS handles the rest)
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Database Error: " + inner });
            }
        }

        // ==========================================
        // 🔐 ACCESS CONTROL LOGIC
        // ==========================================
        private bool UserHasAccess(int id, string type, int userId, string role)
        {
            try
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

                    bool isAssigned = task.Task_Assignments.Any(ta => ta.UserId == userId);
                    bool isProjectMember = task.Project.ProjectUsers.Any(pu => pu.UserId == userId);

                    return isAssigned || isProjectMember;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}