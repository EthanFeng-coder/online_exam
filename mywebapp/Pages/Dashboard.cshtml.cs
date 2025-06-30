using System.Net;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using mywebapp.Models;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace mywebapp.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public string? ErrorMessage { get; set; }
        public Question? CurrentQuestion { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Group { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Question { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StudentId { get; set; } = string.Empty;

        [BindProperty]
        public string SubmittedCode { get; set; } = string.Empty;

        public DashboardModel(IHttpClientFactory clientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _clientFactory = clientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Check for authentication cookie
                var authCookie = _httpContextAccessor.HttpContext?.Request.Cookies["StudentAuth"];
                if (string.IsNullOrEmpty(authCookie) || authCookie != StudentId)
                {
                    Console.WriteLine($"Direct URL access attempt without authentication. StudentId: {StudentId}");
                    return RedirectToPage("/Index");
                }

                if (string.IsNullOrEmpty(StudentId))
                {
                    return RedirectToPage("/Index");
                }
                

                // First get student's current progress
                var client = _clientFactory.CreateClient("Questions");
                var studentResponse = await client.GetAsync($"api/Questions/students/{StudentId}");
               
                // if (studentResponse.IsSuccessStatusCode)
                // {
                //     var studentData = await studentResponse.Content.ReadFromJsonAsync<Student>();
                //     Console.WriteLine($"{Group} {studentData.Progress.CurrentGroupId}");
                //     if (studentData != null && Group != studentData.Progress.CurrentGroupId)
                //     {
                //         Console.WriteLine($"Student {StudentId} attempted to access unauthorized group. Current: {studentData.Progress.CurrentGroupId}, Attempted: {Group}");
                //         return RedirectToPage("/Index");
                //     }
                // }

                // Check if this is the last question of a group
                if (Question > 4)
                {
                    Question = 0;
                    Group = 1;
                }

                var response = await client.GetAsync($"api/Questions/groups/{Group}/questions/{Question}?studentId={StudentId}");
                Console.WriteLine($"API Response status: {response.StatusCode}");

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"Student {StudentId} attempted to access questions after completion");
                    // Remove auth cookie since they're done
                    Response.Cookies.Delete("StudentAuth");
                    return RedirectToPage("/Index");
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine($"Unauthorized access attempt for Student: {StudentId}, Group: {Group}");
                    return RedirectToPage("/Index");
                }

                if (response.IsSuccessStatusCode)
                {
                    CurrentQuestion = await response.Content.ReadFromJsonAsync<Question>();
                    return Page();
                }
               
                
                ErrorMessage = "Failed to load question";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(StudentId))
                {
                    Console.WriteLine("No student ID found for submission");
                    return RedirectToPage("/Index");
                }

                Console.WriteLine($"Submitting solution for Student: {StudentId}, Group: {Group}, Question: {Question}");

                var client = _clientFactory.CreateClient("Questions");
                var submission = new CodeSubmission
                {
                    StudentId = StudentId,
                    GroupId = Group,
                    QuestionIndex = Question,
                    Code = SubmittedCode,
                    ReplaceExisting = true  // Flag to indicate replacing existing submission
                };

                // First, delete any existing submission for this question
                var deleteResponse = await client.DeleteAsync(
                    $"api/Questions/submissions?studentId={StudentId}&groupId={Group}&questionIndex={Question}");
                
                if (!deleteResponse.IsSuccessStatusCode && deleteResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Failed to clean up old submission: {deleteResponse.StatusCode}");
                }

                // Submit the new code
                var response = await client.PostAsJsonAsync("api/Questions/submit", submission);
                Console.WriteLine($"Submission response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SubmissionResult>();
                    if (result == null)
                    {
                        throw new InvalidOperationException("Failed to get submission result");
                    }

                    Console.WriteLine($"Submission successful. Next question: {result.NextQuestion}");
                    Console.WriteLine($"Updating progress for student: {StudentId}");

                    // Check if this was the last question in the group (index 4)
                    if (Question == 4)  // Last question in group
                    {   
                        Console.WriteLine($"Student {StudentId} completed group {Group}");
                        
                        // Mark the student's progress as done in their JSON
                        var doneResponse = await client.PostAsJsonAsync($"api/Questions/markAsDone", new
                        {
                            StudentId = StudentId,
                            GroupId = Group,
                            CompletedAt = DateTime.UtcNow,
                            Done = true
                        });

                        if (!doneResponse.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Failed to mark student as done: {doneResponse.StatusCode}");
                            ErrorMessage = "Failed to mark completion";
                            await OnGetAsync();
                            return Page();
                        }

                        // Update progress for the last question
                        var updateProgressResponse = await client.PostAsJsonAsync($"api/Questions/updateProgress", new
                        {
                            StudentId = StudentId,
                            GroupId = Group,
                            QuestionIndex = Question,
                            Completed = true
                        });

                        if (!updateProgressResponse.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Failed to update progress: {updateProgressResponse.StatusCode}");
                            ErrorMessage = "Failed to update progress";
                            await OnGetAsync();
                            return Page();
                        }
                        // Remove the authentication cookie
                        Response.Cookies.Delete("StudentAuth");
                        
                        // Redirect to completion page
                        return RedirectToPage("/Completion", new
                        {
                            studentId = StudentId,
                            groupId = Group,
                            done = true
                        });
                    }

                    // For questions 0-3, continue with normal progress update
                    var updateResponse = await client.PostAsJsonAsync($"api/Questions/updateProgress", new
                    {
                        StudentId = StudentId,
                        GroupId = Group,
                        QuestionIndex = Question,
                        Completed = true
                    });

                    if (!updateResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Failed to update progress: {updateResponse.StatusCode}");
                        ErrorMessage = "Failed to update progress";
                        await OnGetAsync();
                        return Page();
                    }

                    // Redirect to next question
                    return RedirectToPage("/Dashboard", new 
                    { 
                        studentId = StudentId,
                        group = result.NextGroup,
                        question = result.NextQuestion
                    });
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Submission failed: {errorContent}");
                ErrorMessage = "Failed to submit solution";
                await OnGetAsync();
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in submission: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                ErrorMessage = "Error submitting solution";
                await OnGetAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAutoSaveAsync([FromBody] CodeSubmission submission)
        {
            try
            {
                if (string.IsNullOrEmpty(submission.StudentId))
                {
                    return BadRequest("No student ID provided");
                }

                var client = _clientFactory.CreateClient("Questions");
                
                var autoSave = new AutoSave
                {
                    GroupId = submission.GroupId,
                    QuestionIndex = submission.QuestionIndex,
                    Code = submission.Code,
                    SavedAt = DateTime.UtcNow
                };

                // Submit the auto-saved code
                var response = await client.PostAsJsonAsync($"api/Questions/autosave/{submission.StudentId}", autoSave);
                
                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true });
                }

                return new JsonResult (new { success = false, message = "Failed to auto-save" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }

    public class SubmissionResult
    {
        public string Message { get; set; } = string.Empty;
        public int NextGroup { get; set; }
        public int NextQuestion { get; set; }
    }

    public class CodeSubmission
    {
        public required string StudentId { get; set; }
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public required string Code { get; set; }
        public bool ReplaceExisting { get; set; } = true;  // Default to replacing existing submissions
    }

    public class AutoSave
    {
        public int GroupId { get; set; }
        public int QuestionIndex { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
    }
}