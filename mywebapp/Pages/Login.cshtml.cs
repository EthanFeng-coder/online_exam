using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace mywebapp.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            public string StudentId { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Passcode { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                // TODO: Replace with actual user validation
                if (Input.StudentId == "admin" && Input.Passcode == "admin123")
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, Input.StudentId),
                        new Claim(ClaimTypes.Role, "Student")
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    // Store student ID in session immediately after successful login
                    HttpContext.Session.SetString("StudentId", Input.StudentId);
                    Console.WriteLine($"Set student ID in session: {Input.StudentId}");

                    // Redirect to Dashboard with query parameters
                    return RedirectToPage("/Dashboard", new { 
                        group = 1, 
                        question = 0, 
                        studentId = Input.StudentId 
                    });
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                ModelState.AddModelError(string.Empty, "Login failed");
                return Page();
            }
        }
    }
}
