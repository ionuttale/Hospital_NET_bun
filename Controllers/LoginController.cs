using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Hospital_simple.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MySqlConnector;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static BCrypt.Net.BCrypt;

namespace Hospital_simple.Controllers;

[AllowAnonymous]
public class LoginController : Controller
{
    private readonly ILogger<LoginController> _logger;
    private readonly IConfiguration _configuration;

    public LoginController(ILogger<LoginController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            return role switch
            {
                "admin" => RedirectToAction("Index", "Admin"),
                "client" => RedirectToAction("Index", "Client"),
                _ => RedirectToAction("Index", "Login") // fallback
            };
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            string storedHash = null;
            int userId = 0;
            string userRole = null;
            ulong? doctorId = null; 

            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            string sql = "SELECT UserID, Password, UserType, DoctorID FROM Users WHERE Username = @username";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@username", model.Username);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    storedHash = reader.GetString("Password").Trim();
                    userId = reader.GetInt32("UserID");
                    userRole = reader.GetString("UserType").ToLower(); 
                    
                    doctorId = reader.IsDBNull(reader.GetOrdinal("DoctorID")) ? null : (ulong?)reader.GetInt32("DoctorID");
                }
            } 


            if (storedHash != null && CheckPassword(storedHash, model.Password))
            {
                try
                {
                    string updateSql = "UPDATE Users SET LastLogin = @now WHERE UserID = @id";
                    await using var updateCmd = new MySqlCommand(updateSql, connection);
                    updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                    updateCmd.Parameters.AddWithValue("@id", userId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update LastLogin for UserID {UserId}", userId);
                }
                
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.Role, userRole)
                };

        
                if (userRole == "client")
                {
                    if (!doctorId.HasValue)
                    {
                        _logger.LogError("Client user {Username} has no associated DoctorID.", model.Username);
                        ModelState.AddModelError(string.Empty, "Client account is not linked to a doctor.");
                        return View(model);
                    }
                    
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, doctorId.Value.ToString()));
                }
                else 
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("User {Username} logged in successfully as {Role}.", model.Username, userRole);

                return userRole switch
                {
                    "admin" => RedirectToAction("Index", "Admin"),
                    "client" => RedirectToAction("Index", "Client"),
                    _ => RedirectToAction("Index", "Login")
                };
            }
            else
            {
                _logger.LogWarning("Failed login attempt for user {Username}.", model.Username);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error during login for user {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Database error. Please try again.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Login");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private bool CheckPassword(string storedHash, string password)
    {
        return Verify(password, storedHash);
    }
}