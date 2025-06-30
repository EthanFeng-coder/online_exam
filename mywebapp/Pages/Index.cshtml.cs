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

        public async Task<IActionResult> OnPostAsync()
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
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Failed to load student data");

                Console.WriteLine($"Login attempt - ID: {StudentId}");
                
                var studentAuthenticated = studentData.Students.Any(s => s.ValidateCredentials(StudentId, Password));
                
                if (studentAuthenticated)
                {
                    // Set authentication cookie
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.Now.AddHours(2)
                    };

                    Response.Cookies.Append("StudentAuth", StudentId, cookieOptions);

                    return RedirectToPage("/Dashboard", new 
                    { 
                        studentId = StudentId,
                        group = 1,
                        question = 0
                    });
                }

                Console.WriteLine($"Login failed - Invalid credentials for ID: {StudentId}");
                ModelState.AddModelError(string.Empty, "Invalid Student ID or Password");
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, "An error occurred during login");
                return Page();
            }
        }
    }
}