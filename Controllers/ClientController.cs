using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using System.Security.Claims;
using Hospital_simple.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq; 

namespace Hospital_simple.Controllers
{
    [Authorize(Roles = "client")] 
    public class ClientController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ClientController> _logger;

        public ClientController(IConfiguration configuration, ILogger<ClientController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // ------------------------- Dashboard -------------------------
        public async Task<IActionResult> Index()
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            int myPatientsCount = 0;
            string doctorFullName = "";
            var consultations = new List<ConsultationModels>();

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                var cmdPatients = new MySqlCommand("SELECT COUNT(DISTINCT PatientID) FROM Consultations WHERE DoctorID=@doctorId", connection);
                cmdPatients.Parameters.AddWithValue("@doctorId", doctorId);
                myPatientsCount = Convert.ToInt32(await cmdPatients.ExecuteScalarAsync());

                var cmdName = new MySqlCommand("SELECT FirstName, LastName FROM Doctors WHERE DoctorID=@doctorId", connection);
                cmdName.Parameters.AddWithValue("@doctorId", doctorId);
                using var readerName = await cmdName.ExecuteReaderAsync();
                if (await readerName.ReadAsync())
                {
                    doctorFullName = $"{readerName.GetString("FirstName")} {readerName.GetString("LastName")}";
                }
                await readerName.CloseAsync();

                string sqlConsults = @"
                    SELECT c.ConsultID, c.PatientID, p.FirstName AS PatientFirst, p.LastName AS PatientLast,
                           c.Date, c.Disease, c.Observation
                    FROM Consultations c
                    INNER JOIN Patients p ON c.PatientID=p.PatientID
                    WHERE c.DoctorID=@doctorId
                    ORDER BY c.Date DESC
                    LIMIT 3";
                using var cmdConsults = new MySqlCommand(sqlConsults, connection);
                cmdConsults.Parameters.AddWithValue("@doctorId", doctorId);
                using var readerConsults = await cmdConsults.ExecuteReaderAsync();
                while (await readerConsults.ReadAsync())
                {
                    consultations.Add(new ConsultationModels
                    {
                        ConsultationID = (ulong)readerConsults.GetInt32("ConsultID"),
                        PatientID = (ulong)readerConsults.GetInt32("PatientID"),
                        PatientName = $"{readerConsults.GetString("PatientFirst")} {readerConsults.GetString("PatientLast")}",
                        ConsultationDate = readerConsults.GetDateTime("Date"),
                        Disease = readerConsults.IsDBNull(readerConsults.GetOrdinal("Disease")) ? "" : readerConsults.GetString("Disease"),
                        Observation = readerConsults.IsDBNull(readerConsults.GetOrdinal("Observation")) ? "" : readerConsults.GetString("Observation")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
            }

            ViewData["MyPatientsCount"] = myPatientsCount;
            ViewData["DoctorFullName"] = doctorFullName;
            return View(consultations);
        }

        // ------------------------- Patients -------------------------
        public async Task<IActionResult> ManagePatients()
        {
            var patients = new List<PatientModel>();
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                string sql = @"
                    SELECT p.* FROM Patients p
                    INNER JOIN Consultations c ON p.PatientID=c.PatientID
                    WHERE c.DoctorID=@doctorId
                    GROUP BY p.PatientID";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@doctorId", doctorId);
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
                _logger.LogError(ex, "Error loading patients");
            }

            return View(patients);
        }

        // GET: Client/CreatePatient
        [HttpGet]
        public IActionResult CreatePatient()
        {
            return View(); 
        }

