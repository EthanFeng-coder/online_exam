using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using mywebapp.Models;
using Microsoft.AspNetCore.Http;

namespace mywebapp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionsController : ControllerBase
    {
        private readonly List<QuestionGroup> _questionGroups;
        private const string QuestionsPath = "Data/questions.json";

        // Add these fields at the top of the controller
        private readonly Dictionary<string, StudentProgress> _studentProgress = new();

        public QuestionsController(IWebHostEnvironment webHostEnvironment)
        {
            try
            {
                var jsonPath = Path.Combine(webHostEnvironment.ContentRootPath, QuestionsPath);
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
                _questionGroups = data?.QuestionGroups ?? new List<QuestionGroup>();
                
                Console.WriteLine($"Successfully loaded {_questionGroups.Count} question groups");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading questions: {ex.Message}");
                _questionGroups = new List<QuestionGroup>();
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
                // Check studentId from query parameter instead of session
                Console.WriteLine($"Retrieved student ID from query: {studentId}");

                if (string.IsNullOrEmpty(studentId))
                {
                    Console.WriteLine("No student ID found in query parameters");
                    return BadRequest(new { error = "Student ID is required" });
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

                // Create a copy with replaced values
                var questionCopy = new Question
                {
                    Id = question.Id,
                    Title = question.Title,
                    Description = ReplaceWithStudentNumbers(question.Description, studentId),
                    InitialCode = ReplaceWithStudentNumbers(question.InitialCode, studentId),
                    TestCases = question.TestCases?.ToList() ?? new List<string>()
                };

                return Ok(questionCopy);
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
                if (string.IsNullOrEmpty(submission.StudentId))
                {
                    return BadRequest("No student ID provided");
                }

                var group = _questionGroups.FirstOrDefault(g => g.Id == submission.GroupId);
                if (group == null)
                {
                    return NotFound("Question group not found");
                }

                if (!group.HasQuestion(submission.QuestionIndex))
                {
                    return NotFound("Question not found");
                }

                // Update student progress
                if (!_studentProgress.ContainsKey(submission.StudentId))
                {
                    _studentProgress[submission.StudentId] = new StudentProgress 
                    { 
                        StudentId = submission.StudentId,
                        CurrentGroup = submission.GroupId,
                        CurrentQuestion = submission.QuestionIndex
                    };
                }

                var progress = _studentProgress[submission.StudentId];
                string questionKey = $"{submission.GroupId}_{submission.QuestionIndex}";
                progress.CompletedQuestions[questionKey] = true;

                // Find next question
                var nextQuestionIndex = submission.QuestionIndex + 1;
                var nextGroup = submission.GroupId;
                
                if (!group.HasQuestion(nextQuestionIndex))
                {
                    // Move to next group if available
                    nextQuestionIndex = 0;
                    nextGroup++;
                }

                // Store submission
                var submissionKey = $"submission_{submission.StudentId}_{submission.GroupId}_{submission.QuestionIndex}";
                HttpContext.Session.SetString(submissionKey, submission.Code);

                // Update current position
                progress.CurrentGroup = nextGroup;
                progress.CurrentQuestion = nextQuestionIndex;

                return Ok(new { 
                    message = "Solution submitted successfully",
                    nextGroup = nextGroup,
                    nextQuestion = nextQuestionIndex,
                    progress = progress.CompletedQuestions.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in submission: {ex.Message}");
                return StatusCode(500, "Failed to submit solution");
            }
        }

        // Add new method for auto-save
        [HttpPost("autosave")]
        public ActionResult AutoSaveCode([FromBody] CodeSubmission submission)
        {
            try
            {
                var studentId = HttpContext.Session.GetString("StudentId");
                if (string.IsNullOrEmpty(studentId))
                {
                    return BadRequest("No active session found");
                }

                var autosaveKey = $"autosave_{studentId}_{submission.GroupId}_{submission.QuestionIndex}";
                var saveData = new AutoSaveData
                {
                    Code = submission.Code,
                    LastSaved = DateTime.Now
                };

                var jsonString = JsonSerializer.Serialize(saveData);
                HttpContext.Session.SetString(autosaveKey, jsonString);

                //Console.WriteLine($"Auto-saved code for student {studentId}, question {submission.QuestionIndex}");
                return Ok(new { message = "Code auto-saved", timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error in AutoSaveCode: {ex.Message}");
                return StatusCode(500, "Failed to auto-save code");
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

        public class AutoSaveData
        {
            public string Code { get; set; }
            public DateTime LastSaved { get; set; }
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
        public string StudentId { get; set; }
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public string Code { get; set; }
    }
}