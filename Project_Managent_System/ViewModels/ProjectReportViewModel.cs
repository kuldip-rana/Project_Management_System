using System.Collections.Generic;

namespace Project_Managent_System.ViewModels
{
    public class ProjectReportViewModel
    {
        public string ProjectName { get; set; }
        public string Status { get; set; }
        public System.DateTime StartDate { get; set; }
        public System.DateTime EndDate { get; set; }
        public string ProjectMembers { get; set; }
        public List<TaskReportItem> Tasks { get; set; }
    }

    public class TaskReportItem
    {
        public string Title { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public System.DateTime DueDate { get; set; }
        public string AssigneeNames { get; set; } // Flattened names as a single string
    }
}