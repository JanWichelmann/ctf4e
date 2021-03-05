using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels
{
    public class AdminLessonExecution
    {
        public LessonExecution LessonExecution { get; set; }

        public int SlotId { get; set; }

        public bool OverrideExisting { get; set; }
    }
}