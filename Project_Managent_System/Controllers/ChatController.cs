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

        // GET: Fetch conversation
        [HttpGet]
        public ActionResult GetThread(int id, string type)
        {
            var commentsQuery = db.Comments.Include(c => c.Main_Users);

            // Filter based on whether we are in a Project or Task drawer
            if (type == "Project")
                commentsQuery = commentsQuery.Where(c => c.ProjectId == id);
            else
                commentsQuery = commentsQuery.Where(c => c.TaskId == id);

            var comments = commentsQuery
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentItem
                {
                    UserName = c.Main_Users.FirstName,
                    Role = c.Main_Users.Role,
                    Message = c.Messaage, // Matching your DB spelling 'Messaage'
                    CreatedAt = c.CreatedAt
                }).ToList();

            ViewBag.TargetId = id;
            ViewBag.ChatType = type;

            return PartialView("~/Views/Shared/_CommentThread.cshtml", comments);
        }

        // POST: Save message
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PostComment(int TargetId, string Message, string ChatType)
        {
            try
            {
                // 1. Validation check
                if (string.IsNullOrWhiteSpace(Message)) return Json(new { success = false });

                var comment = new Comment
                {
                    UserId = Convert.ToInt32(Session["UserId"]),
                    Messaage = Message.Trim(), // Matches your DB spelling 'Messaage'
                    CreatedAt = DateTime.Now
                };

                if (ChatType == "Project")
                {
                    comment.ProjectId = TargetId;
                    // FIX: If TaskId is NOT NULL in your DB, we must provide a value.
                    // Option A: Find the first task in this project to link it to
                    var linkedTask = db.Tasks.FirstOrDefault(t => t.ProjectId == TargetId);
                    if (linkedTask != null)
                    {
                        comment.TaskId = linkedTask.TaskId;
                    }
                    else
                    {
                        // Option B: If no tasks exist, this will still fail unless 
                        // you have a 'Global' task with ID 1 or make the column NULLABLE.
                        comment.TaskId = 1;
                    }
                }
                else
                {
                    comment.TaskId = TargetId;
                    var task = db.Tasks.Find(TargetId);
                    if (task != null) comment.ProjectId = task.ProjectId;
                }

                db.Comments.Add(comment);
                db.SaveChanges(); // This is where the Error 500 usually happens

                return GetThread(TargetId, ChatType);
            }
            catch (Exception ex)
            {
                // This will send the actual error message to the drawer instead of just '500'
                return Content("<div class='alert alert-danger small'>" + ex.Message + "</div>");
            }
        }
    }
}