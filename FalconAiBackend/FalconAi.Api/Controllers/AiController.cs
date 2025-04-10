using FalconAi.Api.Dtos;
using FalconAi.Api.Helper;
using FalconAi.Api.Services.cs.IService.cs;
using FalconAi.Domain.Enum;
using FalconAi.Domain.Models;
using FalconAi.Repository;
using FalconAi.Repository.IRepository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace FalconAi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IWebHostEnvironment hostingEnvironment;
        private readonly IConfiguration configuration;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ApplicationDbContext context;

        public AiController(IUnitOfWork unitOfWork, ApplicationDbContext context, IWebHostEnvironment hostingEnvironment, IConfiguration configuration, UserManager<ApplicationUser> userManager)
        {
            this.unitOfWork = unitOfWork;
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;
            this.userManager = userManager;
            this.context = context;
        }
        private string ProcessUploadFile(IFormFile Photo)
        {
            string uniqueFileName = null;
            if (Photo != null)
            {
                string uploadFile = Path.Combine(hostingEnvironment.WebRootPath, "files");
                uniqueFileName = Guid.NewGuid().ToString() + "_" + Photo.FileName;
                string filePath = Path.Combine(uploadFile, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    Photo.CopyTo(fileStream);
                }
            }
            return uniqueFileName;
        }

        private async Task<IActionResult> CreateNotification(NotificationDto request)
        {
            // التحقق من صحة البيانات المرسلة
            if (request == null)
                return BadRequest("❌ البيانات المرسلة غير صحيحة.");

            if (request.UserIds == null || request.UserIds.Count == 0)
                return BadRequest("❌ يجب تحديد المستخدمين لإرسال الإشعار.");

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest("❌ عنوان الإشعار ومحتوى الإشعار مطلوبان.");

            // التحقق من إعدادات OneSignal
            string appIdString = configuration.GetSection(AppSettingKey.OneSignalAppId).Value;
            if (string.IsNullOrWhiteSpace(appIdString) || !Guid.TryParse(appIdString, out Guid appId))
                return BadRequest("❌ OneSignalAppId غير مضبوط أو غير صالح.");

            string restKey = configuration.GetSection(AppSettingKey.OneSignalRestKey).Value;
            if (string.IsNullOrWhiteSpace(restKey))
                return BadRequest("❌ OneSignalRestKey غير مضبوط.");

            // إرسال الإشعار
            string result = await NotificationHelper.OneSignalPushNotification(unitOfWork, userManager, request, appId, restKey);

            // طباعة استجابة OneSignal للتحقق
            Console.WriteLine($"📤 استجابة OneSignal: {result}");

            return Ok(result);
        }


        [HttpGet]
        [Route("GetSkills")]
        public IActionResult GetSkills()
        {
            // Retrieve all non-deleted trials and order them by creation time
            var skills = unitOfWork.Repository<AiSkill>()
                .GetAll()
                .ToList();

            // Check if any trials were found
            if (!skills.Any())
            {
                return BadRequest(new { message = "No Skills Found" });
            }

            // Prepare the result
            var result = skills.Select(skill => new
            {
                Id = skill.Id,
                Name = skill.Name,
                MinScore = skill.MinScore,
                MaxScore = skill.MaxScore,
                });

            return Ok(result);
        }


        [HttpPost]
        [Route("AddAiVideo")]
        public async Task<IActionResult> AddAiVideo([FromForm] AiVideoScoreDto model)
        {
            var attempt = await unitOfWork.Repository<Attempt>().GetById(model.AttemptId);
            if (attempt == null)
                return BadRequest(new { message = "Attempt Not Found" });

            var user = userManager.FindByIdAsync(attempt.PlayerId).Result;

            if (model.Status == true)
            {
                attempt.Status = Status.Accepted;
                attempt.RejectedReason = null;
                if (model.Video != null)
                {
                    attempt.AiVideoPath = ProcessUploadFile(model.Video);
                }

                await unitOfWork.Repository<Attempt>().Update(attempt);
                await unitOfWork.Complete();

                if (user != null)
                {
                    var playerId = user.PlayerId == null ? "not" : user.PlayerId;
                    var request = new NotificationDto
                    {
                        Title = "FALCON AI",
                        Body = "رائع! تم تقييم محاولتك من قِبل النظام ، نعتز بجهودك ونتطلع لرؤية المزيد من إبداعاتك المميزة!",
                        UserIds = new List<string> { user.Id.ToString() },
                        PlayerIds = new List<string> {playerId }
                    };

                    // Call CreateNotification method
                    await CreateNotification(request);
                }
            }
            else
            {
                attempt.Status = Status.Rejected;
                attempt.RejectedReason = model.RejectedMessage;

                //if (model.Video != null)
                //{
                //    attempt.AiVideoPath = ProcessUploadFile(model.Video);
                //}


                await unitOfWork.Repository<Attempt>().Update(attempt);
                await unitOfWork.Complete();

                if (user != null)
                {
                    var playerId = user.PlayerId == null ? "not" : user.PlayerId;
                    var request = new NotificationDto
                    {
                        Title = "FALCON AI",
                        Body = $" {model.RejectedMessage} . نأسف،لم يتم قبول الفيديو الخاص بك من قِبل النظام",
                        UserIds = new List<string> { user.Id.ToString() },
                        PlayerIds = new List<string> { playerId }
                    };

                    // Call CreateNotification method
                    await CreateNotification(request);
                }
            }

            return Ok(new { message = "Attempt Video Added Successfully"});
        }

        [HttpPost]
        [Route("AddScore")]
        public async Task<IActionResult> AddScore([FromBody] AiScoreDto model)
        {
            var attempt = await unitOfWork.Repository<Attempt>().GetById(model.AttemptId);
            if (attempt == null)
                return BadRequest(new { message = "Attempt Not Found" });


            if (model.Skills != null && model.Skills.Count() > 0)
            {
                var attemptSkills = model.Skills
                    .Select(skill => new AttemptSkill
                    {
                        AttemptId = model.AttemptId,
                        Score = skill.Score,
                        SkillId = skill.Id
                    })
                    .ToList();

                // Add the attempt skills to the repository
                foreach (var attemptSkill in attemptSkills)
                {
                    await unitOfWork.Repository<AttemptSkill>().Add(attemptSkill);
                }
            }

            await unitOfWork.Repository<Attempt>().Update(attempt);
            await unitOfWork.Complete();

            return Ok(new { message = "Attempt Score Added Successfully" });
        }

        [HttpGet]
        [Route("GetPlayers")]
        public IActionResult GetPlayers()
        {

            var players = context.Players.ToList();

            var playerProfiles = new List<PlayerProfile>();

            foreach (var player in players)
            {
                var attempts = context.Attempts
                    .Where(a => a.PlayerId == player.Id && a.Status == Status.Accepted)
                    .GroupBy(a => a.ExerciseId)
                    .Select(g => g.OrderByDescending(a => a.CreationTime).FirstOrDefault())
                    .ToList();

                var exerciseIds = attempts.Select(a => a.ExerciseId).Distinct().ToList();
                var trialIds = context.TrialExercises.Where(a => exerciseIds.Contains(a.ExerciseId)).Select(a => a.TrialId).ToList();
                var trials = context.Trials.Where(t => trialIds.Contains(t.Id)).ToList();
                var exercises = context.Exercises.Where(e => exerciseIds.Contains(e.Id)).ToList();

                var trialScores = new Dictionary<string, double>();
                foreach (var trial in trials)
                {
                    var averageScore = exercises
                        .Select(e =>
                        {
                            var attempt = attempts.FirstOrDefault(a => a.ExerciseId == e.Id);
                            if (attempt == null) return 0;
                            var finalScores = GetFinalScoresForAttempt(attempt.Id);
                            return finalScores.Any() ? finalScores.Average() : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Average();

                    trialScores.Add(trial.Title, Math.Round(averageScore, 2));
                }

                var tps = trialScores.Values.Any() ? Math.Round(trialScores.Values.Average(), 2) : 0;
                var aiSkills = context.AiSkills.ToList();
                var skillScores = attempts
                    .SelectMany(attempt => context.AttemptSkills.Where(ar => ar.AttemptId == attempt.Id).ToList())
                    .GroupBy(ar => ar.SkillId)
                    .ToDictionary(
                        g => aiSkills.FirstOrDefault(a => a.Id == g.Key)?.Name ?? "Unknown",
                        g => {
                            var aiSkill = aiSkills.FirstOrDefault(a => a.Id == g.Key);
                            return Math.Round(g.Average(ar => NormalizeScore(ar.Score, aiSkill.MinScore, aiSkill.MaxScore)), 2);
                        }
                    );

                var exerciseScores = attempts
                    .Select(e =>
                    {
                        var attemptResults = context.AttemptSkills.Where(ar => ar.AttemptId == e.Id).ToList();
                        var finalScores = attemptResults.Select(ar =>
                        {
                            var aiSkill = context.AiSkills.FirstOrDefault(a => a.Id == ar.SkillId);
                            return aiSkill != null ? NormalizeScore(ar.Score, aiSkill.MinScore, aiSkill.MaxScore) : 0;
                        }).ToList();
                        var averageScore = finalScores.Any() ? finalScores.Average() : 0;
                        var exerciseName = context.Exercises.FirstOrDefault(ex => ex.Id == e.ExerciseId)?.Title ?? "Unknown";
                        return new { ExerciseName = exerciseName, Score = Math.Round(averageScore, 2) };
                    })
                    .ToDictionary(x => x.ExerciseName, x => x.Score);

                var playerProfile = new PlayerProfile
                {
                    //Name = player.FirstName + " " + player.LastName,
                    //Age = CalculateAge(player.BirthDate),
                    //Gender = player.Gender.ToString() == "Male" ? "ذكر" : "انثى",
                    //DateOfBirth = player.BirthDate.ToString("dd/MM"),
                    //Height = player.Height,
                    //Weight = player.Weight,
                    //Position = player.Position,
                    TPS = tps,
                    TrialScores = trialScores,
                    SkillScores = skillScores,
                    ExerciseScores = exerciseScores,
                };

                playerProfiles.Add(playerProfile);
            }

            return Ok(playerProfiles);
        }

        private List<double> GetFinalScoresForAttempt(int attemptId)
        {
            // جلب المهارات المرتبطة بكل محاولة
            var skills = context.AttemptSkills.Where(s => s.AttemptId == attemptId).ToList();

            var finalScores = new List<double>();

            foreach (var skill in skills)
            {
                // جلب المهارة المرتبطة
                var aiSkill = context.AiSkills.FirstOrDefault(skil => skil.Id == skill.SkillId);
                if (aiSkill == null || aiSkill.MinScore == aiSkill.MaxScore)
                    continue; // إذا كانت المهارة غير موجودة أو النطاق غير صالح

                // تطبيق التطبيع
                var normalizedScore = NormalizeScore(skill.Score, aiSkill.MinScore, aiSkill.MaxScore);
                finalScores.Add(normalizedScore);
            }

            return finalScores;
        }

        public static double NormalizeScore(double value, double minVal, double maxVal)
        {
            // Check if we're doing reverse normalization (min > max)
            bool reverse = minVal > maxVal;
            if (reverse)
            {
                // Swap min and max for calculation
                (minVal, maxVal) = (maxVal, minVal);
            }

            // Calculate normalized value
            double normalizedValue = ((value - minVal) / (maxVal - minVal)) * 10;

            // Clamp between 0 and 10
            normalizedValue = Math.Clamp(normalizedValue, 0, 10);

            // If we're doing reverse normalization, subtract from 10
            if (reverse)
            {
                normalizedValue = 10 - normalizedValue;
            }

            return normalizedValue;
        }


        //[HttpPost]
        //[Route("test")]
        //public async Task<IActionResult> test(string playerid)
        //{

        //            var request = new NotificationDto
        //            {
        //                Title = "FALCON AI",
        //                Body = $"  نأسف،لم يتم قبول الفيديو الخاص بك من قِبل النظام",
        //                UserIds = new List<string> { "33807da0-3d14-4e1d-9dd7-a18c752c2199" },
        //                PlayerIds = new List<string> { playerid }
        //            };

        //            // Call CreateNotification method
        //         var resukt =  CreateNotification(request).Result;

        //    return Ok(new { message = resukt});
        //}




    }
}
