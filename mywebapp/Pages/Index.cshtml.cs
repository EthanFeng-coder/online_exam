using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using mywebapp.Models;

namespace mywebapp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string StudentsPath = "Data/students.json";

        public IndexModel(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
        }

        [BindProperty]
        [Required]
        [Display(Name = "Student ID")]
        public string StudentId { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var jsonPath = Path.Combine(_environment.ContentRootPath, StudentsPath);
                var jsonString = System.IO.File.ReadAllText(jsonPath);
                var studentData = JsonSerializer.Deserialize<StudentData>(jsonString, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Console.WriteLine($"Login attempt - ID: {StudentId}");
                
                var student = studentData.Students.FirstOrDefault(s => s.ValidateCredentials(StudentId, Password));
                
                if (student != null)
                {
                    Console.WriteLine($"Login successful - ID: {student.Id}");
                    
                    // Store student info in session
                    HttpContext.Session.SetString("StudentId", student.Id);
                    HttpContext.Session.SetString("StudentName", student.Name);
                    
                    // Redirect to dashboard with parameters
                    return RedirectToPage("/Dashboard", new { 
                        group = 1, 
                        question = 0,
                        studentId = student.Id 
                    });
                }

                Console.WriteLine($"Login failed - Invalid credentials for ID: {StudentId}");
                ModelState.AddModelError(string.Empty, "Invalid Student ID or Password");
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                ModelState.AddModelError(string.Empty, "An error occurred during login");
                return Page();
            }
        }
    }
}