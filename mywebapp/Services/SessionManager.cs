using Microsoft.AspNetCore.Http;

namespace mywebapp.Services
{
    public class SessionManager
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionManager(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetStudentSession(string studentId)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                session.SetString("StudentId", studentId);
                session.SetString("SessionStartTime", DateTime.Now.ToString("o"));
                Console.WriteLine($"New session started - Student ID: {studentId}, Session ID: {session.Id}");
            }
        }

        public string GetCurrentStudentId()
        {
            return _httpContextAccessor.HttpContext?.Session?.GetString("StudentId");
        }
    }
}