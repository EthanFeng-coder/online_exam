using System.Text.Json;

namespace mywebapp.Models
{
    public static class Constants
    {
        public static string DefaultPassword { get; set; } = "ict212";
    }

    public class Student
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public List<int> CompletedGroups { get; set; } = new();
        public StudentProgress Progress { get; set; } = new();

        // Helper method to validate login - password should be just "ict212"
        public bool ValidateCredentials(string studentId, string password)
        {
            return Id == studentId && password == Constants.DefaultPassword;
        }
    }

    public class StudentProgress
    {
        public int CurrentGroupId { get; set; }
        public int CurrentQuestionIndex { get; set; }
        public List<Submission> Submissions { get; set; } = new();
    }

    public class Submission
    {
        public required string Code { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class StudentData
    {
        public List<Student> Students { get; set; } = new();
    }

    public static class StudentAuthentication
    {
        public static Student AuthenticateStudent(StudentData studentData, string studentId, string password)
        {
            return studentData.Students.FirstOrDefault(s => 
                s.ValidateCredentials(studentId, password));
        }
    }

    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static StudentData DeserializeStudentData(string jsonString)
        {
            return JsonSerializer.Deserialize<StudentData>(jsonString, _options);
        }

        public static string SerializeStudentData(StudentData studentData)
        {
            return JsonSerializer.Serialize(studentData, _options);
        }
    }
}