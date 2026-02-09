using System.Collections.Generic;
using System.Web.Mvc;

namespace Project_Managent_System.ViewModels
{
    public class TaskReAssignViewModel
    {
        // Selected task
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }

        // 🔹 Task dropdown (REQUIRED)
        public List<SelectListItem> Tasks { get; set; }

        // Previously assigned users (optional, informational)
        public List<int> AssignedUserIds { get; set; }

        // Final selected users after submit
        public List<int> SelectedUserIds { get; set; }

        // All available users (checkbox list)
        public List<SelectListItem> Users { get; set; }
    }
}
