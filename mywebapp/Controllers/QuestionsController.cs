using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using mywebapp.Models;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.DataProtection;

namespace mywebapp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionsController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly List<QuestionGroup> _questionGroups;
        private const string QuestionsPath = "Data/questions.json";

        private readonly IDataProtectionProvider _dataProtectionProvider;

        public QuestionsController(IWebHostEnvironment environment, IDataProtectionProvider dataProtectionProvider)
        {
            _environment = environment;
            _dataProtectionProvider = dataProtectionProvider;
            _questionGroups = LoadQuestions();
        }

        private readonly Dictionary<string, StudentProgress> _studentProgress = new();

        private List<QuestionGroup> LoadQuestions()
        {
            try
            {
                var jsonPath = Path.Combine(_environment.ContentRootPath, QuestionsPath);
                Console.WriteLine($"Loading questions from: {jsonPath}");

                if (!System.IO.File.Exists(jsonPath))
                {
                    throw new FileNotFoundException($"Questions file not found at: {jsonPath}");
                }

                var jsonString = System.IO.File.ReadAllText(jsonPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var data = JsonSerializer.Deserialize<QuestionData>(jsonString, options);
                var questionGroups = data?.QuestionGroups ?? new List<QuestionGroup>();
                
                Console.WriteLine($"Successfully loaded {questionGroups.Count} question groups");
                return questionGroups;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading questions: {ex.Message}");
                return new List<QuestionGroup>();
            }
        }

        private string ReplaceWithStudentNumbers(string text, string studentId)
        {
            try
            {
                // Add detailed logging
                Console.WriteLine($"Replacing numbers for student ID: {studentId}");
                Console.WriteLine($"Original text: {text}");

                if (string.IsNullOrEmpty(text)) 
                {
                    Console.WriteLine("Text is empty or null");
                    return text;
                }
                if (string.IsNullOrEmpty(studentId) || studentId.Length < 6) 
                {
                    Console.WriteLine($"Invalid student ID: {studentId}");
                    return text;
                }

                // Safely get student number segments with bounds checking
                string onetothree = studentId.Substring(1, 3);
                string twotofour = studentId.Substring(2, 4);

                // Create replacement dictionary with more placeholder formats
                var replacements = new Dictionary<string, string>();
                
                if (!string.IsNullOrEmpty(onetothree))
                {
                    replacements.Add("[1,3]", onetothree);
                }
                if (!string.IsNullOrEmpty(twotofour))
                {
                    replacements.Add("[2,4]", twotofour);
                }

                string modifiedText = text;
                foreach (var pair in replacements)
                {
                    if (modifiedText.Contains(pair.Key))
                    {
                        modifiedText = modifiedText.Replace(pair.Key, pair.Value);
                        Console.WriteLine($"Replaced {pair.Key} with {pair.Value}");
                    }
                }

                Console.WriteLine($"Modified text: {modifiedText}");
                return modifiedText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error replacing numbers: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return text;
            }
        }

        private string GetOrCreateQuestionText(string originalText, string studentId, int groupId, int questionIndex)
        {
            // Simply return the modified text without session storage
            return ReplaceWithStudentNumbers(originalText, studentId);
        }

        [HttpGet("groups/{groupId}/questions/{questionIndex}")]
        public ActionResult<Question> GetQuestionFromGroup(int groupId, int questionIndex, [FromQuery] string studentId)
        {
            try
            {
                Console.WriteLine($"Retrieved student ID from query: {studentId}");

                if (string.IsNullOrEmpty(studentId))
                {
                    Console.WriteLine("No student ID found in query parameters");
                    return BadRequest(new { error = "Student ID is required" });
                }

                // Load student data to check authorization
                var studentsPath = Path.Combine(_environment.ContentRootPath, "Data/students.json");
                var jsonString = System.IO.File.ReadAllText(studentsPath);
                var studentData = JsonSerializer.Deserialize<StudentData>(jsonString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var student = studentData?.Students.FirstOrDefault(s => s.Id == studentId);
                if (student == null)
                {
                    return NotFound("Student not found");
                }

                // Check if student has already completed their exam
                if (student.Progress.Done)
                {
                    Console.WriteLine($"Student {studentId} attempted to access questions after completion");
                    return StatusCode(StatusCodes.Status403Forbidden, new 
                    { 
                        error = "Exam completed",
                        message = "You have already completed your exam",
                        completedAt = student.Progress.CompletedAt
                    });
                }

                // Check if student is authorized for this group
                if (groupId != student.Progress.CurrentGroupId)
                {
                    Console.WriteLine($"Unauthorized access attempt - Student: {studentId}, Attempted Group: {groupId}, Current Group: {student.Progress.CurrentGroupId}");
                    return StatusCode(StatusCodes.Status401Unauthorized, new 
                    { 
                        error = "Unauthorized access",
                        message = "You are not authorized to access this group",
                        currentGroup = student.Progress.CurrentGroupId
                    });
                }

                var group = _questionGroups.FirstOrDefault(g => g.Id == groupId);
                if (group == null)
                {
                    Console.WriteLine($"Group {groupId} not found");
                    return NotFound($"Question group {groupId} not found");
                }

                var question = group.GetQuestion(questionIndex);
                if (question == null)
                {
                    Console.WriteLine($"Question {questionIndex} not found");
                    return NotFound($"Question {questionIndex} not found");
                }

                // Get question with previous submission's code
                var questionWithPreviousCode = GetQuestionWithInitialCode(question, studentId, groupId, questionIndex);

                // Replace placeholders in description
                questionWithPreviousCode.Description = ReplaceWithStudentNumbers(questionWithPreviousCode.Description, studentId);

                return Ok(questionWithPreviousCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("groups")]
        public ActionResult<IEnumerable<QuestionGroup>> GetQuestionGroups()
        {
            return Ok(_questionGroups);
        }

        [HttpPost("submit")]
        public ActionResult SubmitAnswer([FromBody] CodeSubmission submission)
        {
            try
            {
                Console.WriteLine($"Processing submission for student: {submission.StudentId}");
                
                var studentsPath = Path.Combine(_environment.ContentRootPath, "Data/students.json");
                Console.WriteLine($"Loading students data from: {studentsPath}");

                if (!System.IO.File.Exists(studentsPath))
                {
                    Console.WriteLine("Error: students.json not found");
                    return StatusCode(500, "Student data file not found");
                }

                // Read and parse the JSON file
                var jsonString = System.IO.File.ReadAllText(studentsPath);
                var studentData = JsonSerializer.Deserialize<StudentData>(jsonString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Failed to load student data");

                // Find the student
                var student = studentData.Students.FirstOrDefault(s => s.Id == submission.StudentId);
                if (student == null)
                {
                    Console.WriteLine($"Student not found: {submission.StudentId}");
                    return NotFound("Student not found");
                }

                // Add new submission
                var newSubmission = new Submission
                {
                    GroupId = submission.GroupId,
                    QuestionIndex = submission.QuestionIndex,
                    Code = submission.Code,
                    SubmittedAt = DateTime.UtcNow
                };
                student.Progress.Submissions.Add(newSubmission);

                // Check for duplicate submissions and remove older one
                var submissions = student.Progress.Submissions
                    .Where(s => s.QuestionIndex == submission.QuestionIndex)
                    .OrderByDescending(s => s.SubmittedAt)
                    .ToList();

                if (submissions.Count > 1)
                {
                    Console.WriteLine($"Found multiple submissions for question {submission.QuestionIndex}, removing older one");
                    var oldSubmission = submissions.Last();
                    student.Progress.Submissions.Remove(oldSubmission);
                }

                // Update current position
                student.Progress.CurrentGroupId = submission.GroupId;
                student.Progress.CurrentQuestionIndex = submission.QuestionIndex + 1;

                // Check if group is completed
                var group = _questionGroups.FirstOrDefault(g => g.Id == submission.GroupId);
                if (group != null && submission.QuestionIndex >= group.Questions.Count - 1)
                {
                    if (!student.CompletedGroups.Contains(submission.GroupId))
                    {
                        student.CompletedGroups.Add(submission.GroupId);
                    }
                    
                    // Mark as done since there are no more questions
                    student.Progress.CurrentGroupId = submission.GroupId;
                    student.Progress.CurrentQuestionIndex = submission.QuestionIndex;
                    student.Progress.Done = true;
                    student.Progress.CompletedAt = DateTime.UtcNow;

                    // Save the updated JSON back to file
                    var Doneoptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    
                    var DoneupdatedJson = JsonSerializer.Serialize(studentData, Doneoptions);
                    System.IO.File.WriteAllText(studentsPath, DoneupdatedJson);
                    
                    Console.WriteLine($"Student {submission.StudentId} has completed all questions");

                    return Ok(new
                    {
                        message = "All questions completed",
                        nextGroup = student.Progress.CurrentGroupId,
                        nextQuestion = student.Progress.CurrentQuestionIndex,
                        isCompleted = true,
                        completedAt = student.Progress.CompletedAt
                    });
                }

                // Save the updated JSON back to file
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var updatedJson = JsonSerializer.Serialize(studentData, options);
                System.IO.File.WriteAllText(studentsPath, updatedJson);
                
                Console.WriteLine("Successfully updated student progress");

                return Ok(new
                {
                    message = "Submission saved successfully",
                    nextGroup = student.Progress.CurrentGroupId,  // Will stay the same
                    nextQuestion = student.Progress.CurrentQuestionIndex,
                    isCompleted = student.Progress.Done
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing submission: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, "Failed to process submission");
            }
        }

        // Add new method for auto-save
        [HttpPost("autosave/{studentId}")]
        public async Task<IActionResult> AutoSave(string studentId, [FromBody] AutoSaveRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(studentId))
                {
                    return BadRequest("Student ID is required");
                }

                // Read and parse the JSON file with proper options
                var jsonFilePath = Path.Combine(_environment.ContentRootPath, "Data", "students.json");
                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonFilePath);
                var data = JsonSerializer.Deserialize<StudentData>(jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Failed to load student data");

                // Debug log the student data
                // Console.WriteLine($"Looking for student with ID: {studentId}");
                // Console.WriteLine("Available students in data:");
                // foreach (var s in data.Students)
                // {
                //     Console.WriteLine($"Student ID: {s.Id}, Name: {s.Name}");
                // }

                var student = data.Students.FirstOrDefault(s => s.Id == studentId);
                //Console.WriteLine($"Comparison result - Looking for: {studentId}, Found: {student?.Id ?? "not found"}");

                if (student == null)
                {
                    return NotFound($"Student {studentId} not found");
                }

                // Create autosave entry using same structure as submission
                student.Progress.AutoSave = new Submission
                {
                    GroupId = request.GroupId,
                    QuestionIndex = request.QuestionIndex,
                    Code = request.Code,
                    SubmittedAt = DateTime.UtcNow
                };

                // Save back to file
                await System.IO.File.WriteAllTextAsync(jsonFilePath, 
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AutoSave: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Add method to retrieve auto-saved code
        private string GetAutoSavedCode(string studentId, int groupId, int questionIndex)
        {
            var autosaveKey = $"autosave_{studentId}_{groupId}_{questionIndex}";
            var savedJson = HttpContext.Session.GetString(autosaveKey);
            
            if (!string.IsNullOrEmpty(savedJson))
            {
                try
                {
                    var saveData = JsonSerializer.Deserialize<AutoSaveData>(savedJson);
                    if ((DateTime.Now - saveData.LastSaved).TotalMinutes <= 1)
                    {
                        return saveData.Code;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving auto-saved code: {ex.Message}");
                }
            }
            return null;
        }

        private Question GetQuestionWithInitialCode(Question originalQuestion, string studentId, int groupId, int questionIndex)
        {
            try
            {
                // If it's the first question, return original
                if (questionIndex == 0)
                {
                    return originalQuestion;
                }

                // Load students.json to get previous submission
                var studentsPath = Path.Combine(_environment.ContentRootPath, "Data/students.json");
                Console.WriteLine($"Loading students data from: {studentsPath}");

                if (!System.IO.File.Exists(studentsPath))
                {
                    Console.WriteLine("Students.json not found, using default code");
                    return originalQuestion;
                }

                var jsonString = System.IO.File.ReadAllText(studentsPath);
                var studentData = JsonSerializer.Deserialize<StudentData>(jsonString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (studentData == null)
                {
                    Console.WriteLine("Failed to load student data, using default code");
                    return originalQuestion;
                }

                // Find student's previous question submission
                var student = studentData.Students.FirstOrDefault(s => s.Id == studentId);
                if (student == null)
                {
                    Console.WriteLine($"Student {studentId} not found, using default code");
                    return originalQuestion;
                }

                // Get the last submission from the previous question
                var previousSubmission = student.Progress.Submissions
                    .Where(s => s.GroupId == groupId && s.QuestionIndex == (questionIndex - 1))
                    .OrderByDescending(s => s.SubmittedAt)
                    .FirstOrDefault();

                if (previousSubmission == null)
                {
                    Console.WriteLine("No previous submission found, using code from question 0");
                    
                    // Get submission from question 0
                    var question0Submission = student.Progress.Submissions
                        .Where(s => s.GroupId == groupId && s.QuestionIndex == 0)
                        .OrderByDescending(s => s.SubmittedAt)
                        .FirstOrDefault();

                    if (question0Submission != null)
                    {
                        Console.WriteLine("Found submission from question 0, using that code");
                        return new Question
                        {
                            Id = originalQuestion.Id,
                            Title = originalQuestion.Title,
                            Description = originalQuestion.Description,
                            InitialCode = question0Submission.Code,
                            Difficulty = originalQuestion.Difficulty
                        };
                    }
                    
                    Console.WriteLine("No submission found for question 0, using default code");
                    return originalQuestion;
                }

                Console.WriteLine($"Found previous submission for question {questionIndex - 1}");

                // Create a new question with the previous submission's code
                return new Question
                {
                    Id = originalQuestion.Id,
                    Title = originalQuestion.Title,
                    Description = originalQuestion.Description,
                    InitialCode = previousSubmission.Code,
                    Difficulty = originalQuestion.Difficulty
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading previous code: {ex.Message}");
                return originalQuestion;
            }
        }

        public class AutoSaveData
        {
            public string Code { get; set; }
            public DateTime LastSaved { get; set; }
        }

        [HttpPost("markAsDone")]
        public async Task<IActionResult> MarkAsDone([FromBody] StudentCompletion completion)
        {
            try
            {
                var jsonFilePath = Path.Combine(_environment.ContentRootPath, "Data", "students.json");
                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonFilePath);
                var data = JsonSerializer.Deserialize<StudentData>(jsonContent);

                var student = data?.Students.FirstOrDefault(s => s.Id == completion.StudentId);
                if (student == null)
                {
                    return NotFound($"Student {completion.StudentId} not found");
                }

                // Mark the student as done
                student.Progress.Done = true;
                student.Progress.CompletedAt = completion.CompletedAt;

                // Save back to file
                await System.IO.File.WriteAllTextAsync(jsonFilePath, 
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
    public class QuestionData
    {
        public List<QuestionGroup> QuestionGroups { get; set; }
    }

    public class StudentProgress
    {
        public string StudentId { get; set; }
        public int CurrentGroup { get; set; }
        public int CurrentQuestion { get; set; }
        public Dictionary<string, bool> CompletedQuestions { get; set; } = new();
    }

    public class CodeSubmission
    {
        [Required(ErrorMessage = "Student ID is required")]
        public string StudentId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Group ID is required")]
        public int GroupId { get; set; }

        [Required(ErrorMessage = "Question Index is required")]
        public int QuestionIndex { get; set; }

        [Required(ErrorMessage = "Code submission is required")]
        public string Code { get; set; } = string.Empty;
    }

    public class AutoSaveRequest
    {
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class StudentCompletion
    {
        public string StudentId { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
    }
}