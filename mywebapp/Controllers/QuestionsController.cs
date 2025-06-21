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

                // Get student number segments
                var firstThree = studentId.Substring(0, 3);
                var nextThree = studentId.Substring(3, 3);
                Console.WriteLine($"Student ID segments: {firstThree}, {nextThree}");

                // Create replacement dictionary with more placeholder formats
                var replacements = new Dictionary<string, string>
                {
                    {"[98]", firstThree},
                    {"[56]", nextThree},
                    {"[0,3]", firstThree},
                    {"[3,6]", nextThree}
                };

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
            var sessionKey = $"question_{groupId}_{questionIndex}_text";
            var storedText = HttpContext.Session.GetString(sessionKey);

            if (!string.IsNullOrEmpty(storedText))
            {
                Console.WriteLine($"Retrieved stored text for student {studentId} from session");
                return storedText;
            }

            var modifiedText = ReplaceWithStudentNumbers(originalText, studentId);
            HttpContext.Session.SetString(sessionKey, modifiedText);
            Console.WriteLine($"Stored new text for student {studentId} in session");
            return modifiedText;
        }

        [HttpGet("groups/{groupId}/questions/{questionIndex}")]
        public ActionResult<Question> GetQuestionFromGroup(int groupId, int questionIndex)
        {
            try
            {
                // Get student ID from session only
                var studentId = HttpContext.Session.GetString("StudentId");
                Console.WriteLine($"Retrieved student ID from session: {studentId}");

                if (string.IsNullOrEmpty(studentId))
                {
                    Console.WriteLine("No student ID found in session");
                    return BadRequest(new { error = "Please log in first" });
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
                var studentId = HttpContext.Session.GetString("StudentId");
                if (string.IsNullOrEmpty(studentId))
                {
                    return BadRequest("No student ID found in session");
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

                // Store submission in session
                var submissionKey = $"submission_{studentId}_{submission.GroupId}_{submission.QuestionIndex}";
                HttpContext.Session.SetString(submissionKey, submission.Code);

                return Ok(new { message = "Solution submitted successfully" });
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
}