        // POST: Client/CreatePatient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatient(PatientModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"INSERT INTO Patients (FirstName, LastName, Email, Address, Insurance)
                            VALUES (@first, @last, @email, @address, @insurance)";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@first", model.FirstName);
                cmd.Parameters.AddWithValue("@last", model.LastName);
                cmd.Parameters.AddWithValue("@email", model.Email);
                cmd.Parameters.AddWithValue("@address", model.Address ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@insurance", model.Insurance ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                ModelState.AddModelError("", "Error creating patient.");
                return View(model);
            }

            return RedirectToAction(nameof(ManagePatients));
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
                _logger.LogError(ex, "Error in UpdatePatient GET {Id}", id);
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
                _logger.LogError(ex, "Error in UpdatePatient POST {Id}", model.PatientID);
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
                _logger.LogError(ex, "Error deleting patient {Id}", id);
                TempData["ErrorMessage"] = "Cannot delete patient linked to consultations.";
            }

            return RedirectToAction(nameof(ManagePatients));
        }

        // ------------------------- Consultations -------------------------
        public async Task<IActionResult> ManageConsultations()
        {
            var consultations = new List<ConsultationModels>();
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT c.ConsultID, c.PatientID, p.FirstName AS PatientFirst, p.LastName AS PatientLast,
                           c.Date, c.Disease, c.Observation,
                           r.ReceiptID
                    FROM Consultations c
                    INNER JOIN Patients p ON c.PatientID=p.PatientID
                    LEFT JOIN Receipts r ON c.ConsultID = r.ConsultID
                    WHERE c.DoctorID=@doctorId
                    ORDER BY c.Date DESC";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@doctorId", doctorId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    consultations.Add(new ConsultationModels
                    {
                        ConsultationID = (ulong)reader.GetInt32("ConsultID"),
                        PatientID = (ulong)reader.GetInt32("PatientID"),
                        PatientName = $"{reader.GetString("PatientFirst")} {reader.GetString("PatientLast")}",
                        ConsultationDate = reader.GetDateTime("Date"),
                        Disease = reader.IsDBNull(reader.GetOrdinal("Disease")) ? "" : reader.GetString("Disease"),
                        Observation = reader.IsDBNull(reader.GetOrdinal("Observation")) ? "" : reader.GetString("Observation"),
                        
                        ReceiptID = reader.IsDBNull(reader.GetOrdinal("ReceiptID")) ? null : (ulong?)reader.GetInt32("ReceiptID")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading consultations");
            }

            return View(consultations);
        }
        
        // ------------------------- Create Consultation -------------------------
        private async Task PopulateCreateConsultationDropdowns()
        {
            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            // Populate Patient dropdown
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
                        Text = $"{readerPatients["FirstName"]} {readerPatients["LastName"]}"
                    });
                }
            }
            ViewBag.Patients = patients;

            // Populate Medicine dropdown
            var medicines = new List<SelectListItem>();
            string sqlMedicines = "SELECT MedicineID, Name FROM Medicines ORDER BY Name";
            using (var cmdMeds = new MySqlCommand(sqlMedicines, connection))
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

        // GET: Client/CreateConsultation
        [HttpGet]
        public async Task<IActionResult> CreateConsultation()
        {
            await PopulateCreateConsultationDropdowns();

            var model = new CreateConsultationViewModel();
            
            for (int i = 0; i < 5; i++)
            {
                model.Medicines.Add(new ReceiptMedicineLineItem());
            }

            return View(model);
        }

        // POST: Client/CreateConsultation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConsultation(CreateConsultationViewModel model)
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            ulong patientId;
            ulong consultationId;
            ulong receiptId;

            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                if (model.IsNewPatient)
                {
                    string sqlPatient = @"
                        INSERT INTO Patients (FirstName, LastName, Email, Address, Insurance)
                        VALUES (@first, @last, @email, @address, @insurance);
                        SELECT LAST_INSERT_ID();";
                    
                    using var cmdPatient = new MySqlCommand(sqlPatient, connection, transaction);
                    cmdPatient.Parameters.AddWithValue("@first", model.NewPatient.FirstName);
                    cmdPatient.Parameters.AddWithValue("@last", model.NewPatient.LastName);
                    cmdPatient.Parameters.AddWithValue("@email", model.NewPatient.Email ?? (object)DBNull.Value);
                    cmdPatient.Parameters.AddWithValue("@address", model.NewPatient.Address ?? (object)DBNull.Value);
                    cmdPatient.Parameters.AddWithValue("@insurance", model.NewPatient.Insurance ?? (object)DBNull.Value);
                    
                    patientId = Convert.ToUInt64(await cmdPatient.ExecuteScalarAsync());
                }
                else
                {
                    if (!model.ExistingPatientID.HasValue)
                    {
                        throw new Exception("No existing patient was selected.");
                    }
                    patientId = model.ExistingPatientID.Value;
                }

                string sqlConsult = @"
                    INSERT INTO Consultations (PatientID, DoctorID, Date, Disease, Observation)
                    VALUES (@patientId, @doctorId, @date, @disease, @obs);
                    SELECT LAST_INSERT_ID();";

                using var cmdConsult = new MySqlCommand(sqlConsult, connection, transaction);
                cmdConsult.Parameters.AddWithValue("@patientId", patientId);
                cmdConsult.Parameters.AddWithValue("@doctorId", doctorId);
                cmdConsult.Parameters.AddWithValue("@date", DateTime.Now); // Set to current time
                cmdConsult.Parameters.AddWithValue("@disease", model.Disease);
                cmdConsult.Parameters.AddWithValue("@obs", model.Observation ?? (object)DBNull.Value);

                consultationId = Convert.ToUInt64(await cmdConsult.ExecuteScalarAsync());
                
                var validMedicines = model.Medicines
                    .Where(m => m.MedicineID > 0 && m.Quantity > 0 && !string.IsNullOrEmpty(m.Dosage))
                    .ToList();

                if (validMedicines.Any())
                {
                    string sqlReceipt = @"
                        INSERT INTO Receipts (ConsultID) VALUES (@consultId);
                        SELECT LAST_INSERT_ID();";
                    
                    using var cmdReceipt = new MySqlCommand(sqlReceipt, connection, transaction);
                    cmdReceipt.Parameters.AddWithValue("@consultId", consultationId);
                    receiptId = Convert.ToUInt64(await cmdReceipt.ExecuteScalarAsync());

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
                }

                await transaction.CommitAsync();

                return RedirectToAction(nameof(ManageConsultations));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating consultation");
                
                await PopulateCreateConsultationDropdowns();
                ModelState.AddModelError("", $"Error: {ex.Message}");
                return View(model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> UpdateConsultation(int id)
        {
            ConsultationModels consultation = null;
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT c.ConsultID, c.PatientID, c.DoctorID, c.Date, c.Disease, c.Observation
                    FROM Consultations c
                    WHERE c.ConsultID=@id";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    if ((ulong)reader.GetInt32("DoctorID") != (ulong)doctorId)
                    {
                        _logger.LogWarning("Unauthorized access attempt: Doctor {DoctorId} tried to access consultation {ConsultId}", doctorId, id);
                        return Unauthorized();
                    }

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

                await PopulateConsultationDropdowns(connection, consultation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GET UpdateConsultation {Id}", id);
                if (consultation == null) consultation = new ConsultationModels();
            }

            return View(consultation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateConsultation(ConsultationModels model)
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string checkSql = "SELECT DoctorID FROM Consultations WHERE ConsultID = @id";
                using var checkCmd = new MySqlCommand(checkSql, connection);
                checkCmd.Parameters.AddWithValue("@id", model.ConsultationID);
                var ownerDoctorId = Convert.ToUInt64(await checkCmd.ExecuteScalarAsync());

                if (ownerDoctorId != (ulong)doctorId)
                {
                    _logger.LogWarning("Unauthorized update attempt: Doctor {DoctorId} tried to update consultation {ConsultId}", doctorId, model.ConsultationID);
                    return Unauthorized();
                }

                if (model.DoctorID != (ulong)doctorId)
                {
                    ModelState.AddModelError("DoctorID", "You cannot assign a consultation to another doctor.");
                }

                if (!ModelState.IsValid)
                {
                    await PopulateConsultationDropdowns(connection, model);
                    return View(model);
                }

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
                _logger.LogError(ex, "Error in POST UpdateConsultation {Id}", model.ConsultationID);
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                await PopulateConsultationDropdowns(connection, model);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConsultation(ulong id)
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string checkSql = "SELECT DoctorID FROM Consultations WHERE ConsultID = @id";
                using var checkCmd = new MySqlCommand(checkSql, connection);
                checkCmd.Parameters.AddWithValue("@id", id);
                var ownerDoctorId = Convert.ToUInt64(await checkCmd.ExecuteScalarAsync());

                if (ownerDoctorId != (ulong)doctorId)
                {
                    _logger.LogWarning("Unauthorized delete attempt: Doctor {DoctorId} tried to delete consultation {ConsultId}", doctorId, id);
                    return Unauthorized();
                }

                // Fix for MARS error: Read IDs into a list first
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
                _logger.LogError(ex, "Error in DeleteConsultation {Id}", id);
            }

            return RedirectToAction(nameof(ManageConsultations));
        }

        private async Task PopulateConsultationDropdowns(MySqlConnection connection, ConsultationModels model)
        {
            try
            {
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
                _logger.LogError(ex, "Error populating consultation dropdowns");
            }
        }


        // ------------------------- Receipts -------------------------
        public async Task<IActionResult> ManageReceipts()
        {
            var receipts = new List<ReceiptModel>();
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT r.ReceiptID, r.ConsultID, c.Disease,
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
                    WHERE d.DoctorID = @doctorId
                    ORDER BY r.ReceiptID";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@doctorId", doctorId);
                using var reader = await cmd.ExecuteReaderAsync();

                var receiptDict = new Dictionary<ulong, ReceiptModel>();

                while (await reader.ReadAsync())
                {
                    var receiptId = (ulong)reader.GetInt32("ReceiptID");
                    if (!receiptDict.ContainsKey(receiptId))
                    {
                        receiptDict[receiptId] = new ReceiptModel
                        {
                            ReceiptID = receiptId,
                            ConsultationID = (ulong)reader.GetInt32("ConsultID"),
                            Disease = reader.IsDBNull(reader.GetOrdinal("Disease")) ? "" : reader.GetString("Disease"),
                            PatientName = $"{reader.GetString("PatientFirst")} {reader.GetString("PatientLast")}",
                            DoctorName = $"{reader.GetString("DoctorFirst")} {reader.GetString("DoctorLast")}",
                            Medicines = new List<ReceiptMedicineModel>()
                        };
                    }

                    if (!reader.IsDBNull(reader.GetOrdinal("ReceiptMedicineID")))
                    {
                        receiptDict[receiptId].Medicines.Add(new ReceiptMedicineModel
                        {
                            ReceiptMedicineID = (ulong)reader.GetInt32("ReceiptMedicineID"),
                            Quantity = (uint)reader.GetInt32("Quantity"),
                            Dosage = reader.GetString("Dosage"),
                            Medicine = new MedicineModel
                            {
                                MedicineID = (ulong)reader.GetInt32("MedicineID"),
                                Name = reader.GetString("MedicineName")
                            }
                        });
                    }
                }
                receipts = new List<ReceiptModel>(receiptDict.Values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ManageReceipts");
            }

            return View(receipts);
        }

        // GET: Client/CreateReceipt
        [HttpGet]
        public async Task<IActionResult> CreateReceipt()
        {
            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var consultations = new List<SelectListItem>();
            string sqlConsults = @"
                SELECT c.ConsultID, CONCAT(p.FirstName, ' ', p.LastName, ' - ', c.Disease) AS DisplayText
                FROM Consultations c
                INNER JOIN Patients p ON c.PatientID = p.PatientID
                ORDER BY c.Date DESC";
            
            using (var cmd = new MySqlCommand(sqlConsults, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    consultations.Add(new SelectListItem
                    {
                        Value = reader["ConsultID"].ToString(),
                        Text = reader["DisplayText"].ToString()
                    });
                }
            }
            ViewBag.Consultations = consultations;

            var medicinesList = new List<SelectListItem>();
            string sqlMeds = "SELECT MedicineID, Name FROM Medicines ORDER BY Name";
            using var cmdMeds = new MySqlCommand(sqlMeds, connection);
            using var readerMeds = await cmdMeds.ExecuteReaderAsync();
            while (await readerMeds.ReadAsync())
            {
                medicinesList.Add(new SelectListItem
                {
                    Value = readerMeds["MedicineID"].ToString(),
                    Text = readerMeds["Name"].ToString()
                });
            }
            ViewBag.Medicines = medicinesList;

            var model = new CreateReceiptModel();
            for (int i = 0; i < 5; i++) model.Medicines.Add(new ReceiptMedicineLineItem());

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReceipt(CreateReceiptModel model)
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (!ModelState.IsValid)
            {
                await PopulateCreateReceiptDropdowns(); 
                return View(model);
            }

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();

                string sqlReceipt = @"INSERT INTO Receipts (ConsultID) VALUES (@consultId);
                                    SELECT LAST_INSERT_ID();";
                using var cmdReceipt = new MySqlCommand(sqlReceipt, connection, transaction);
                cmdReceipt.Parameters.AddWithValue("@consultId", model.ConsultationID);
                ulong receiptId = Convert.ToUInt64(await cmdReceipt.ExecuteScalarAsync());

                var validMeds = model.Medicines
                    .Where(m => m.MedicineID > 0 && m.Quantity > 0 && !string.IsNullOrEmpty(m.Dosage))
                    .ToList();

                foreach (var med in validMeds)
                {
                    string sqlMed = @"INSERT INTO Receipt_Medicines (ReceiptID, MedicineID, Quantity, Dosage)
                                    VALUES (@rId, @medId, @qty, @dosage)";
                    using var cmdMed = new MySqlCommand(sqlMed, connection, transaction);
                    cmdMed.Parameters.AddWithValue("@rId", receiptId);
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
                _logger.LogError(ex, "Error creating receipt");
                ModelState.AddModelError("", "Error creating receipt.");
                await PopulateCreateReceiptDropdowns();
                return View(model);
            }
        }

        private async Task PopulateCreateReceiptDropdowns()
        {
            await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var consultations = new List<SelectListItem>();
            string sqlConsults = @"
                SELECT c.ConsultID, CONCAT(p.FirstName, ' ', p.LastName, ' - ', c.Disease) AS DisplayText
                FROM Consultations c
                INNER JOIN Patients p ON c.PatientID = p.PatientID
                ORDER BY c.Date DESC";
            using (var cmd = new MySqlCommand(sqlConsults, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    consultations.Add(new SelectListItem
                    {
                        Value = reader["ConsultID"].ToString(),
                        Text = reader["DisplayText"].ToString()
                    });
                }
            }
            ViewBag.Consultations = consultations;

            var medicinesList = new List<SelectListItem>();
            string sqlMeds = "SELECT MedicineID, Name FROM Medicines ORDER BY Name";
            using var cmdMeds = new MySqlCommand(sqlMeds, connection);
            using var readerMeds = await cmdMeds.ExecuteReaderAsync();
            while (await readerMeds.ReadAsync())
            {
                medicinesList.Add(new SelectListItem
                {
                    Value = readerMeds["MedicineID"].ToString(),
                    Text = readerMeds["Name"].ToString()
                });
            }
            ViewBag.Medicines = medicinesList;
        }



        // GET: Client/ViewReceipt/1
        [HttpGet]
        public async Task<IActionResult> ViewReceipt(ulong id)
        {
            var receipt = new ReceiptModel { ReceiptID = id, Medicines = new List<ReceiptMedicineModel>() };
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT r.ReceiptID, r.ConsultID, c.Disease,
                        d.DoctorID, d.FirstName AS DoctorFirst, d.LastName AS DoctorLast,
                        p.FirstName AS PatientFirst, p.LastName AS PatientLast,
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
                bool isAuthorized = false;
                while (await reader.ReadAsync())
                {
                    if (!isAuthorized)
                    {
                        if ((ulong)reader.GetInt32("DoctorID") != (ulong)doctorId)
                        {
                            _logger.LogWarning("Unauthorized access attempt: Doctor {DoctorId} tried to access receipt {ReceiptId}", doctorId, id);
                            return Unauthorized();
                        }
                        isAuthorized = true;
                    }

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
                _logger.LogError(ex, "Error in GET ViewReceipt {Id}", id);
            }

            return View(receipt);
        }

        // GET: Client/UpdateReceipt/1
        [HttpGet]
        public async Task<IActionResult> UpdateReceipt(ulong id)
        {
            var receipt = new ReceiptModel { ReceiptID = id, Medicines = new List<ReceiptMedicineModel>() };
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string sql = @"
                    SELECT r.ReceiptID, r.ConsultID, c.Disease,
                        d.DoctorID, d.FirstName AS DoctorFirst, d.LastName AS DoctorLast,
                        p.FirstName AS PatientFirst, p.LastName AS PatientLast,
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
                bool isAuthorized = false;
                while (await reader.ReadAsync())
                {
                    if (!isAuthorized)
                    {
                        if ((ulong)reader.GetInt32("DoctorID") != (ulong)doctorId)
                        {
                            _logger.LogWarning("Unauthorized access attempt: Doctor {DoctorId} tried to access receipt {ReceiptId}", doctorId, id);
                            return Unauthorized();
                        }
                        isAuthorized = true;
                    }

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
                await reader.CloseAsync();

                var medicinesList = new List<SelectListItem>();
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
                _logger.LogError(ex, "Error in GET UpdateReceipt {Id}", id);
            }

            return View(receipt);
        }

        // POST: Client/UpdateReceipt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReceipt(ReceiptModel model)
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string checkSql = @"
                    SELECT d.DoctorID FROM Doctors d
                    JOIN Consultations c ON d.DoctorID = c.DoctorID
                    JOIN Receipts r ON c.ConsultID = r.ConsultID
                    WHERE r.ReceiptID = @id";
                using var checkCmd = new MySqlCommand(checkSql, connection);
                checkCmd.Parameters.AddWithValue("@id", model.ReceiptID);
                var ownerDoctorId = Convert.ToUInt64(await checkCmd.ExecuteScalarAsync());

                if (ownerDoctorId != (ulong)doctorId)
                {
                    _logger.LogWarning("Unauthorized update attempt: Doctor {DoctorId} tried to update receipt {ReceiptId}", doctorId, model.ReceiptID);
                    return Unauthorized();
                }

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
                _logger.LogError(ex, "Error in POST UpdateReceipt {Id}", model.ReceiptID);
                return await UpdateReceipt((ulong)model.ReceiptID);
            }

            return RedirectToAction(nameof(ManageReceipts));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReceipt(ulong id)
        {
            int doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await using var connection = new MySqlConnection(_configuration.GetConnectionString("Default"));
                await connection.OpenAsync();

                string checkSql = @"
                    SELECT d.DoctorID FROM Doctors d
                    JOIN Consultations c ON d.DoctorID = c.DoctorID
                    JOIN Receipts r ON c.ConsultID = r.ConsultID
                    WHERE r.ReceiptID = @id";
                using var checkCmd = new MySqlCommand(checkSql, connection);
                checkCmd.Parameters.AddWithValue("@id", id);
                var ownerDoctorId = Convert.ToUInt64(await checkCmd.ExecuteScalarAsync());

                if (ownerDoctorId != (ulong)doctorId)
                {
                    _logger.LogWarning("Unauthorized delete attempt: Doctor {DoctorId} tried to delete receipt {ReceiptId}", doctorId, id);
                    return Unauthorized();
                }

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
                _logger.LogError(ex, "Error in DeleteReceipt {Id}", id);
            }

            return RedirectToAction(nameof(ManageReceipts));
        }
    }
}