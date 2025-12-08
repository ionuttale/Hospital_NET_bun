using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using Hospital_simple.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using BCrypt;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq; 

namespace Hospital_simple.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IConfiguration configuration, ILogger<AdminController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // ------------------------- Dashboard -------------------------
        public async Task<IActionResult> Index()
        {
            try
            {
                int totalDoctors = 0;
                int totalPatients = 0;

                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                using var cmdDoctors = new MySqlCommand("SELECT COUNT(*) FROM Doctors", connection);
                totalDoctors = Convert.ToInt32(await cmdDoctors.ExecuteScalarAsync());

                using var cmdPatients = new MySqlCommand("SELECT COUNT(*) FROM Patients", connection);
                totalPatients = Convert.ToInt32(await cmdPatients.ExecuteScalarAsync());

                ViewData["TotalDoctors"] = totalDoctors;
                ViewData["TotalPatients"] = totalPatients;

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Index: {ex}");
                return View();
            }
        }

        // ------------------------- Users -------------------------
        public async Task<IActionResult> ManageUsers()
        {
            var users = new List<UserModel>();
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT UserID, Username, UserType, LastLogin, DoctorID FROM Users";
                using var cmd = new MySqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    users.Add(new UserModel
                    {
                        UserID = (ulong)reader.GetInt32("UserID"),
                        Username = reader.GetString("Username"),
                        UserType = reader.GetString("UserType"),
                        LastLogin = reader.IsDBNull(reader.GetOrdinal("LastLogin")) ? null : reader.GetDateTime("LastLogin"),
                        DoctorID = reader.IsDBNull(reader.GetOrdinal("DoctorID")) ? null : (ulong?)reader.GetInt32("DoctorID")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManageUsers: {ex}");
            }

            return View(users);
        }
        
        // GET: Admin/CreateUser
        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            await PopulateCreateUserDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                
                await using var transaction = await connection.BeginTransactionAsync();

                ulong? newDoctorId = null;

                try 
                {
                    if (model.UserType == "client")
                    {
                        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
                        {
                            ModelState.AddModelError("", "First Name and Last Name are required for Doctors.");
                            return View(model);
                        }

                        string sqlDoctor = @"INSERT INTO Doctors (FirstName, LastName, Email, Specialty) 
                                            VALUES (@first, @last, @email, @spec);
                                            SELECT LAST_INSERT_ID();";
                        
                        using var cmdDoc = new MySqlCommand(sqlDoctor, connection, transaction);
                        cmdDoc.Parameters.AddWithValue("@first", model.FirstName);
                        cmdDoc.Parameters.AddWithValue("@last", model.LastName);
                        cmdDoc.Parameters.AddWithValue("@email", model.Email ?? (object)DBNull.Value);
                        cmdDoc.Parameters.AddWithValue("@spec", model.Specialty ?? (object)DBNull.Value);
                        
                        newDoctorId = Convert.ToUInt64(await cmdDoc.ExecuteScalarAsync());
                    }

                    string sqlUser = @"INSERT INTO Users (Username, Password, Type, DoctorID) 
                                    VALUES (@user, @pass, @type, @docId)";

                    using var cmdUser = new MySqlCommand(sqlUser, connection, transaction);
                    cmdUser.Parameters.AddWithValue("@user", model.Username);
                    cmdUser.Parameters.AddWithValue("@pass", model.Password); 
                    cmdUser.Parameters.AddWithValue("@type", model.UserType);
                    cmdUser.Parameters.AddWithValue("@docId", newDoctorId ?? (object)DBNull.Value);

                    await cmdUser.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw; 
                }

                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating user: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string updateSql = "UPDATE Users SET DoctorID = NULL WHERE UserID = @userId";
                using var updateCmd = new MySqlCommand(updateSql, connection);
                updateCmd.Parameters.AddWithValue("@userId", id);
                await updateCmd.ExecuteNonQueryAsync();

                string deleteSql = "DELETE FROM Users WHERE UserID = @userId";
                using var deleteCmd = new MySqlCommand(deleteSql, connection);
                deleteCmd.Parameters.AddWithValue("@userId", id);
                await deleteCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteUser {id}: {ex}");
            }

            return RedirectToAction("ManageUsers");
        }

        // ------------------------- Doctors -------------------------
        public async Task<IActionResult> ManageDoctors()
        {
            var doctors = new List<DoctorModel>();
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT * FROM Doctors";
                using var cmd = new MySqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    doctors.Add(new DoctorModel
                    {
                        DoctorID = (ulong)reader.GetInt32("DoctorID"),
                        FirstName = reader.GetString("FirstName"),
                        LastName = reader.GetString("LastName"),
                        Email = reader.GetString("Email"),
                        Specialization = reader.IsDBNull(reader.GetOrdinal("Specialization")) ? "" : reader.GetString("Specialization")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManageDoctors: {ex}");
            }

            return View(doctors);
        }

        // GET: Admin/CreateDoctor
        [HttpGet]
        public IActionResult CreateDoctor()
        {
            return View(new CreateDoctorUserModel());
        }

        // POST: Admin/CreateDoctor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(CreateDoctorUserModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                string sqlDoctor = @"
                    INSERT INTO Doctors (FirstName, LastName, Email, Specialization)
                    VALUES (@first, @last, @email, @spec);
                    SELECT LAST_INSERT_ID();"; 
                
                using var cmdDoctor = new MySqlCommand(sqlDoctor, connection, transaction);
                cmdDoctor.Parameters.AddWithValue("@first", model.FirstName);
                cmdDoctor.Parameters.AddWithValue("@last", model.LastName);
                cmdDoctor.Parameters.AddWithValue("@email", model.Email);
                cmdDoctor.Parameters.AddWithValue("@spec", model.Specialization ?? (object)DBNull.Value);

                ulong newDoctorId = Convert.ToUInt64(await cmdDoctor.ExecuteScalarAsync());

                string sqlUser = @"
                    INSERT INTO Users (Username, Password, UserType, DoctorID)
                    VALUES (@user, @pass, 'client', @docId)";
                
                using var cmdUser = new MySqlCommand(sqlUser, connection, transaction);
                cmdUser.Parameters.AddWithValue("@user", model.Username);
                cmdUser.Parameters.AddWithValue("@pass", model.Password);
                cmdUser.Parameters.AddWithValue("@docId", newDoctorId);

                await cmdUser.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                
                return RedirectToAction(nameof(ManageDoctors));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error in CreateDoctor POST: {ex}");
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                return View(model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> UpdateDoctor(ulong id)
        {
            DoctorModel doctor = null;
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT * FROM Doctors WHERE DoctorID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    doctor = new DoctorModel
                    {
                        DoctorID = (ulong)reader.GetInt32("DoctorID"),
                        FirstName = reader.GetString("FirstName"),
                        LastName = reader.GetString("LastName"),
                        Email = reader.GetString("Email"),
                        Specialization = reader.IsDBNull(reader.GetOrdinal("Specialization")) ? "" : reader.GetString("Specialization")
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateDoctor GET {id}: {ex}");
            }

            return View(doctor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDoctor(DoctorModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                    await connection.OpenAsync();

                    string sql = @"UPDATE Doctors 
                                   SET FirstName=@first, LastName=@last, Email=@email, Specialization=@spec 
                                   WHERE DoctorID=@id";
                    using var cmd = new MySqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@first", model.FirstName);
                    cmd.Parameters.AddWithValue("@last", model.LastName);
                    cmd.Parameters.AddWithValue("@email", model.Email);
                    cmd.Parameters.AddWithValue("@spec", model.Specialization);
                    cmd.Parameters.AddWithValue("@id", model.DoctorID);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateDoctor POST {model.DoctorID}: {ex}");
            }

            return RedirectToAction(nameof(ManageDoctors));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctor(ulong id)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string deleteUserSql = "DELETE FROM Users WHERE DoctorID=@id AND UserType='client'";
                using var userCmd = new MySqlCommand(deleteUserSql, connection);
                userCmd.Parameters.AddWithValue("@id", id);
                await userCmd.ExecuteNonQueryAsync();

                string updateUsersSql = "UPDATE Users SET DoctorID=NULL WHERE DoctorID=@id";
                using var updateCmd = new MySqlCommand(updateUsersSql, connection);
                updateCmd.Parameters.AddWithValue("@id", id);
                await updateCmd.ExecuteNonQueryAsync();
                
                string deleteSql = "DELETE FROM Doctors WHERE DoctorID=@id";
                using var deleteCmd = new MySqlCommand(deleteSql, connection);
                deleteCmd.Parameters.AddWithValue("@id", id);
                await deleteCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteDoctor {id}: {ex}");
                TempData["ErrorMessage"] = "Could not delete doctor. They may still be linked to consultations.";
            }

            return RedirectToAction(nameof(ManageDoctors));
        }

        // ------------------------- Patients -------------------------
        public async Task<IActionResult> ManagePatients()
        {
            var patients = new List<PatientModel>();
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT * FROM Patients";
                using var cmd = new MySqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    patients.Add(new PatientModel
                    {
                        PatientID = (ulong)reader.GetInt32("PatientID"),
                        FirstName = reader.GetString("FirstName"),
                        LastName = reader.GetString("LastName"),
                        Email = reader.GetString("Email"),
                        Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? "" : reader.GetString("Address"),
                        Insurance = reader.IsDBNull(reader.GetOrdinal("Insurance")) ? "" : reader.GetString("Insurance")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManagePatients: {ex}");
            }

            return View(patients);
        }

        // GET: Admin/CreatePatient
        [HttpGet]
        public IActionResult CreatePatient()
        {
            return View();
        }

        // POST: Admin/CreatePatient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatient(PatientModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO Patients (FirstName, LastName, Email, Address, Insurance)
                    VALUES (@first, @last, @email, @address, @insurance)";
                
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@first", model.FirstName);
                cmd.Parameters.AddWithValue("@last", model.LastName);
                cmd.Parameters.AddWithValue("@email", model.Email);
                cmd.Parameters.AddWithValue("@address", model.Address ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@insurance", model.Insurance ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                
                return RedirectToAction(nameof(ManagePatients));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreatePatient POST: {ex}");
                ModelState.AddModelError("", "An error occurred while creating the patient.");
                return View(model);
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> UpdatePatient(ulong id)
        {
            PatientModel patient = null;
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT * FROM Patients WHERE PatientID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    patient = new PatientModel
                    {
                        PatientID = (ulong)reader.GetInt32("PatientID"),
                        FirstName = reader.GetString("FirstName"),
                        LastName = reader.GetString("LastName"),
                        Email = reader.GetString("Email"),
                        Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? "" : reader.GetString("Address"),
                        Insurance = reader.IsDBNull(reader.GetOrdinal("Insurance")) ? "" : reader.GetString("Insurance")
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdatePatient GET {id}: {ex}");
            }

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePatient(PatientModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                    await connection.OpenAsync();

                    string sql = @"UPDATE Patients
                                   SET FirstName=@first, LastName=@last, Email=@email, Address=@address, Insurance=@insurance
                                   WHERE PatientID=@id";
                    using var cmd = new MySqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@first", model.FirstName);
                    cmd.Parameters.AddWithValue("@last", model.LastName);
                    cmd.Parameters.AddWithValue("@email", model.Email);
                    cmd.Parameters.AddWithValue("@address", model.Address);
                    cmd.Parameters.AddWithValue("@insurance", model.Insurance);
                    cmd.Parameters.AddWithValue("@id", model.PatientID);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdatePatient POST {model.PatientID}: {ex}");
            }

            return RedirectToAction(nameof(ManagePatients));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(ulong id)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "DELETE FROM Patients WHERE PatientID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeletePatient {id}: {ex}");
            }

            return RedirectToAction(nameof(ManagePatients));
        }

        // ------------------------- Medicines -------------------------
        public async Task<IActionResult> ManageMedicines()
        {
            var medicines = new List<MedicineModel>();
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT * FROM Medicines";
                using var cmd = new MySqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    medicines.Add(new MedicineModel
                    {
                        MedicineID = (ulong)reader.GetInt32("MedicineID"),
                        Name = reader.GetString("Name")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManageMedicines: {ex}");
            }

            return View(medicines);
        }
        
        // GET: Admin/CreateMedicine
        [HttpGet]
        public IActionResult CreateMedicine()
        {
            return View();
        }

        // POST: Admin/CreateMedicine
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMedicine(MedicineModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                
                string sql = "INSERT INTO Medicines (Name) VALUES (@name)";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@name", model.Name);
                
                await cmd.ExecuteNonQueryAsync();
                
                return RedirectToAction(nameof(ManageMedicines));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateMedicine POST: {ex}");
                ModelState.AddModelError("", "An error occurred while creating the medicine.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> UpdateMedicine(ulong id)
        {
            MedicineModel medicine = null;
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = "SELECT * FROM Medicines WHERE MedicineID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    medicine = new MedicineModel
                    {
                        MedicineID = (ulong)reader.GetInt32("MedicineID"),
                        Name = reader.GetString("Name")
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateMedicine GET {id}: {ex}");
            }

            return View(medicine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMedicine(MedicineModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                    await connection.OpenAsync();

                    string sql = "UPDATE Medicines SET Name=@name WHERE MedicineID=@id";
                    using var cmd = new MySqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@name", model.Name);
                    cmd.Parameters.AddWithValue("@id", model.MedicineID);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateMedicine POST {model.MedicineID}: {ex}");
            }

            return RedirectToAction(nameof(ManageMedicines));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMedicine(ulong id)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string deleteMedSql = "DELETE FROM Receipt_Medicines WHERE MedicineID=@id";
                using var cmdMed = new MySqlCommand(deleteMedSql, connection);
                cmdMed.Parameters.AddWithValue("@id", id);
                await cmdMed.ExecuteNonQueryAsync();

                string sql = "DELETE FROM Medicines WHERE MedicineID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteMedicine {id}: {ex}");
            }

            return RedirectToAction(nameof(ManageMedicines));
        }

        // ------------------------- Consultations -------------------------
        public async Task<IActionResult> ManageConsultations()
        {
            var consultations = new List<ConsultationModels>();
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT c.ConsultID, c.PatientID, p.FirstName AS PatientFirst, p.LastName AS PatientLast,
                        c.DoctorID, d.FirstName AS DoctorFirst, d.LastName AS DoctorLast,
                        c.Date, c.Disease, c.Observation,
                        r.ReceiptID
                    FROM Consultations c
                    INNER JOIN Patients p ON c.PatientID=p.PatientID
                    INNER JOIN Doctors d ON c.DoctorID=d.DoctorID
                    LEFT JOIN Receipts r ON c.ConsultID = r.ConsultID
                    ORDER BY c.Date DESC";

                using var cmd = new MySqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    consultations.Add(new ConsultationModels
                    {
                        ConsultationID = (ulong)reader.GetInt32("ConsultID"),
                        PatientID = (ulong)reader.GetInt32("PatientID"),
                        PatientName = $"{reader.GetString("PatientFirst")} {reader.GetString("PatientLast")}",
                        DoctorID = (ulong)reader.GetInt32("DoctorID"),
                        DoctorName = $"{reader.GetString("DoctorFirst")} {reader.GetString("DoctorLast")}",
                        ConsultationDate = reader.GetDateTime("Date"),
                        Disease = reader.IsDBNull(reader.GetOrdinal("Disease")) ? "" : reader.GetString("Disease"),
                        Observation = reader.IsDBNull(reader.GetOrdinal("Observation")) ? "" : reader.GetString("Observation"),
                        
                        ReceiptID = reader.IsDBNull(reader.GetOrdinal("ReceiptID")) ? null : (ulong?)reader.GetInt32("ReceiptID")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManageConsultations: {ex}");
            }

            return View(consultations);
        }
        
        // GET: Admin/CreateConsultation
        [HttpGet]
        public async Task<IActionResult> CreateConsultation()
        {
            await PopulateConsultationDropdowns(new ConsultationModels());
            return View();
        }

        // POST: Admin/CreateConsultation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConsultation(ConsultationModels model)
        {
            if (model.PatientID == 0 || model.DoctorID == 0 || string.IsNullOrEmpty(model.Disease))
            {
                ModelState.AddModelError("", "Please select a Patient, Doctor, and enter a Disease.");
                await PopulateConsultationDropdowns(model);
                return View(model);
            }

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                
                string sql = @"
                    INSERT INTO Consultations (PatientID, DoctorID, Date, Disease, Observation)
                    VALUES (@patientId, @doctorId, @date, @disease, @obs)";
                
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@patientId", model.PatientID);
                cmd.Parameters.AddWithValue("@doctorId", model.DoctorID);
                cmd.Parameters.AddWithValue("@date", model.ConsultationDate);
                cmd.Parameters.AddWithValue("@disease", model.Disease);
                cmd.Parameters.AddWithValue("@obs", model.Observation ?? (object)DBNull.Value);
                
                await cmd.ExecuteNonQueryAsync();
                
                return RedirectToAction(nameof(ManageConsultations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateConsultation POST: {ex}");
                ModelState.AddModelError("", "An error occurred while creating the consultation.");
                await PopulateConsultationDropdowns(model);
                return View(model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> UpdateConsultation(int id)
        {
            ConsultationModels consultation = null;

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                // Get consultation details
                string sql = @"
                    SELECT c.ConsultID, c.PatientID, c.DoctorID, c.Date, c.Disease, c.Observation
                    FROM Consultations c
                    WHERE c.ConsultID=@id";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    consultation = new ConsultationModels
                    {
                        ConsultationID = (ulong)reader.GetInt32("ConsultID"),
                        PatientID = (ulong)reader.GetInt32("PatientID"),
                        DoctorID = (ulong)reader.GetInt32("DoctorID"),
                        ConsultationDate = reader.GetDateTime("Date"),
                        Disease = reader.IsDBNull(reader.GetOrdinal("Disease")) ? "" : reader.GetString("Disease"),
                        Observation = reader.IsDBNull(reader.GetOrdinal("Observation")) ? "" : reader.GetString("Observation")
                    };
                }
                await reader.CloseAsync();

                if (consultation == null) return NotFound();
                
                await PopulateConsultationDropdowns(consultation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET UpdateConsultation: {ex.Message}");
                if (consultation == null) consultation = new ConsultationModels();
            }

            return View(consultation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateConsultation(ConsultationModels model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await PopulateConsultationDropdowns(model);
                    return View(model);
                }

                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string updateSql = @"
                    UPDATE Consultations
                    SET PatientID=@patient, DoctorID=@doctor, Date=@date, Disease=@disease, Observation=@obs
                    WHERE ConsultID=@id";

                using var cmd = new MySqlCommand(updateSql, connection);
                cmd.Parameters.AddWithValue("@patient", model.PatientID);
                cmd.Parameters.AddWithValue("@doctor", model.DoctorID);
                cmd.Parameters.AddWithValue("@date", model.ConsultationDate);
                cmd.Parameters.AddWithValue("@disease", model.Disease);
                cmd.Parameters.AddWithValue("@obs", model.Observation);
                cmd.Parameters.AddWithValue("@id", model.ConsultationID);

                await cmd.ExecuteNonQueryAsync();
                return RedirectToAction(nameof(ManageConsultations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in POST UpdateConsultation: {ex.Message}");
                await PopulateConsultationDropdowns(model);
                return View(model);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConsultation(ulong id)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                var receiptIdsToDelete = new List<ulong>();
                string findReceiptsSql = "SELECT ReceiptID FROM Receipts WHERE ConsultID=@id";
                using (var findCmd = new MySqlCommand(findReceiptsSql, connection))
                {
                    findCmd.Parameters.AddWithValue("@id", id);
                    using (var reader = await findCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            receiptIdsToDelete.Add((ulong)reader.GetInt32("ReceiptID"));
                        }
                    } 
                }

                if (receiptIdsToDelete.Any())
                {
                    foreach (var receiptId in receiptIdsToDelete)
                    {
                        using var cmdMed = new MySqlCommand("DELETE FROM Receipt_Medicines WHERE ReceiptID=@rId", connection);
                        cmdMed.Parameters.AddWithValue("@rId", receiptId);
                        await cmdMed.ExecuteNonQueryAsync();
                    }
                }

                string deleteReceiptSql = "DELETE FROM Receipts WHERE ConsultID=@id";
                using var cmdReceipt = new MySqlCommand(deleteReceiptSql, connection);
                cmdReceipt.Parameters.AddWithValue("@id", id);
                await cmdReceipt.ExecuteNonQueryAsync();

                string sql = "DELETE FROM Consultations WHERE ConsultID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteConsultation {id}: {ex}");
            }

            return RedirectToAction(nameof(ManageConsultations));
        }

        // ------------------------- Receipts -------------------------
        public async Task<IActionResult> ManageReceipts()
        {
            var receipts = new List<ReceiptModel>();
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT r.ReceiptID, r.ConsultID,
                        c.Disease,
                        p.FirstName AS PatientFirst, p.LastName AS PatientLast,
                        d.FirstName AS DoctorFirst, d.LastName AS DoctorLast,
                        rm.ReceiptMedicineID, rm.Quantity, rm.Dosage,
                        m.MedicineID, m.Name AS MedicineName
                    FROM Receipts r
                    INNER JOIN Consultations c ON r.ConsultID = c.ConsultID
                    INNER JOIN Patients p ON c.PatientID = p.PatientID
                    INNER JOIN Doctors d ON c.DoctorID = d.DoctorID
                    LEFT JOIN Receipt_Medicines rm ON r.ReceiptID = rm.ReceiptID
                    LEFT JOIN Medicines m ON rm.MedicineID = m.MedicineID
                    ORDER BY r.ReceiptID";

                using var cmd = new MySqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                var receiptDict = new Dictionary<ulong, ReceiptModel>();

                while (await reader.ReadAsync())
                {
                    var receiptId = (ulong)reader.GetInt32("ReceiptID");
                    var consultId = (ulong)reader.GetInt32("ConsultID");

                    if (!receiptDict.ContainsKey(receiptId))
                    {
                        receiptDict[receiptId] = new ReceiptModel
                        {
                            ReceiptID = receiptId,
                            ConsultationID = consultId,
                            Disease = reader.IsDBNull(reader.GetOrdinal("Disease")) ? "" : reader.GetString("Disease"),
                            PatientName = $"{reader.GetString("PatientFirst")} {reader.GetString("PatientLast")}",
                            DoctorName = $"{reader.GetString("DoctorFirst")} {reader.GetString("DoctorLast")}",
                            Medicines = new List<ReceiptMedicineModel>()
                        };
                    }

                    if (!reader.IsDBNull(reader.GetOrdinal("ReceiptMedicineID")))
                    {
                        var med = new ReceiptMedicineModel
                        {
                            ReceiptMedicineID = (ulong)reader.GetInt32("ReceiptMedicineID"),
                            Quantity = (uint)reader.GetInt32("Quantity"),
                            Dosage = reader.GetString("Dosage"),
                            Medicine = new MedicineModel
                            {
                                MedicineID = (ulong)reader.GetInt32("MedicineID"),
                                Name = reader.GetString("MedicineName")
                            }
                        };
                        receiptDict[receiptId].Medicines.Add(med);
                    }
                }

                receipts = new List<ReceiptModel>(receiptDict.Values);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManageReceipts: {ex}");
            }

            return View(receipts);
        }

        // GET: Admin/CreateReceipt
        [HttpGet]
        public async Task<IActionResult> CreateReceipt()
        {
            await PopulateCreateReceiptDropdowns();
            var model = new CreateReceiptModel();
            for (int i = 0; i < 5; i++)
            {
                model.Medicines.Add(new ReceiptMedicineLineItem());
            }
            return View(model);
        }

        // POST: Admin/CreateReceipt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReceipt(CreateReceiptModel model)
        {
            var validMedicines = model.Medicines
                .Where(m => m.MedicineID > 0 && m.Quantity > 0 && !string.IsNullOrEmpty(m.Dosage))
                .ToList();

            if (model.ConsultationID == 0 || !validMedicines.Any())
            {
                ModelState.AddModelError("", "You must select a consultation and add at least one valid medicine.");
                await PopulateCreateReceiptDropdowns();
                return View(model);
            }

            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                string sqlReceipt = @"
                    INSERT INTO Receipts (ConsultID) VALUES (@consultId);
                    SELECT LAST_INSERT_ID();";
                
                using var cmdReceipt = new MySqlCommand(sqlReceipt, connection, transaction);
                cmdReceipt.Parameters.AddWithValue("@consultId", model.ConsultationID);
                ulong receiptId = Convert.ToUInt64(await cmdReceipt.ExecuteScalarAsync());

                foreach (var med in validMedicines)
                {
                    string sqlMed = @"
                        INSERT INTO Receipt_Medicines (ReceiptID, MedicineID, Quantity, Dosage)
                        VALUES (@receiptId, @medId, @qty, @dosage);";
                    
                    using var cmdMed = new MySqlCommand(sqlMed, connection, transaction);
                    cmdMed.Parameters.AddWithValue("@receiptId", receiptId);
                    cmdMed.Parameters.AddWithValue("@medId", med.MedicineID);
                    cmdMed.Parameters.AddWithValue("@qty", med.Quantity);
                    cmdMed.Parameters.AddWithValue("@dosage", med.Dosage);
                    await cmdMed.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return RedirectToAction(nameof(ManageReceipts));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error in CreateReceipt POST: {ex}");
                ModelState.AddModelError("", $"Error: {ex.Message}. A receipt may already exist for this consultation.");
                await PopulateCreateReceiptDropdowns();
                return View(model);
            }
        }

        // GET: Admin/ViewReceipt/1
        [HttpGet]
        public async Task<IActionResult> ViewReceipt(ulong id)
        {
            var receipt = new ReceiptModel { ReceiptID = id, Medicines = new List<ReceiptMedicineModel>() };

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT r.ReceiptID, r.ConsultID, c.Disease,
                        p.PatientID, p.FirstName AS PatientFirst, p.LastName AS PatientLast,
                        d.DoctorID, d.FirstName AS DoctorFirst, d.LastName AS DoctorLast,
                        rm.ReceiptMedicineID, rm.MedicineID, rm.Quantity, rm.Dosage,
                        m.Name AS MedicineName
                    FROM Receipts r
                    INNER JOIN Consultations c ON r.ConsultID = c.ConsultID
                    INNER JOIN Patients p ON c.PatientID = p.PatientID
                    INNER JOIN Doctors d ON c.DoctorID = d.DoctorID
                    LEFT JOIN Receipt_Medicines rm ON r.ReceiptID = rm.ReceiptID
                    LEFT JOIN Medicines m ON rm.MedicineID = m.MedicineID
                    WHERE r.ReceiptID=@id";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    receipt.ConsultationID = (ulong)reader.GetInt32("ConsultID");
                    receipt.Disease = reader.GetString("Disease");
                    receipt.PatientName = $"{reader.GetString("PatientFirst")} {reader.GetString("PatientLast")}";
                    receipt.DoctorName = $"{reader.GetString("DoctorFirst")} {reader.GetString("DoctorLast")}";

                    if (!reader.IsDBNull(reader.GetOrdinal("ReceiptMedicineID")))
                    {
                        receipt.Medicines.Add(new ReceiptMedicineModel
                        {
                            ReceiptMedicineID = (ulong)reader.GetInt32("ReceiptMedicineID"),
                            Medicine = new MedicineModel
                            {
                                MedicineID = (ulong)reader.GetInt32("MedicineID"),
                                Name = reader.GetString("MedicineName")
                            },
                            Quantity = (uint)reader.GetInt32("Quantity"),
                            Dosage = reader.GetString("Dosage")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading receipt: {ex.Message}");
            }

            return View(receipt);
        }

        // GET: Admin/UpdateReceipt/1
        [HttpGet]
        public async Task<IActionResult> UpdateReceipt(ulong id)
        {
            var receipt = new ReceiptModel { ReceiptID = id, Medicines = new List<ReceiptMedicineModel>() };

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT r.ReceiptID, r.ConsultID, c.Disease,
                        p.PatientID, p.FirstName AS PatientFirst, p.LastName AS PatientLast,
                        d.DoctorID, d.FirstName AS DoctorFirst, d.LastName AS DoctorLast,
                        rm.ReceiptMedicineID, rm.MedicineID, rm.Quantity, rm.Dosage,
                        m.Name AS MedicineName
                    FROM Receipts r
                    INNER JOIN Consultations c ON r.ConsultID = c.ConsultID
                    INNER JOIN Patients p ON c.PatientID = p.PatientID
                    INNER JOIN Doctors d ON c.DoctorID = d.DoctorID
                    LEFT JOIN Receipt_Medicines rm ON r.ReceiptID = rm.ReceiptID
                    LEFT JOIN Medicines m ON rm.MedicineID = m.MedicineID
                    WHERE r.ReceiptID=@id";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    receipt.ConsultationID = (ulong)reader.GetInt32("ConsultID");
                    receipt.Disease = reader.GetString("Disease");
                    receipt.PatientName = $"{reader.GetString("PatientFirst")} {reader.GetString("PatientLast")}";
                    receipt.DoctorName = $"{reader.GetString("DoctorFirst")} {reader.GetString("DoctorLast")}";

                    if (!reader.IsDBNull(reader.GetOrdinal("ReceiptMedicineID")))
                    {
                        receipt.Medicines.Add(new ReceiptMedicineModel
                        {
                            ReceiptMedicineID = (ulong)reader.GetInt32("ReceiptMedicineID"),
                            Medicine = new MedicineModel
                            {
                                MedicineID = (ulong)reader.GetInt32("MedicineID"),
                                Name = reader.GetString("MedicineName")
                            },
                            Quantity = (uint)reader.GetInt32("Quantity"),
                            Dosage = reader.GetString("Dosage")
                        });
                    }
                }

                var medicinesList = new List<SelectListItem>();
                await reader.CloseAsync();

                string sqlMedicines = "SELECT MedicineID, Name FROM Medicines ORDER BY Name";
                using var cmdMed = new MySqlCommand(sqlMedicines, connection);
                using var readerMed = await cmdMed.ExecuteReaderAsync();
                while (await readerMed.ReadAsync())
                {
                    medicinesList.Add(new SelectListItem
                    {
                        Value = readerMed["MedicineID"].ToString(),
                        Text = (string)readerMed["Name"]
                    });
                }
                ViewBag.Medicines = medicinesList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading receipt: {ex.Message}");
            }

            return View(receipt);
        }

        // POST: Admin/UpdateReceipt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReceipt(ReceiptModel model)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                foreach (var med in model.Medicines)
                {
                    string updateSql = @"
                        UPDATE Receipt_Medicines
                        SET MedicineID=@medId, Quantity=@qty, Dosage=@dosage
                        WHERE ReceiptMedicineID=@rmId";
                    using var cmd = new MySqlCommand(updateSql, connection);
                    cmd.Parameters.AddWithValue("@medId", med.Medicine.MedicineID);
                    cmd.Parameters.AddWithValue("@qty", med.Quantity);
                    cmd.Parameters.AddWithValue("@dosage", med.Dosage);
                    cmd.Parameters.AddWithValue("@rmId", med.ReceiptMedicineID);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating receipt: {ex.Message}");
                return await UpdateReceipt((ulong)model.ReceiptID);
            }

            return RedirectToAction(nameof(ManageReceipts));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReceipt(ulong id)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string deleteMedSql = "DELETE FROM Receipt_Medicines WHERE ReceiptID=@id";
                using var cmdMed = new MySqlCommand(deleteMedSql, connection);
                cmdMed.Parameters.AddWithValue("@id", id);
                await cmdMed.ExecuteNonQueryAsync();

                string sql = "DELETE FROM Receipts WHERE ReceiptID=@id";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteReceipt {id}: {ex}");
            }

            return RedirectToAction(nameof(ManageReceipts));
        }

        
        // ------------------------- HELPER METHODS -------------------------

        private async Task PopulateConsultationDropdowns(ConsultationModels model)
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                var patients = new List<SelectListItem>();
                string sqlPatients = "SELECT PatientID, FirstName, LastName FROM Patients ORDER BY FirstName, LastName";
                using (var cmdPatients = new MySqlCommand(sqlPatients, connection))
                using (var readerPatients = await cmdPatients.ExecuteReaderAsync())
                {
                    while (await readerPatients.ReadAsync())
                    {
                        patients.Add(new SelectListItem
                        {
                            Value = readerPatients["PatientID"].ToString(),
                            Text = $"{readerPatients["FirstName"]} {readerPatients["LastName"]}",
                            Selected = model.PatientID == (ulong)readerPatients.GetInt32("PatientID")
                        });
                    }
                }
                ViewBag.Patients = patients;

                var doctors = new List<SelectListItem>();
                string sqlDoctors = "SELECT DoctorID, FirstName, LastName FROM Doctors ORDER BY FirstName, LastName";
                using (var cmdDoctors = new MySqlCommand(sqlDoctors, connection))
                using (var readerDoctors = await cmdDoctors.ExecuteReaderAsync())
                {
                    while (await readerDoctors.ReadAsync())
                    {
                        doctors.Add(new SelectListItem
                        {
                            Value = readerDoctors["DoctorID"].ToString(),
                            Text = $"{readerDoctors["FirstName"]} {readerDoctors["LastName"]}",
                            Selected = model.DoctorID == (ulong)readerDoctors.GetInt32("DoctorID")
                        });
                    }
                }
                ViewBag.Doctors = doctors;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error populating consultation dropdowns: {ex.Message}");
            }
        }

        private async Task PopulateCreateUserDropdowns()
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                
                var doctors = new List<SelectListItem>();
                string sqlDoctors = @"
                    SELECT DoctorID, FirstName, LastName 
                    FROM Doctors 
                    WHERE DoctorID NOT IN (SELECT DoctorID FROM Users WHERE DoctorID IS NOT NULL)
                    ORDER BY FirstName, LastName";
                using (var cmdDoctors = new MySqlCommand(sqlDoctors, connection))
                using (var readerDoctors = await cmdDoctors.ExecuteReaderAsync())
                {
                    while (await readerDoctors.ReadAsync())
                    {
                        doctors.Add(new SelectListItem
                        {
                            Value = readerDoctors["DoctorID"].ToString(),
                            Text = $"{readerDoctors["FirstName"]} {readerDoctors["LastName"]}"
                        });
                    }
                }
                ViewBag.Doctors = doctors;
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error populating user dropdowns: {ex.Message}");
            }
        }

        private async Task PopulateCreateReceiptDropdowns()
        {
            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                var consultations = new List<SelectListItem>();
                string sqlConsult = @"
                    SELECT c.ConsultID, c.Date, p.FirstName, p.LastName
                    FROM Consultations c
                    JOIN Patients p ON c.PatientID = p.PatientID
                    WHERE c.ConsultID NOT IN (SELECT ConsultID FROM Receipts)
                    ORDER BY c.Date DESC";
                using (var cmdConsult = new MySqlCommand(sqlConsult, connection))
                using (var readerConsult = await cmdConsult.ExecuteReaderAsync())
                {
                    while (await readerConsult.ReadAsync())
                    {
                        consultations.Add(new SelectListItem
                        {
                            Value = readerConsult["ConsultID"].ToString(),
                            Text = $"({readerConsult.GetDateTime("Date"):yyyy-MM-dd}) - {readerConsult["FirstName"]} {readerConsult["LastName"]}"
                        });
                    }
                }
                ViewBag.Consultations = consultations;

                var medicines = new List<SelectListItem>();
                string sqlMeds = "SELECT MedicineID, Name FROM Medicines ORDER BY Name";
                using (var cmdMeds = new MySqlCommand(sqlMeds, connection))
                using (var readerMeds = await cmdMeds.ExecuteReaderAsync())
                {
                    while (await readerMeds.ReadAsync())
                    {
                        medicines.Add(new SelectListItem
                        {
                            Value = readerMeds["MedicineID"].ToString(),
                            Text = (string)readerMeds["Name"]
                        });
                    }
                }
                ViewBag.Medicines = medicines;
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error populating receipt dropdowns: {ex.Message}");
            }
        }
    }
}