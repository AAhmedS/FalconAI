using FalconAi.Domain.Enum;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace FalconAiMvc.ViewModels
{
    public class TrialViewModel
    {
        public int Id { get; set; }
        public IFormFile? ImageFile { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Title { get; set; }

        //[Required(ErrorMessage = "الوصف مطلوب")]
        //public string? Description { get; set; }

        [Required(ErrorMessage = "العمر مطلوب")]
        public int? MinAge { get; set; }

        [Required(ErrorMessage = "العمر مطلوب")]
        public int? MaxAge { get; set; }
        public Gender? Gender { get; set; }
        public int? Open {  get; set; }

        [Required(ErrorMessage = "البلد مطلوبة")]
        public string Country { get; set; }
        public int? ClubId { get; set; }
        public IEnumerable<SelectListItem>? Clubs { get; set; } = new List<SelectListItem>();



    }
}
