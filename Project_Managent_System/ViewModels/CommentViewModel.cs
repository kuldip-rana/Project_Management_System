using System;
using System.Collections.Generic;

namespace Project_Managent_System.ViewModels
{
    public class CommentViewModel
    {
        public int? ProjectId { get; set; }
        public int? TaskId { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; }
        public string ChatType { get; set; } // Add this: "Project" or "Task"
        public List<CommentItem> Comments { get; set; }
    }

    public class CommentItem
    {
        public int UserId { get; set; } // Good to have for debugging
        public string UserName { get; set; }
        public string Role { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}