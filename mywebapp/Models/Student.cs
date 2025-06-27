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

        public void AddSubmission(int groupId, int questionIndex, string code)
        {
            try
            {
                Console.WriteLine($"Adding submission for student {Id}");
                Console.WriteLine($"Group: {groupId}, Question: {questionIndex}");
                
                Progress.Submissions.Add(new Submission
                {
                    GroupId = groupId,
                    QuestionIndex = questionIndex,
                    Code = code,
                    SubmittedAt = DateTime.UtcNow
                });

                Console.WriteLine($"Submission added successfully");
                Console.WriteLine($"Total submissions for student: {Progress.Submissions.Count}");

                // Update progress
                Progress.CurrentGroupId = groupId;
                Progress.CurrentQuestionIndex = questionIndex + 1;
                
                Console.WriteLine($"Updated progress - Next Group: {Progress.CurrentGroupId}, Next Question: {Progress.CurrentQuestionIndex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding submission: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public bool HasCompletedQuestion(int groupId, int questionIndex)
        {
            return Progress.Submissions.Any(s => 
                s.GroupId == groupId && s.QuestionIndex == questionIndex);
        }
    }

    public class StudentProgress
    {
        public int CurrentGroupId { get; set; } = 1;
        public int CurrentQuestionIndex { get; set; } = 0;
        public List<Submission> Submissions { get; set; } = new();
        public Submission? AutoSave { get; set; }
        public bool Done { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
    
    public class Submission
    {
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public required string Code { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class StudentData
    {
        public List<Student> Students { get; set; } = new();

        public static StudentData LoadFromFile(string path)
        {
            if (!File.Exists(path))
                return new StudentData();
            
            var json = File.ReadAllText(path);
            return JsonHelper.DeserializeStudentData(json) ?? new StudentData();
        }
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

        public static StudentData? DeserializeStudentData(string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<StudentData>(jsonString, _options) 
                    ?? new StudentData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing student data: {ex.Message}");
                return new StudentData();
            }
        }

        public static string SerializeStudentData(StudentData studentData)
        {
            return JsonSerializer.Serialize(studentData, _options);
        }
    }
}