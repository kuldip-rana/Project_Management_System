using System.Collections.Generic;
using System.Web.Mvc;
using Project_Managent_System.Models;

namespace Project_Managent_System.ViewModels
{
    public class TaskAssignViewModel
    {
        // Selected task
        public int TaskId { get; set; }
        public int ProjectId { get; set; }

        // Selected users (multiple)
        public List<int> SelectedUserIds { get; set; }

        // Dropdown data
        public List<SelectListItem> Tasks { get; set; }
        public List<SelectListItem> Users { get; set; }
        public List<SelectListItem> Projects { get; set; }
    }
}
