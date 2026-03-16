using Project_Managent_System.Models;
using Project_Managent_System.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Project_Managent_System.Controllers
{
    public class CalenderController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        public ActionResult Calendar()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Signup_Login");

            return View();
        }

        [HttpGet]
        public JsonResult GetEvents()
        {
            try
            {
                // 1. Fetch Projects
                var projects = db.Projects.ToList();
                var projectEvents = projects.Select(p => new {
                    id = "p_" + p.ProjectId,
                    title = "📁 " + p.ProjectName,
                    start = p.StartDate.ToString("yyyy-MM-dd"),
                    // Fix: Since EndDate isn't nullable, we just use it directly. 
                    // If you want a safety check, compare it against DateTime.MinValue
                    end = p.EndDate.ToString("yyyy-MM-dd"),
                    color = "#1e1b4b",
                    url = Url.Action("Details", "Projects", new { id = p.ProjectId }),
                    allDay = true
                }).ToList();

                // 2. Fetch Tasks
                var tasks = db.Tasks.ToList();
                var taskEvents = tasks.Select(t => new {
                    id = "t_" + t.TaskId,
                    title = "📝 " + t.TaskTitle,
                    start = t.StartDate.ToString("yyyy-MM-dd"),
                    // Fix: Using DueDate directly as it is a non-nullable DateTime
                    end = t.DueDate.ToString("yyyy-MM-dd"),
                    color = t.Priority == "High" ? "#ef4444" : "#3b82f6",
                    url = Url.Action("Details", "Tasks", new { id = t.TaskId }),
                    allDay = true
                }).ToList();

                var result = projectEvents.Cast<object>().Concat(taskEvents).ToList();
                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}