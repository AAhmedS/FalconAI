using FalconAi.Api.Helper;
using FalconAi.Api.Services.cs.IService.cs;
using FalconAi.Domain.Models;
using FalconAi.Repository.IRepository;
using FalconAi.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using FalconAiMvc.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using FalconAi.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using static System.Reflection.Metadata.BlobBuilder;
using Microsoft.Identity.Client;

namespace FalconAiMvc.Controllers
{
    [Authorize]
    public class TrialController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly IUnitOfWork unitOfWork;
        private readonly ApplicationDbContext context;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IUserServices userServices;
        private readonly FileUploadHelper fileUploadHelper;
        private readonly IConfiguration configuration;
        private readonly IEmailServices emailServices;



        public TrialController(UserManager<ApplicationUser> userManager, IEmailServices emailServices, SignInManager<ApplicationUser> signInManager, IUnitOfWork unitOfWork, IConfiguration configuration,
            ApplicationDbContext context, RoleManager<IdentityRole> roleManager, IUserServices userServices, FileUploadHelper fileUploadHelper)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.unitOfWork = unitOfWork;
            this.context = context;
            this.roleManager = roleManager;
            this.userServices = userServices;
            this.fileUploadHelper = fileUploadHelper;
            this.configuration = configuration;
            this.emailServices = emailServices;
        }

        private async Task<IActionResult> CreateNotification(NotificationDto request)
        {
            Guid appId = Guid.Parse(configuration.GetSection(AppSettingKey.OneSignalAppId).Value);
            string restKey = configuration.GetSection(AppSettingKey.OneSignalRestKey).Value;
            string result = await NotificationHelper.OneSignalPushNotification(unitOfWork, userManager, request, appId, restKey);
            return Ok(result);
        }

        [Route("proxy/image")]
        public IActionResult ProxyImage(string path)
        {
            // Full URL to the external HTTP image
            string imageUrl = userServices.Path() + path;

            using (var client = new HttpClient())
            {
                var response = client.GetAsync(imageUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsByteArrayAsync().Result;
                    return File(content, "image/jpeg"); // or appropriate content type based on image
                }
                return NotFound(); // Handle image not found case
            }
        }

        [Route("proxy/adminImage")]
        public IActionResult ProxyAdminImage(string path)
        {
            // Full URL to the external HTTP image
            string imageUrl = userServices.AdminPath() + path;

            using (var client = new HttpClient())
            {
                var response = client.GetAsync(imageUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsByteArrayAsync().Result;
                    return File(content, "image/jpeg"); // or appropriate content type based on image
                }
                return NotFound(); // Handle image not found case
            }
        }

        [Route("proxy/video")]
        public IActionResult ProxyVideo(string path)
        {
            // Full URL to the external HTTP image
            string imageUrl = userServices.Path() + path;

            using (var client = new HttpClient())
            {
                var response = client.GetAsync(imageUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsByteArrayAsync().Result;
                    return File(content, "video/mp4"); // or appropriate content type based on image
                }
                return NotFound(); // Handle image not found case
            }
        }

        [Route("proxy/adminVideo")]
        public IActionResult ProxyAdminVideo(string path)
        {
            // Full URL to the external HTTP image
            string imageUrl = userServices.AdminPath() + path;

            using (var client = new HttpClient())
            {
                var response = client.GetAsync(imageUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsByteArrayAsync().Result;
                    return File(content, "video/mp4"); // or appropriate content type based on image
                }
                return NotFound(); // Handle image not found case
            }
        }
        private bool IsPasswordStrong(string password)
        {
            // Check if password meets the criteria
            return Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$");
        }


        public IActionResult TrialHome()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetTrials()
        {
            // Get all sports into memory
            var trialsList = unitOfWork.Repository<Trial>().GetAll().ToList();

            // Prepare a list for the final result
            var sportData = trialsList.Select(trial => new
            {
                Id = trial.Id,
                Image = Url.Action("ProxyAdminImage", new { path = trial.PhotoPath }),
                Title = trial.Title,
                // Description = trial.Description,
                MinAge = trial.MinAge,
                MaxAge = trial.MaxAge,
                GenderType = trial.Gender == 0 ? "ذكر" : "أنثى",
                Gender = trial.Gender,
                Country = trial.Country,
                IsClosed = trial.IsColsed == false ? false : true,
                IsOpen = trial.IsOpen == true ? "مفتوحة" : "مغلقة",
                Club = trial.ClubId == null || trial.ClubId == 0 ? "لا يوجد" : context.PlayerClubs.FirstOrDefault(c => c.Id == trial.ClubId)?.Name,
                Exercise = context.TrialExercises.Count(e => e.TrialId == trial.Id),
                //Bookings = context.Attempts.Where(a=> context.Exercises.Where(a => a.TrialId == trial.Id).Select(a => a.Id).Contains(a.Id)).Select(a=>a.PlayerId).Distinct().Count() ,
                Bookings = context.Attempts
    .Where(a => context.TrialExercises
        .Where(te => te.TrialId == trial.Id) // جلب التمارين المرتبطة بالتجربة
        .Select(te => te.ExerciseId) // استخراج معرفات التمارين
        .Contains(a.ExerciseId)) // التحقق من أن المحاولة تخص أحد هذه التمارين
    .Select(a => a.PlayerId) // استخراج معرفات اللاعبين
    .Distinct() // إزالة التكرارات
    .Count(), // حساب عدد اللاعبين الفريدين
                IsDeleted = trial.IsDeleted == true ? "نعم" : "لا"
            }).ToList();

            // Return the sports data as JSON
            return Json(sportData);
        }


        public IActionResult AddTrial()
        {
            var model = new TrialViewModel
            {
                Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList(),
            };
            return View (model);
        }

        [HttpPost]
        public async Task<IActionResult> AddTrial(TrialViewModel model)
        {
            if (model.ImageFile == null || model.ImageFile.Length == 0)
                ModelState.AddModelError("ImageFile", "يرجى اختيار صورة");

            if (model.ClubId == null)
                ModelState.AddModelError("ClubId", "يرجى اختيار نادي");

            if (!ModelState.IsValid)
            {
                model.Clubs = context.PlayerClubs
               .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
               .ToList();
                // Return validation errors as JSON if the model state is invalid
                return View(model);
            }

            // Check if the model state is valid
            else if (ModelState.IsValid)
            {

                var trial = new Trial
                {
                    Title = model.Title,
                    //  Description = model.Description,
                    ClubId = model.ClubId.Value,
                    PhotoPath = fileUploadHelper.ProcessUploadFile(model.ImageFile),
                    CreationTime = DateTime.UtcNow.ToLocalTime(),
                    MinAge = model.MinAge,
                    Country = model.Country,
                    Gender = model.Gender,
                    MaxAge = model.MaxAge,
                    IsOpen = model.Open == 1 ? true : false
                };

                // Add the sport to the repository
                await unitOfWork.Repository<Trial>().Add(trial);
                await unitOfWork.Complete();

                // Query all users with non-null PlayerId and IsDeleted false
                var usersToNotify = await context.Users
                    .Where(u => u.PlayerId != null && !u.IsDeleted)
                    .ToListAsync();

                var request = new NotificationDto
                {
                    Title = "FALCON AI",
                    Body = "تم إضافة تجربة جديدة في التطبيق",
                    UserIds = usersToNotify.Select(u => u.Id.ToString()).ToList(),
                    PlayerIds = usersToNotify.Select(u => u.PlayerId.ToString()).ToList()
                };

                await CreateNotification(request);

                ViewBag.SuccessMessage = "تم اضافة التجربة بنجاح"; // Use TempData for redirects
                model.Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList();
                return View(model); // Redirect to Index or the same page            }
            }

            model.Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList();
            // Return validation errors as JSON if the model state is invalid
            return View(model);
        }

        public IActionResult UpdateTrial(int Id)
        {
            var trial = unitOfWork.Repository<Trial>().GetById(Id).Result;
            var model = new TrialViewModel
            {
                
                ClubId = trial.ClubId,
                Country = trial.Country,
                Gender = trial.Gender,
                Id = trial.Id,
                MaxAge = trial.MaxAge,
                MinAge = trial.MinAge,
                Open = trial.IsOpen == true ? 1 : 0,
                Title = trial.Title,
                Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList(),
                
            };
            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> UpdateTrial(TrialViewModel model)
        {
            //if (model.Title == null)
            //    ModelState.AddModelError("Title", "العنوان مطلوب");
            if (ModelState.IsValid)
            {

                var trial = unitOfWork.Repository<Trial>().GetById(model.Id).Result;
                if (trial == null)
                    return BadRequest(new { message = "This Sport Not Found" });
                
                if (!ModelState.IsValid)
                {
                    model.Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList();
                    return View(model);
                }

                // trial.Description = model.Description;
                trial.Title = model.Title;
                trial.Gender = model.Gender;
                trial.MinAge = model.MinAge;
                trial.MaxAge = model.MaxAge;
                trial.Country = model.Country;
                trial.IsOpen = model.Open == 1 ? true : false;
                trial.ClubId = model.ClubId;

                if (model.ImageFile != null)
                {
                    trial.PhotoPath = fileUploadHelper.ProcessUploadFile(model.ImageFile);
                }
                else
                {
                    trial.PhotoPath = trial.PhotoPath;
                }


                await unitOfWork.Repository<Trial>().Update(trial);
                await unitOfWork.Complete();
                model.Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList();

                ViewBag.SuccessMessage = "تم تعديل التجربة بنجاح"; // Use TempData for redirects
                return View(model);
            }

            model.Clubs = context.PlayerClubs
                .Select(rp => new SelectListItem { Value = rp.Id.ToString(), Text = rp.Name })
                .ToList();
            return View(model);


        }


        public IActionResult GetTrialBookingCount(int id)
        {
            //var ids = context.Exercises.Where(a => a.TrialId == id).Distinct().Select(a => a.Id).ToList();
            var ids = context.TrialExercises
              .Where(te => te.TrialId == id) // جلب العلاقات المرتبطة بالتجربة
              .Select(te => te.ExerciseId) // استخراج معرفات التمارين
              .Distinct() // إزالة أي تكرارات
              .ToList();

            var bookingCount = context.Attempts.Count(b => ids.Contains(b.ExerciseId));
            return Json(new { bookingCount });
        }


        [HttpPost]
        public async Task<IActionResult> DeleteTrial(int Id)
        {
            var trial = await unitOfWork.Repository<Trial>().GetById(Id); // Use await here
            if (trial == null)
                return BadRequest(new { message = "This Sport Not Found" });

            var ids = context.TrialExercises.Where(a => a.TrialId == Id).Select(a => a.ExerciseId).ToList();
            var Bookings = context.Attempts.Count(b => ids.Contains(b.ExerciseId));
            if (Bookings > 0)
            {
                trial.IsDeleted = true;
                await unitOfWork.Repository<Trial>().Update(trial);
                await unitOfWork.Complete(); // Ensure completion is asynchronous

            }
            else
            {
                await unitOfWork.Repository<Trial>().Delete(trial);
                await unitOfWork.Complete(); // Ensure completion is asynchronous

            }

            return Ok();

        }


        [HttpPost]
        public async Task<IActionResult> StopTrial(int Id)
        {
            var trial = await unitOfWork.Repository<Trial>().GetById(Id); // Use await here
            if (trial == null)
                return BadRequest(new { message = "This Sport Not Found" });
            bool status = trial.IsColsed == true ? false : true;



            trial.IsColsed = status;
            await unitOfWork.Repository<Trial>().Update(trial);
            await unitOfWork.Complete(); // Ensure completion is asynchronous

            return Ok();

        }

        public IActionResult TrialExercise(int trialId)
        {
            var trial = unitOfWork.Repository<Trial>().GetById(trialId).Result;
            ViewData["TrialId"] = trialId;
            var model = new ExerciseNumberViewModel
            {
                TrialId = trial.Id,
                TrialTitle = trial.Title
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult GetTrialExercises(int trialId)
        {
            // جلب مزودي الخدمة من قاعدة البيانات
            var ids = context.TrialExercises.Where(a => a.TrialId == trialId).Select(a => a.ExerciseId).ToList();
            var exercises = context.Exercises.Where(s => ids.Contains(s.Id)).OrderByDescending(u => u.Id).ToList();

            // إعداد النتيجة النهائية مع تحويل نوع الخدمة إلى نص مناسب
            var result = exercises.Select(e => new
            {
                Id = e.Id,
                Title = e.Title,
                TitleAr = e.TitleAr,
                //Description = e.Description,
                TrialId = trialId,
                Image = Url.Action("ProxyAdminImage", new { path = e.PhotoPath }),
                // Videos = context.ExerciseVideos.Count(v=>v.ExerciseId == e.Id),
                // Equipments = context.ExerciseEquipments.Count(v => v.ExerciseId == e.Id),
                // Instructions = context.Playernstructions.Count(v => v.ExerciseId == e.Id),
                //Skills = context.Skills.Count(v => v.ExerciseId == e.Id),
                //Bookings = unitOfWork.Repository<Attempt>()
                //.GetAll()
                //.Where(attempt => attempt.ExerciseId == e.Id)
                //.Select(attempt => attempt.PlayerId) // Select unique player IDs
                //.Distinct() // Ensure uniqueness
                //.Count(),
                IsDeleted = e.IsDeleted == true ? "نعم" : "لا",
                Number = "#" +e.ExerciseNumber ,
            }).ToList();
            ViewData["TrialId"] = trialId;

            // إرجاع النتيجة بصيغة JSON
            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteExerciseFromTrial(int ExerciseId, int TrialId)
        {

            var exercise = context.TrialExercises.FirstOrDefault(a => a.ExerciseId == ExerciseId && a.TrialId == TrialId);

            await unitOfWork.Repository<TrialExercise>().Delete(exercise);
            await unitOfWork.Complete();

            return Ok();


        }


        public IActionResult AddTrialExercise(int Id)
        {
            var model = new AttemptViewModel
            {
                Id = Id
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddTrialExercise(AttemptViewModel model)
        {
            if(model.AccountNumber == null)
            {
                ModelState.AddModelError("AccountNumber", "رقم التمرين مطلوب");
                return View(model);

            }

            var exercise = context.Exercises.FirstOrDefault(a => a.ExerciseNumber == model.AccountNumber);
            if (model.AccountNumber != null && exercise == null)
            {
                ModelState.AddModelError("AccountNumber", "رقم التمرين غير صحيح");
                return View(model);

            }
                  var trialex = new TrialExercise
                {
                    TrialId = model.Id,
                    ExerciseId = exercise.Id,

                };

                await unitOfWork.Repository<TrialExercise>().Add(trialex);
                await unitOfWork.Complete();

            ViewBag.SuccessMessage = "تم اضافة التمرين للتجربة بنجاح";
            return View(model);

        }



    }
}
