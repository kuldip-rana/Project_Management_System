using System;

namespace Project_Managent_System.Models.ViewModels
{
    public class CalendarEvent
    {
        public string id { get; set; }
        public string title { get; set; }

        // FullCalendar prefers ISO string
        public string start { get; set; }
        public string end { get; set; }

        public string color { get; set; }
        public string url { get; set; }
    }
}